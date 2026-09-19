using Microsoft.EntityFrameworkCore;
using Npgsql;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Services;
public sealed class BookingService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IBookingService
{
    private async Task<bool> CanManageAsync(Shipment shipment, CancellationToken ct)
    {
        var roles = shipment.IsExternalOrder ? new[] { "BROKER" } : new[] { "SHIPPER", "BROKER" };
        return await (from u in db.Users
                      join membership in db.UserRoles on u.Id equals membership.UserId
                      join role in db.Roles on membership.RoleId equals role.Id
                      where u.Id == actor.UserId && u.Id == shipment.OwnerUserId
                          && (u.AccountStatus == "Active" || u.AccountStatus == "PendingKyc")
                          && roles.Contains(role.NormalizedName!)
                      select u.Id).AnyAsync(ct);
    }
    private static TransportRequestResponse Map(TransportRequest r) => new(r.Id,r.ShipmentId,r.DriverUserId,r.VehicleId,r.Status);
    private async Task<bool> EligibleAsync(Guid driver,Guid vehicle,Shipment shipment,CancellationToken ct)
    {
        var day=DateOnly.FromDateTime(shipment.PickupAt.UtcDateTime);
        return await (from u in db.Users join ur in db.UserRoles on u.Id equals ur.UserId
            join role in db.Roles on ur.RoleId equals role.Id
            join p in db.DriverProfiles on u.Id equals p.UserId
            join v in db.Vehicles on u.Id equals v.DriverUserId
            where u.Id==driver && u.AccountStatus=="Active" && role.NormalizedName=="DRIVER"
                && p.Status=="Approved" && p.LicenseExpiresOn>=day && v.Id==vehicle && v.Status=="Approved"
                && v.MaxPayloadKg>=shipment.WeightKg select u.Id).AnyAsync(ct);
    }
    private void Audit(string action,Guid id)
    {
        db.AuditEvents.Add(new AuditEvent {ActorUserId=actor.UserId,Action=action,TargetType="TransportRequest",TargetId=id,Outcome="Succeeded",OccurredAt=clock.GetUtcNow()});
    }
    public async Task<ShipmentResult<TransportRequestResponse>> RequestAsync(Guid shipmentId,RequestTransportDto request,CancellationToken cancellationToken)
    {
        var s=await db.Shipments.SingleOrDefaultAsync(x=>x.Id==shipmentId,cancellationToken);
        if(s is null) { return ShipmentResult<TransportRequestResponse>.Fail("not_found"); }
        if(s.Status!="Published" || s.PickupAt<=clock.GetUtcNow() || s.OwnerUserId==actor.UserId) { return ShipmentResult<TransportRequestResponse>.Fail("conflict"); }
        if(!await EligibleAsync(actor.UserId,request.VehicleId,s,cancellationToken)) { return ShipmentResult<TransportRequestResponse>.Fail("driver_not_eligible"); }
        var previous=await db.TransportRequests.SingleOrDefaultAsync(x=>x.ShipmentId==shipmentId && x.DriverUserId==actor.UserId,cancellationToken);
        if(previous is not null)
        {
            return previous.VehicleId==request.VehicleId && previous.Status=="Pending" ? new(Map(previous)) : ShipmentResult<TransportRequestResponse>.Fail("conflict");
        }
        var r=new TransportRequest {Id=Guid.NewGuid(),ShipmentId=shipmentId,DriverUserId=actor.UserId,VehicleId=request.VehicleId,CreatedAt=clock.GetUtcNow()};
        db.TransportRequests.Add(r);
        s.Version++;
        Audit("transport.requested",r.Id);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch(DbUpdateConcurrencyException) { return ShipmentResult<TransportRequestResponse>.Fail("conflict"); }
        catch(DbUpdateException e) when(e.InnerException is PostgresException {SqlState:PostgresErrorCodes.UniqueViolation}) { return ShipmentResult<TransportRequestResponse>.Fail("conflict"); }
        return new(Map(r));
    }
    public async Task<ShipmentResult<IReadOnlyList<TransportRequestResponse>>> ListRequestsAsync(Guid? shipmentId,int page,CancellationToken cancellationToken)
    {
        if(page<1 || page>100000) { return ShipmentResult<IReadOnlyList<TransportRequestResponse>>.Fail("invalid_pagination"); }
        var q=db.TransportRequests.AsNoTracking();
        if(shipmentId.HasValue)
        {
            if(!await db.Shipments.AnyAsync(x=>x.Id==shipmentId && x.OwnerUserId==actor.UserId,cancellationToken)) { return ShipmentResult<IReadOnlyList<TransportRequestResponse>>.Fail("not_found"); }
            q=q.Where(x=>x.ShipmentId==shipmentId);
        }
        else { q=q.Where(x=>x.DriverUserId==actor.UserId); }
        return new(await q.OrderByDescending(x=>x.CreatedAt).ThenBy(x=>x.Id).Skip((page-1)*20).Take(20)
            .Select(x=>new TransportRequestResponse(x.Id,x.ShipmentId,x.DriverUserId,x.VehicleId,x.Status)).ToListAsync(cancellationToken));
    }
    public async Task<ShipmentResult<BookingResponse>> AcceptAsync(Guid requestId,CancellationToken cancellationToken)
    {
        await using var tx=await db.Database.BeginTransactionAsync(cancellationToken);
        // Lock the shipment before requests so competing confirmations have one winner.
        var shipmentId=await db.TransportRequests.Where(x=>x.Id==requestId).Select(x=>(Guid?)x.ShipmentId).SingleOrDefaultAsync(cancellationToken);
        if(shipmentId is null) { return ShipmentResult<BookingResponse>.Fail("not_found"); }
        var s=await db.Shipments.FromSqlInterpolated($"""SELECT * FROM public."Shipments" WHERE "Id"={shipmentId.Value} AND "OwnerUserId"={actor.UserId} FOR UPDATE""").SingleOrDefaultAsync(cancellationToken);
        if(s is null) { return ShipmentResult<BookingResponse>.Fail("not_found"); }
        var r=await db.TransportRequests.FromSqlInterpolated($"""SELECT * FROM public."TransportRequests" WHERE "Id"={requestId} FOR UPDATE""").SingleAsync(cancellationToken);
        var existing=await (from b in db.Bookings join t in db.Trips on b.Id equals t.BookingId
            where b.RequestId==requestId select new BookingResponse(b.Id,b.ShipmentId,b.DriverUserId,b.VehicleId,t.Id,t.Status,b.ConfirmedAt)).SingleOrDefaultAsync(cancellationToken);
        if(!await CanManageAsync(s,cancellationToken)) { return ShipmentResult<BookingResponse>.Fail("forbidden"); }
        if(existing is not null) { return new(existing); }
        if(s.Status!="Published" || r.Status!="Pending" || s.PickupAt<=clock.GetUtcNow()) { return ShipmentResult<BookingResponse>.Fail("conflict"); }
        // Keep approvals, capacity and account status stable until confirmation commits.
        await db.Database.ExecuteSqlInterpolatedAsync($"""SELECT "Id" FROM public."Users" WHERE "Id"={r.DriverUserId} FOR SHARE""", cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""SELECT "UserId" FROM public."UserRoles" WHERE "UserId"={r.DriverUserId} FOR SHARE""", cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""SELECT "UserId" FROM public."DriverProfiles" WHERE "UserId"={r.DriverUserId} FOR SHARE""", cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""SELECT "Id" FROM public."Vehicles" WHERE "Id"={r.VehicleId} FOR SHARE""", cancellationToken);
        if(!await EligibleAsync(r.DriverUserId,r.VehicleId,s,cancellationToken)) { return ShipmentResult<BookingResponse>.Fail("driver_not_eligible"); }
        if(await db.Trips.AnyAsync(x=>(x.DriverUserId==r.DriverUserId || x.VehicleId==r.VehicleId) && (x.Status==TripStates.Assigned || x.Status==TripStates.InProgress || x.Status==TripStates.Loaded || x.Status==TripStates.Delivered),cancellationToken))
        {
            return ShipmentResult<BookingResponse>.Fail("resource_unavailable");
        }
        var booking=new Booking {Id=Guid.NewGuid(),ShipmentId=s.Id,RequestId=r.Id,OwnerUserId=s.OwnerUserId,DriverUserId=r.DriverUserId,VehicleId=r.VehicleId,ConfirmedAt=clock.GetUtcNow()};
        var trip=new Trip {Id=Guid.NewGuid(),BookingId=booking.Id,DriverUserId=r.DriverUserId,VehicleId=r.VehicleId,CreatedAt=clock.GetUtcNow()};
        db.Bookings.Add(booking);db.Trips.Add(trip);
        r.Status="Accepted";s.Status="Booked";s.Version++;s.UpdatedAt=clock.GetUtcNow();
        await db.TransportRequests.Where(x=>x.ShipmentId==s.Id && x.Id!=r.Id && x.Status=="Pending").ExecuteUpdateAsync(x=>x.SetProperty(p=>p.Status,"Rejected"),cancellationToken);
        Audit("transport.accepted",r.Id);
        try { await db.SaveChangesAsync(cancellationToken);await tx.CommitAsync(cancellationToken); }
        catch(DbUpdateConcurrencyException) { return ShipmentResult<BookingResponse>.Fail("conflict"); }
        catch(DbUpdateException e) when(e.InnerException is PostgresException {SqlState:PostgresErrorCodes.UniqueViolation}) { return ShipmentResult<BookingResponse>.Fail("resource_unavailable"); }
        return new(new(booking.Id,s.Id,r.DriverUserId,r.VehicleId,trip.Id,trip.Status,booking.ConfirmedAt));
    }
    public async Task<ShipmentResult<TransportRequestResponse>> CloseRequestAsync(Guid requestId,bool withdraw,CancellationToken cancellationToken)
    {
        var q=from r in db.TransportRequests join s in db.Shipments on r.ShipmentId equals s.Id
            where r.Id==requestId && (withdraw?r.DriverUserId==actor.UserId:s.OwnerUserId==actor.UserId) select r;
        var request=await q.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if(request is null) { return ShipmentResult<TransportRequestResponse>.Fail("not_found"); }
        await using var tx=await db.Database.BeginTransactionAsync(cancellationToken);
        var status=withdraw?"Withdrawn":"Rejected";
        var changed=await db.TransportRequests.Where(x=>x.Id==requestId && x.Status=="Pending").ExecuteUpdateAsync(x=>x.SetProperty(r=>r.Status,status),cancellationToken);
        if(changed!=1) { return ShipmentResult<TransportRequestResponse>.Fail("conflict"); }
        Audit(withdraw?"transport.withdrawn":"transport.rejected",requestId);
        await db.SaveChangesAsync(cancellationToken);await tx.CommitAsync(cancellationToken);
        request.Status=status;return new(Map(request));
    }
    public async Task<ShipmentResult<IReadOnlyList<BookingResponse>>> ListBookingsAsync(int page,CancellationToken cancellationToken)
    {
        if(page<1 || page>100000) { return ShipmentResult<IReadOnlyList<BookingResponse>>.Fail("invalid_pagination"); }
        return new(await (from b in db.Bookings.AsNoTracking() join t in db.Trips on b.Id equals t.BookingId
            where b.OwnerUserId==actor.UserId || b.DriverUserId==actor.UserId
            orderby b.ConfirmedAt descending,b.Id
            select new BookingResponse(b.Id,b.ShipmentId,b.DriverUserId,b.VehicleId,t.Id,t.Status,b.ConfirmedAt))
            .Skip((page-1)*20).Take(20).ToListAsync(cancellationToken));
    }
}
