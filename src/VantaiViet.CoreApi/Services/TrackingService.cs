using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Hubs;
using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Services;
public sealed class TrackingService(CoreDbContext db,ICurrentActor actor,TimeProvider clock,IHubContext<TrackingHub> hub,
    ITripRouteService routing,IHostEnvironment environment,ILogger<TrackingService> logger) : ITrackingService
{
    private IQueryable<Trip> Visible(Guid userId) =>
        from t in db.Trips join b in db.Bookings on t.BookingId equals b.Id
        join u in db.Users on userId equals u.Id
        where (u.AccountStatus=="Active" || u.AccountStatus=="PendingKyc") &&
            (b.OwnerUserId==userId || t.DriverUserId==userId || db.TripParticipants.Any(p=>p.TripId==t.Id && p.UserId==userId))
        select t;
    public Task<bool> CanViewAsync(Guid id,Guid authenticatedUserId,CancellationToken ct) => Visible(authenticatedUserId).AnyAsync(x=>x.Id==id,ct);
    private static bool Sending(string status) => status is TripStates.InProgress or TripStates.Loaded;
    private static GpsPointResponse Map(TripLocation p) => new(p.Id,p.PointId,p.Latitude,p.Longitude,p.AccuracyMeters,p.RecordedAt,p.ReceivedAt,p.Simulated);
    public async Task<ShipmentResult<IReadOnlyList<TrackingTripResponse>>> ListAsync(int page,CancellationToken cancellationToken)
    {
        if (page<1 || page>100000) { return ShipmentResult<IReadOnlyList<TrackingTripResponse>>.Fail("invalid_pagination"); }
        return new(await (from t in Visible(actor.UserId).AsNoTracking()
            join b in db.Bookings on t.BookingId equals b.Id join s in db.Shipments on b.ShipmentId equals s.Id
            orderby t.CreatedAt descending,t.Id select new TrackingTripResponse(t.Id,s.Title,t.Status))
            .Skip((page-1)*20).Take(20).ToListAsync(cancellationToken));
    }
    public async Task<ShipmentResult<TrackingSnapshot>> SnapshotAsync(Guid id,CancellationToken cancellationToken)
    {
        var t=await Visible(actor.UserId).AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,cancellationToken);
        if (t is null) { return ShipmentResult<TrackingSnapshot>.Fail("not_found"); }
        var b=await db.Bookings.AsNoTracking().SingleAsync(x=>x.Id==t.BookingId,cancellationToken);
        var s=await db.Shipments.AsNoTracking().SingleAsync(x=>x.Id==b.ShipmentId,cancellationToken);
        var latest=await db.TripLocations.AsNoTracking().Where(x=>x.TripId==id).OrderByDescending(x=>x.RecordedAt).ThenByDescending(x=>x.Id).FirstOrDefaultAsync(cancellationToken);
        return new(new(id,t.Status,s.Title,s.PickupAddress,s.DeliveryAddress,
            s.PickupLatitude.HasValue?new(s.PickupLatitude.Value,s.PickupLongitude!.Value):null,
            s.DeliveryLatitude.HasValue?new(s.DeliveryLatitude.Value,s.DeliveryLongitude!.Value):null,
            latest is null?null:Map(latest),latest is null || clock.GetUtcNow()-latest.RecordedAt>TimeSpan.FromMinutes(2),
            t.DriverUserId==actor.UserId && Sending(t.Status),b.OwnerUserId==actor.UserId));
    }
    public async Task<ShipmentResult<GpsBatchResponse>> SendAsync(Guid id,GpsBatchRequest request,CancellationToken cancellationToken)
    {
        if (request.Points is null || request.Points.Length is <1 or >100) { return ShipmentResult<GpsBatchResponse>.Fail("invalid_points"); }
        await using var tx=await db.Database.BeginTransactionAsync(cancellationToken);
        // Serializes terminal transitions with uploads; GPS does not change the lifecycle version.
        var t=await db.Trips.FromSqlInterpolated($"""SELECT * FROM public."Trips" WHERE "Id"={id} AND "DriverUserId"={actor.UserId} FOR UPDATE""").SingleOrDefaultAsync(cancellationToken);
        if (t is null) { return ShipmentResult<GpsBatchResponse>.Fail("not_found"); }
        if (!Sending(t.Status) || !t.StartedAt.HasValue) { return ShipmentResult<GpsBatchResponse>.Fail("tracking_stopped"); }
        if (!await db.Users.AnyAsync(x=>x.Id==actor.UserId && x.AccountStatus=="Active",cancellationToken)) { return ShipmentResult<GpsBatchResponse>.Fail("forbidden"); }
        var now=clock.GetUtcNow();
        var points=request.Points.Select(p=>p with { RecordedAt=DateTimeOffset.FromUnixTimeMilliseconds(p.RecordedAt.ToUnixTimeMilliseconds()) }).ToArray();
        if (points.Any(p=>p.PointId==Guid.Empty || !double.IsFinite(p.Latitude) || !double.IsFinite(p.Longitude) || !double.IsFinite(p.AccuracyMeters)
            || p.Latitude is <-90 or >90 || p.Longitude is <-180 or >180 || p.AccuracyMeters is <0 or >1000
            || p.RecordedAt<t.StartedAt.Value.AddMilliseconds(-1) || p.RecordedAt<now.AddHours(-24) || p.RecordedAt>now.AddSeconds(30)
            || (p.Simulated && !environment.IsDevelopment())))
        { return ShipmentResult<GpsBatchResponse>.Fail("invalid_points"); }
        var unique=points.DistinctBy(x=>x.PointId).ToArray();
        if (unique.Length!=points.Length) { return ShipmentResult<GpsBatchResponse>.Fail("duplicate_point_ids"); }
        var ids=unique.Select(x=>x.PointId).ToArray();
        var existing=await db.TripLocations.Where(x=>x.TripId==id && ids.Contains(x.PointId)).ToDictionaryAsync(x=>x.PointId,cancellationToken);
        foreach (var p in unique)
        {
            if (existing.TryGetValue(p.PointId,out var e))
            {
                if (e.Latitude!=p.Latitude || e.Longitude!=p.Longitude || e.AccuracyMeters!=p.AccuracyMeters || e.RecordedAt!=p.RecordedAt || e.Simulated!=p.Simulated)
                { return ShipmentResult<GpsBatchResponse>.Fail("point_conflict"); }
            }
        }
        foreach (var p in unique.Where(p=>!existing.ContainsKey(p.PointId)))
        {
            db.TripLocations.Add(new TripLocation {TripId=id,PointId=p.PointId,Latitude=p.Latitude,Longitude=p.Longitude,
                AccuracyMeters=p.AccuracyMeters,RecordedAt=p.RecordedAt,ReceivedAt=now,Simulated=p.Simulated});
        }
        await db.SaveChangesAsync(cancellationToken);await tx.CommitAsync(cancellationToken);
        // Only invalidation is broadcast. Fetching actual coordinates always rechecks authorization.
        try { await hub.Clients.Group(id.ToString()).SendAsync("Refresh",cancellationToken); }
        catch (Exception ex) when(ex is not OperationCanceledException)
        { logger.LogWarning("Tracking notification unavailable; clients will poll. Type: {Type}",ex.GetType().Name); }
        return new(new(unique.Length-existing.Count,existing.Count));
    }
    public async Task<ShipmentResult<GpsHistoryResponse>> HistoryAsync(Guid id,long afterId,CancellationToken cancellationToken)
    {
        if (afterId<0) { return ShipmentResult<GpsHistoryResponse>.Fail("invalid_cursor"); }
        if (!await CanViewAsync(id,actor.UserId,cancellationToken)) { return ShipmentResult<GpsHistoryResponse>.Fail("not_found"); }
        var rows=await db.TripLocations.AsNoTracking().Where(x=>x.TripId==id && x.Id>afterId).OrderBy(x=>x.Id).Take(501).ToListAsync(cancellationToken);
        var page=rows.Take(500).ToArray();
        return new(new(page.Select(Map).ToArray(),page.Length==0?afterId:page[^1].Id,rows.Count>500));
    }
    public async Task<ShipmentResult<bool>> SetStopsAsync(Guid id,SetTripStopsRequest r,CancellationToken cancellationToken)
    {
        if (r.Pickup is null || r.Delivery is null || !Valid(r.Pickup) || !Valid(r.Delivery)) { return ShipmentResult<bool>.Fail("invalid_coordinates"); }
        await using var tx=await db.Database.BeginTransactionAsync(cancellationToken);
        var t=await db.Trips.FromSqlInterpolated($"""SELECT * FROM public."Trips" WHERE "Id"={id} FOR UPDATE""").SingleOrDefaultAsync(cancellationToken);
        if (t is null) { return ShipmentResult<bool>.Fail("not_found"); }
        var b=await db.Bookings.SingleAsync(x=>x.Id==t.BookingId,cancellationToken);
        if (b.OwnerUserId!=actor.UserId || !await CanViewAsync(id,actor.UserId,cancellationToken)) { return ShipmentResult<bool>.Fail("not_found"); }
        if (t.Status!=TripStates.Assigned) { return ShipmentResult<bool>.Fail("conflict"); }
        var s=await db.Shipments.SingleAsync(x=>x.Id==b.ShipmentId,cancellationToken);
        s.PickupLatitude=r.Pickup.Latitude;s.PickupLongitude=r.Pickup.Longitude;
        s.DeliveryLatitude=r.Delivery.Latitude;s.DeliveryLongitude=r.Delivery.Longitude;s.Version++;s.UpdatedAt=clock.GetUtcNow();
        Audit(id,"tracking.stops_changed");await db.SaveChangesAsync(cancellationToken);await tx.CommitAsync(cancellationToken);
        return new(true);
    }
    private static bool Valid(CoordinateDto p) => double.IsFinite(p.Latitude) && double.IsFinite(p.Longitude) && p.Latitude is >=-90 and <=90 && p.Longitude is >=-180 and <=180;
    public async Task<ShipmentResult<IReadOnlyList<TrackingParticipantResponse>>> ParticipantsAsync(Guid id,CancellationToken cancellationToken)
    {
        if (!await IsOwnerAsync(id,cancellationToken)) { return ShipmentResult<IReadOnlyList<TrackingParticipantResponse>>.Fail("not_found"); }
        return new(await (from p in db.TripParticipants join u in db.Users on p.UserId equals u.Id where p.TripId==id
            orderby p.UserId select new TrackingParticipantResponse(u.Id,u.DisplayName)).ToListAsync(cancellationToken));
    }
    private async Task<bool> IsOwnerAsync(Guid id,CancellationToken ct) =>
        await (from t in Visible(actor.UserId) join b in db.Bookings on t.BookingId equals b.Id
            where t.Id==id && b.OwnerUserId==actor.UserId select t.Id).AnyAsync(ct);
    public async Task<ShipmentResult<bool>> SetParticipantAsync(Guid id,Guid userId,bool allow,CancellationToken cancellationToken)
    {
        await using var tx=await db.Database.BeginTransactionAsync(cancellationToken);
        var trip=await db.Trips.FromSqlInterpolated($"""SELECT * FROM public."Trips" WHERE "Id"={id} FOR UPDATE""").SingleOrDefaultAsync(cancellationToken);
        if (trip is null || !await IsOwnerAsync(id,cancellationToken)) { return ShipmentResult<bool>.Fail("not_found"); }
        var p=await db.TripParticipants.SingleOrDefaultAsync(x=>x.TripId==id && x.UserId==userId,cancellationToken);
        if (allow && p is null)
        {
            var eligible=await (from u in db.Users join ur in db.UserRoles on u.Id equals ur.UserId join r in db.Roles on ur.RoleId equals r.Id
                where u.Id==userId && (u.AccountStatus=="Active" || u.AccountStatus=="PendingKyc") && (r.NormalizedName=="SHIPPER" || r.NormalizedName=="BROKER")
                select u.Id).AnyAsync(cancellationToken);
            if (!eligible) { return ShipmentResult<bool>.Fail("invalid_participant"); }
            db.TripParticipants.Add(new TripParticipant {TripId=id,UserId=userId,GrantedByUserId=actor.UserId,GrantedAt=clock.GetUtcNow()});
        }
        else if (!allow && p is not null) { db.TripParticipants.Remove(p); }
        Audit(id,allow?"tracking.viewer_added":"tracking.viewer_removed");
        await db.SaveChangesAsync(cancellationToken);await tx.CommitAsync(cancellationToken);
        return new(true);
    }
    private void Audit(Guid id,string action) => db.AuditEvents.Add(new AuditEvent {ActorUserId=actor.UserId,TargetType="Trip",TargetId=id,Action=action,Outcome="Succeeded",OccurredAt=clock.GetUtcNow()});
    public async Task<ShipmentResult<RouteResponse>> RouteAsync(Guid id,CancellationToken cancellationToken)
    {
        var snapshot=await SnapshotAsync(id,cancellationToken);
        if (snapshot.Data is null) { return ShipmentResult<RouteResponse>.Fail("not_found"); }
        if (snapshot.Data.Pickup is null || snapshot.Data.Delivery is null) { return new(new("MissingStops",[],null,null)); }
        var route = await routing.GetAsync(id,snapshot.Data.Pickup,snapshot.Data.Delivery,cancellationToken);
        if (!await CanViewAsync(id,actor.UserId,cancellationToken)) { return ShipmentResult<RouteResponse>.Fail("not_found"); }
        return new(route);
    }
}
