using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Services;
public sealed class TripService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : ITripService
{
    private enum ActionKind { Start, Pickup, Deliver, Complete, Cancel, Incident }
    private IQueryable<Trip> Accessible() =>
        from t in db.Trips join b in db.Bookings on t.BookingId equals b.Id
        where b.OwnerUserId == actor.UserId || b.DriverUserId == actor.UserId select t;
    private static TripResponse Map(Trip t) => new(t.Id,t.BookingId,t.DriverUserId,t.VehicleId,t.Status,t.Version,
        t.StartedAt,t.PickedUpAt,t.DeliveredAt,t.CompletedAt,t.CancelledAt);
    public async Task<ShipmentResult<TripResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var trip = await Accessible().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id,cancellationToken);
        return trip is null ? ShipmentResult<TripResponse>.Fail("not_found") : new(Map(trip));
    }
    public async Task<ShipmentResult<IReadOnlyList<TripEventResponse>>> EventsAsync(Guid id,int page,CancellationToken cancellationToken)
    {
        if (page < 1 || page > 100000) { return ShipmentResult<IReadOnlyList<TripEventResponse>>.Fail("invalid_pagination"); }
        if (!await Accessible().AnyAsync(x => x.Id == id,cancellationToken)) { return ShipmentResult<IReadOnlyList<TripEventResponse>>.Fail("not_found"); }
        return new(await db.TripEvents.AsNoTracking().Where(x => x.TripId == id).OrderByDescending(x => x.OccurredAt).ThenBy(x => x.Id)
            .Skip((page-1)*20).Take(20).Select(x => new TripEventResponse(x.Id,x.ActorUserId,x.Action,x.Note,x.ProofId,x.OccurredAt)).ToListAsync(cancellationToken));
    }
    public Task<ShipmentResult<TripResponse>> StartAsync(Guid id,TripVersionRequest r,CancellationToken ct) => ChangeAsync(id,r.Version,ActionKind.Start,null,null,ct);
    public Task<ShipmentResult<TripResponse>> PickupAsync(Guid id,TripHandoverRequest r,CancellationToken ct) => ChangeAsync(id,r.Version,ActionKind.Pickup,r.ProofId,r.Note,ct);
    public Task<ShipmentResult<TripResponse>> DeliverAsync(Guid id,TripHandoverRequest r,CancellationToken ct) => ChangeAsync(id,r.Version,ActionKind.Deliver,r.ProofId,r.Note,ct);
    public Task<ShipmentResult<TripResponse>> CompleteAsync(Guid id,TripVersionRequest r,CancellationToken ct) => ChangeAsync(id,r.Version,ActionKind.Complete,null,null,ct);
    public Task<ShipmentResult<TripResponse>> CancelAsync(Guid id,TripReasonRequest r,CancellationToken ct) => ChangeAsync(id,r.Version,ActionKind.Cancel,null,r.Reason,ct);
    public Task<ShipmentResult<TripResponse>> ReportIncidentAsync(Guid id,TripReasonRequest r,CancellationToken ct) => ChangeAsync(id,r.Version,ActionKind.Incident,null,r.Reason,ct);
    private async Task<ShipmentResult<TripResponse>> ChangeAsync(Guid id,long version,ActionKind action,Guid? proofId,string? note,CancellationToken ct)
    {
        var t = await Accessible().SingleOrDefaultAsync(x => x.Id == id,ct);
        if (t is null) { return ShipmentResult<TripResponse>.Fail("not_found"); }
        if (!await db.Users.AnyAsync(x => x.Id == actor.UserId && (x.AccountStatus == "Active" || x.AccountStatus == "PendingKyc"),ct))
        { return ShipmentResult<TripResponse>.Fail("forbidden"); }
        var booking = await db.Bookings.SingleAsync(x => x.Id == t.BookingId,ct);
        var driverAction = action is ActionKind.Start or ActionKind.Pickup or ActionKind.Deliver;
        if ((driverAction && t.DriverUserId != actor.UserId) || (action == ActionKind.Complete && booking.OwnerUserId != actor.UserId))
        { return ShipmentResult<TripResponse>.Fail("forbidden"); }
        if (t.Version != version || t.Status is TripStates.Completed or TripStates.Cancelled)
        { return ShipmentResult<TripResponse>.Fail("conflict"); }
        var valid = action switch
        {
            ActionKind.Start => t.Status == TripStates.Assigned,
            ActionKind.Pickup => t.Status == TripStates.InProgress,
            ActionKind.Deliver => t.Status == TripStates.Loaded,
            ActionKind.Complete => t.Status == TripStates.Delivered,
            ActionKind.Cancel => t.Status is TripStates.Assigned or TripStates.InProgress,
            ActionKind.Incident => true,
            _ => false
        };
        if (!valid) { return ShipmentResult<TripResponse>.Fail("invalid_transition"); }
        if ((action is ActionKind.Cancel or ActionKind.Incident) && (string.IsNullOrWhiteSpace(note) || note.Trim().Length < 3))
        { return ShipmentResult<TripResponse>.Fail("reason_required"); }
        if (action is ActionKind.Pickup or ActionKind.Deliver)
        {
            if (!await db.TripProofs.AnyAsync(x => x.Id == proofId && x.TripId == id && x.UploaderUserId == actor.UserId,ct))
            { return ShipmentResult<TripResponse>.Fail("proof_required"); }
            if (await db.TripEvents.AnyAsync(x => x.TripId == id && x.ProofId == proofId,ct))
            { return ShipmentResult<TripResponse>.Fail("proof_already_used"); }
        }
        if (action == ActionKind.Start)
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            var eligible = await (from p in db.DriverProfiles join v in db.Vehicles on p.UserId equals v.DriverUserId
                join u in db.Users on p.UserId equals u.Id
                where p.UserId == actor.UserId && p.Status == "Approved" && p.LicenseExpiresOn >= today
                    && v.Id == t.VehicleId && v.Status == "Approved" && u.AccountStatus == "Active" select p.UserId).AnyAsync(ct);
            if (!eligible) { return ShipmentResult<TripResponse>.Fail("driver_not_eligible"); }
        }
        var now = clock.GetUtcNow();
        switch (action)
        {
            case ActionKind.Start: t.Status=TripStates.InProgress;t.StartedAt=now;break;
            case ActionKind.Pickup: t.Status=TripStates.Loaded;t.PickedUpAt=now;break;
            case ActionKind.Deliver: t.Status=TripStates.Delivered;t.DeliveredAt=now;break;
            case ActionKind.Complete: t.Status=TripStates.Completed;t.CompletedAt=now;break;
            case ActionKind.Cancel: t.Status=TripStates.Cancelled;t.CancelledAt=now;break;
        }
        if (action is ActionKind.Complete or ActionKind.Cancel)
        {
            var shipment = await db.Shipments.SingleAsync(x => x.Id == booking.ShipmentId,ct);
            shipment.Status = t.Status;
            shipment.Version++;
            shipment.UpdatedAt=now;
            booking.Status=t.Status;
        }
        t.Version++;
        db.TripEvents.Add(new TripEvent {Id=Guid.NewGuid(),TripId=id,ActorUserId=actor.UserId,Action=action.ToString(),Note=note?.Trim(),ProofId=proofId,OccurredAt=now});
        db.AuditEvents.Add(new AuditEvent {ActorUserId=actor.UserId,TargetType="Trip",TargetId=id,Action="trip."+action.ToString().ToLowerInvariant(),Outcome="Succeeded",OccurredAt=now});
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear();return ShipmentResult<TripResponse>.Fail("conflict"); }
        return new(Map(t));
    }
    public async Task<ShipmentResult<TripProofResponse>> UploadProofAsync(Guid id,UploadTripProofRequest request,CancellationToken cancellationToken)
    {
        var trip=await Accessible().SingleOrDefaultAsync(x => x.Id == id,cancellationToken);
        if (trip is null) { return ShipmentResult<TripProofResponse>.Fail("not_found"); }
        if (trip.DriverUserId != actor.UserId) { return ShipmentResult<TripProofResponse>.Fail("forbidden"); }
        if (trip.Status is not (TripStates.InProgress or TripStates.Loaded)) { return ShipmentResult<TripProofResponse>.Fail("invalid_transition"); }
        var bytes=request.Content;
        if (bytes is null || bytes.Length < 8 || bytes.Length > 5242880) { return ShipmentResult<TripProofResponse>.Fail("invalid_image"); }
        var png=request.ContentType=="image/png" && bytes.AsSpan(0,8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10});
        var jpeg=request.ContentType=="image/jpeg" && bytes[0]==255 && bytes[1]==216 && bytes[2]==255 && bytes[^2]==255 && bytes[^1]==217;
        if (!png && !jpeg) { return ShipmentResult<TripProofResponse>.Fail("invalid_image"); }
        var proof=new TripProof {Id=Guid.NewGuid(),TripId=id,UploaderUserId=actor.UserId,ContentType=request.ContentType,Content=bytes,CreatedAt=clock.GetUtcNow()};
        trip.Version++;
        db.TripProofs.Add(proof);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear();return ShipmentResult<TripProofResponse>.Fail("conflict"); }
        return new(new(proof.Id,proof.ContentType,proof.Content.Length,proof.CreatedAt,trip.Version));
    }
    public async Task<ShipmentResult<TripProofFile>> DownloadProofAsync(Guid id,Guid proofId,CancellationToken cancellationToken)
    {
        if (!await Accessible().AnyAsync(x => x.Id == id,cancellationToken)) { return ShipmentResult<TripProofFile>.Fail("not_found"); }
        var proof=await db.TripProofs.AsNoTracking().Where(x => x.Id == proofId && x.TripId == id)
            .Select(x => new TripProofFile(x.ContentType,x.Content)).SingleOrDefaultAsync(cancellationToken);
        return proof is null ? ShipmentResult<TripProofFile>.Fail("not_found") : new(proof);
    }
}
