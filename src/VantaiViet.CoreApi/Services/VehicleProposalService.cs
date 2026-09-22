using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

/// <summary>A broker's direct, negotiated offer on a driver's open truck: features 13 (send), 18 (the price is the offer),
/// 19 (the driver's one counter) and 20 ("tài xế nhận đơn" — accepting books the driver exactly like accepting a request does).</summary>
public sealed class VehicleProposalService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IVehicleProposalService
{
    private Task<bool> CanManageAsync(Guid shipmentId, CancellationToken ct) =>
        db.Database.SqlQuery<bool>($"""SELECT public."CanDispatchShipment"({shipmentId}, {actor.UserId}) AS "Value" """).SingleAsync(ct);

    private async Task<bool> EligibleAsync(Guid driverUserId, Guid vehicleId, Shipment shipment, CancellationToken ct)
    {
        var day = DateOnly.FromDateTime(shipment.PickupAt.UtcDateTime);
        return await (from u in db.Users
            join ur in db.UserRoles on u.Id equals ur.UserId
            join role in db.Roles on ur.RoleId equals role.Id
            join p in db.DriverProfiles on u.Id equals p.UserId
            join v in db.Vehicles on u.Id equals v.DriverUserId
            where u.Id == driverUserId && u.AccountStatus == "Active" && role.NormalizedName == "DRIVER"
                && p.Status == "Approved" && p.LicenseExpiresOn >= day && v.Id == vehicleId && v.Status == "Approved"
                && v.MaxPayloadKg >= shipment.WeightKg
            select u.Id).AnyAsync(ct);
    }

    private static string Money(decimal value) => value.ToString("F0", CultureInfo.InvariantCulture);

    private async Task<VehicleProposalResponse> MapAsync(VehicleProposal p, CancellationToken ct)
    {
        var shipment = await db.Shipments.AsNoTracking().Where(x => x.Id == p.ShipmentId).Select(x => new { x.Title, x.Code }).SingleAsync(ct);
        var driverName = await db.Users.AsNoTracking().Where(x => x.Id == p.DriverUserId).Select(x => x.DisplayName).SingleAsync(ct);
        var vehicleType = await db.Vehicles.AsNoTracking().Where(x => x.Id == p.VehicleId).Select(x => x.VehicleType).SingleAsync(ct);
        return new(p.Id, p.ShipmentId, shipment.Title, shipment.Code, p.VehicleAvailabilityId, p.DriverUserId, driverName, p.VehicleId, vehicleType,
            Money(p.ProposedPrice), p.CounterPrice is { } counter ? Money(counter) : null, ShipmentLimits.DefaultCurrency, p.Status, p.Version, p.CreatedAt, p.UpdatedAt);
    }

    public async Task<ShipmentResult<VehicleProposalResponse>> ProposeAsync(Guid shipmentId, CreateVehicleProposalRequest request, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(shipmentId, cancellationToken))
        {
            return ShipmentResult<VehicleProposalResponse>.Fail("not_found");
        }

        var shipment = await db.Shipments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == shipmentId, cancellationToken);
        if (shipment is null)
        {
            return ShipmentResult<VehicleProposalResponse>.Fail("not_found");
        }

        if (shipment.Status != "Published")
        {
            return ShipmentResult<VehicleProposalResponse>.Fail("conflict");
        }

        var availability = await db.VehicleAvailabilities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.VehicleAvailabilityId, cancellationToken);
        var now = clock.GetUtcNow();
        if (availability is null || availability.Status != VehicleAvailabilityStatuses.Open || availability.ExpiresAt <= now)
        {
            return ShipmentResult<VehicleProposalResponse>.Fail("availability_not_open");
        }

        var hasLiveProposal = await db.VehicleProposals.AsNoTracking().AnyAsync(
            x => x.VehicleAvailabilityId == availability.Id && (x.Status == VehicleProposalStatuses.Pending || x.Status == VehicleProposalStatuses.CounterOffered),
            cancellationToken);
        if (hasLiveProposal)
        {
            return ShipmentResult<VehicleProposalResponse>.Fail("conflict");
        }

        var proposal = new VehicleProposal
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipmentId,
            VehicleAvailabilityId = availability.Id,
            BrokerUserId = actor.UserId,
            DriverUserId = availability.DriverUserId,
            VehicleId = availability.VehicleId,
            ProposedPrice = decimal.Parse(request.ProposedPrice, CultureInfo.InvariantCulture),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.VehicleProposals.Add(proposal);
        db.AuditEvents.Add(new AuditEvent { ActorUserId = actor.UserId, Action = "vehicle_proposal.sent", TargetType = "VehicleProposal", TargetId = proposal.Id, Outcome = "Succeeded", OccurredAt = now });
        await NotificationEvents.RaiseAsync(db, clock, NotificationTypes.ProposalReceived, proposal.DriverUserId, proposal.ShipmentId, actor.UserId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new(await MapAsync(proposal, cancellationToken));
    }

    public async Task<ShipmentResult<IReadOnlyList<VehicleProposalResponse>>> ForShipmentAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(shipmentId, cancellationToken))
        {
            return ShipmentResult<IReadOnlyList<VehicleProposalResponse>>.Fail("not_found");
        }

        var rows = await db.VehicleProposals.AsNoTracking().Where(x => x.ShipmentId == shipmentId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(50).ToListAsync(cancellationToken);
        var mapped = new List<VehicleProposalResponse>(rows.Count);
        foreach (var row in rows) { mapped.Add(await MapAsync(row, cancellationToken)); }
        return new(mapped);
    }

    public async Task<ShipmentResult<IReadOnlyList<VehicleProposalResponse>>> MineAsync(CancellationToken cancellationToken)
    {
        var rows = await db.VehicleProposals.AsNoTracking().Where(x => x.DriverUserId == actor.UserId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(50).ToListAsync(cancellationToken);
        var mapped = new List<VehicleProposalResponse>(rows.Count);
        foreach (var row in rows) { mapped.Add(await MapAsync(row, cancellationToken)); }
        return new(mapped);
    }

    public async Task<ShipmentResult<bool>> WithdrawAsync(Guid id, long version, CancellationToken cancellationToken)
    {
        var proposal = await db.VehicleProposals.SingleOrDefaultAsync(x => x.Id == id && x.BrokerUserId == actor.UserId, cancellationToken);
        if (proposal is null) { return ShipmentResult<bool>.Fail("not_found"); }
        if (VehicleProposalStatuses.IsFinal(proposal.Status)) { return ShipmentResult<bool>.Fail("conflict"); }
        if (proposal.Version != version) { return ShipmentResult<bool>.Fail("conflict"); }
        proposal.Status = VehicleProposalStatuses.Withdrawn;
        proposal.UpdatedAt = clock.GetUtcNow();
        proposal.Version++;
        await NotificationEvents.RaiseAsync(db, clock, NotificationTypes.ProposalWithdrawn, proposal.DriverUserId, proposal.ShipmentId, actor.UserId, cancellationToken);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return ShipmentResult<bool>.Fail("conflict"); }
        return new(true);
    }

    public async Task<ShipmentResult<VehicleProposalResponse>> RejectAsync(Guid id, long version, CancellationToken cancellationToken)
    {
        var proposal = await db.VehicleProposals.SingleOrDefaultAsync(x => x.Id == id && x.DriverUserId == actor.UserId, cancellationToken);
        if (proposal is null) { return ShipmentResult<VehicleProposalResponse>.Fail("not_found"); }
        if (VehicleProposalStatuses.IsFinal(proposal.Status)) { return ShipmentResult<VehicleProposalResponse>.Fail("conflict"); }
        if (proposal.Version != version) { return ShipmentResult<VehicleProposalResponse>.Fail("conflict"); }
        proposal.Status = VehicleProposalStatuses.Rejected;
        proposal.UpdatedAt = clock.GetUtcNow();
        proposal.Version++;
        await NotificationEvents.RaiseAsync(db, clock, NotificationTypes.ProposalRejected, proposal.BrokerUserId, proposal.ShipmentId, actor.UserId, cancellationToken);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return ShipmentResult<VehicleProposalResponse>.Fail("conflict"); }
        return new(await MapAsync(proposal, cancellationToken));
    }

    public async Task<ShipmentResult<VehicleProposalResponse>> CounterAsync(Guid id, CounterVehicleProposalRequest request, CancellationToken cancellationToken)
    {
        var proposal = await db.VehicleProposals.SingleOrDefaultAsync(x => x.Id == id && x.DriverUserId == actor.UserId, cancellationToken);
        if (proposal is null) { return ShipmentResult<VehicleProposalResponse>.Fail("not_found"); }
        // Only one counter round: a fresh Pending offer can be countered, a reply to that counter cannot be countered again.
        if (proposal.Status != VehicleProposalStatuses.Pending) { return ShipmentResult<VehicleProposalResponse>.Fail("conflict"); }
        if (proposal.Version != request.Version) { return ShipmentResult<VehicleProposalResponse>.Fail("conflict"); }
        proposal.CounterPrice = decimal.Parse(request.CounterPrice, CultureInfo.InvariantCulture);
        proposal.Status = VehicleProposalStatuses.CounterOffered;
        proposal.UpdatedAt = clock.GetUtcNow();
        proposal.Version++;
        await NotificationEvents.RaiseAsync(db, clock, NotificationTypes.ProposalCountered, proposal.BrokerUserId, proposal.ShipmentId, actor.UserId, cancellationToken);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return ShipmentResult<VehicleProposalResponse>.Fail("conflict"); }
        return new(await MapAsync(proposal, cancellationToken));
    }

    public async Task<ShipmentResult<VehicleProposalResponse>> AcceptAsync(Guid id, long version, CancellationToken cancellationToken)
    {
        var proposal = await db.VehicleProposals.SingleOrDefaultAsync(x => x.Id == id && x.DriverUserId == actor.UserId, cancellationToken);
        if (proposal is null) { return ShipmentResult<VehicleProposalResponse>.Fail("not_found"); }
        if (proposal.Status != VehicleProposalStatuses.Pending) { return ShipmentResult<VehicleProposalResponse>.Fail("conflict"); }
        if (proposal.Version != version) { return ShipmentResult<VehicleProposalResponse>.Fail("conflict"); }
        return await FinalizeAsync(proposal, proposal.ProposedPrice, cancellationToken);
    }

    public async Task<ShipmentResult<VehicleProposalResponse>> AcceptCounterAsync(Guid id, long version, CancellationToken cancellationToken)
    {
        var proposal = await db.VehicleProposals.SingleOrDefaultAsync(x => x.Id == id && x.BrokerUserId == actor.UserId, cancellationToken);
        if (proposal is null || proposal.CounterPrice is null) { return ShipmentResult<VehicleProposalResponse>.Fail("not_found"); }
        if (proposal.Status != VehicleProposalStatuses.CounterOffered) { return ShipmentResult<VehicleProposalResponse>.Fail("conflict"); }
        if (proposal.Version != version) { return ShipmentResult<VehicleProposalResponse>.Fail("conflict"); }
        if (!await CanManageAsync(proposal.ShipmentId, cancellationToken)) { return ShipmentResult<VehicleProposalResponse>.Fail("forbidden"); }
        return await FinalizeAsync(proposal, proposal.CounterPrice.Value, cancellationToken);
    }

    /// <summary>Both sides have now agreed on a price: books the driver exactly as accepting a normal request would
    /// (a matching <see cref="TransportRequest"/> is created straight into "Accepted" so takers lists, chat and tracking
    /// all work exactly as for a driver-initiated booking), closes the posting, and passes over any other live proposal on it.</summary>
    private async Task<ShipmentResult<VehicleProposalResponse>> FinalizeAsync(VehicleProposal proposal, decimal finalPrice, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var s = await db.Shipments.FromSqlInterpolated($"""SELECT * FROM public."Shipments" WHERE "Id"={proposal.ShipmentId} FOR UPDATE""").SingleOrDefaultAsync(cancellationToken);
        if (s is null || s.Status != "Published" || s.PickupAt <= clock.GetUtcNow())
        {
            return ShipmentResult<VehicleProposalResponse>.Fail("conflict");
        }

        var availability = await db.VehicleAvailabilities.FromSqlInterpolated(
            $"""SELECT * FROM public."VehicleAvailabilities" WHERE "Id"={proposal.VehicleAvailabilityId} FOR UPDATE""").SingleOrDefaultAsync(cancellationToken);
        if (availability is null || availability.Status != VehicleAvailabilityStatuses.Open)
        {
            return ShipmentResult<VehicleProposalResponse>.Fail("conflict");
        }

        // Keep approvals, capacity and account status stable until confirmation commits.
        await db.Database.ExecuteSqlInterpolatedAsync($"""SELECT "Id" FROM public."Users" WHERE "Id"={proposal.DriverUserId} FOR SHARE""", cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""SELECT "UserId" FROM public."DriverProfiles" WHERE "UserId"={proposal.DriverUserId} FOR SHARE""", cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""SELECT "Id" FROM public."Vehicles" WHERE "Id"={proposal.VehicleId} FOR SHARE""", cancellationToken);
        if (!await EligibleAsync(proposal.DriverUserId, proposal.VehicleId, s, cancellationToken))
        {
            return ShipmentResult<VehicleProposalResponse>.Fail("driver_not_eligible");
        }

        if (await db.Trips.AnyAsync(x => (x.DriverUserId == proposal.DriverUserId || x.VehicleId == proposal.VehicleId)
            && (x.Status == TripStates.Assigned || x.Status == TripStates.InProgress || x.Status == TripStates.Loaded || x.Status == TripStates.Delivered), cancellationToken))
        {
            return ShipmentResult<VehicleProposalResponse>.Fail("resource_unavailable");
        }

        var now = clock.GetUtcNow();
        var request = new TransportRequest { Id = Guid.NewGuid(), ShipmentId = s.Id, DriverUserId = proposal.DriverUserId, VehicleId = proposal.VehicleId, Status = "Accepted", CreatedAt = now };
        var booking = new Booking { Id = Guid.NewGuid(), ShipmentId = s.Id, RequestId = request.Id, OwnerUserId = s.OwnerUserId, DriverUserId = proposal.DriverUserId, VehicleId = proposal.VehicleId, ConfirmedAt = now };
        var trip = new Trip { Id = Guid.NewGuid(), BookingId = booking.Id, DriverUserId = proposal.DriverUserId, VehicleId = proposal.VehicleId, CreatedAt = now };
        db.TransportRequests.Add(request);
        db.Bookings.Add(booking);
        db.Trips.Add(trip);
        if (proposal.BrokerUserId != s.OwnerUserId)
        {
            db.TripParticipants.Add(new TripParticipant { TripId = trip.Id, UserId = proposal.BrokerUserId, GrantedByUserId = s.OwnerUserId, GrantedAt = now });
        }

        await db.TransportRequests.Where(x => x.ShipmentId == s.Id && x.Id != request.Id && x.Status == "Pending")
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.Status, "Rejected"), cancellationToken);
        await db.VehicleProposals.Where(x => x.VehicleAvailabilityId == availability.Id && x.Id != proposal.Id
            && (x.Status == VehicleProposalStatuses.Pending || x.Status == VehicleProposalStatuses.CounterOffered))
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.Status, VehicleProposalStatuses.Rejected).SetProperty(p => p.UpdatedAt, now), cancellationToken);

        // The shipment's price now reflects what was actually struck, not just the shipper's original ask.
        s.ProposedPrice = finalPrice; s.Status = "Booked"; s.Version++; s.UpdatedAt = now;
        availability.Status = VehicleAvailabilityStatuses.Matched; availability.Version++; availability.UpdatedAt = now;
        proposal.Status = VehicleProposalStatuses.Accepted; proposal.UpdatedAt = now; proposal.Version++;
        db.AuditEvents.Add(new AuditEvent { ActorUserId = actor.UserId, Action = "vehicle_proposal.accepted", TargetType = "VehicleProposal", TargetId = proposal.Id, Outcome = "Succeeded", OccurredAt = now });
        var counterpart = actor.UserId == proposal.DriverUserId ? proposal.BrokerUserId : proposal.DriverUserId;
        await NotificationEvents.RaiseAsync(db, clock, NotificationTypes.ProposalAccepted, counterpart, proposal.ShipmentId, actor.UserId, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return ShipmentResult<VehicleProposalResponse>.Fail("conflict"); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return ShipmentResult<VehicleProposalResponse>.Fail("resource_unavailable"); }

        return new(await MapAsync(proposal, cancellationToken));
    }
}
