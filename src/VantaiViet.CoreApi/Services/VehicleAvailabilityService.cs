using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

/// <summary>A driver's empty truck looking for cargo on a route, and the broker's search over what is posted and still open.</summary>
public sealed class VehicleAvailabilityService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IVehicleAvailabilityService
{
    // Keeps the search list current: nobody posts a truck that is "free" ten months from now.
    private static readonly TimeSpan MaxWindow = TimeSpan.FromDays(30);
    private const int MaxPageSize = 100;

    private async Task<bool> EligibleAsync(Guid driverUserId, Guid vehicleId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return await (from u in db.Users
            join ur in db.UserRoles on u.Id equals ur.UserId
            join role in db.Roles on ur.RoleId equals role.Id
            join p in db.DriverProfiles on u.Id equals p.UserId
            join v in db.Vehicles on u.Id equals v.DriverUserId
            where u.Id == driverUserId && u.AccountStatus == "Active" && role.NormalizedName == "DRIVER"
                && p.Status == "Approved" && p.LicenseExpiresOn >= today
                && v.Id == vehicleId && v.Status == "Approved"
            select u.Id).AnyAsync(cancellationToken);
    }

    private static VehicleAvailabilityResponse Map(VehicleAvailability a, string driverName, string vehicleType, decimal maxPayloadKg, string? bodyType)
    {
        return new(a.Id, a.DriverUserId, driverName, a.VehicleId, vehicleType, maxPayloadKg,
            a.FromAddress, a.ToAddress, a.AvailableFrom, a.ExpiresAt, a.Notes, a.IsReturnTrip, a.Status, a.Version, a.CreatedAt, bodyType);
    }

    /// <summary>Orders, pages and joins at the database, and only builds the response record once the rows are in memory
    /// (the same shape as <c>ShipmentService.ListAsync</c>) — projecting the record inside the query itself does not translate.
    /// <paramref name="prioritizeReturnTrip"/> puts feature-12 postings first, for the broker's search only.</summary>
    private async Task<List<VehicleAvailabilityResponse>> PageAsync(
        IQueryable<VehicleAvailability> query, string? vehicleType, decimal? minPayloadKg, string? bodyType, bool prioritizeReturnTrip, int skip, int take, CancellationToken cancellationToken)
    {
        var joined = from a in query
            join u in db.Users on a.DriverUserId equals u.Id
            join v in db.Vehicles on a.VehicleId equals v.Id
            select new { a, u.DisplayName, v.VehicleType, v.MaxPayloadKg, v.BodyType };
        if (!string.IsNullOrWhiteSpace(vehicleType))
        {
            joined = joined.Where(x => x.VehicleType == vehicleType);
        }

        if (minPayloadKg is { } minimum)
        {
            joined = joined.Where(x => x.MaxPayloadKg >= minimum);
        }

        if (!string.IsNullOrWhiteSpace(bodyType))
        {
            joined = joined.Where(x => x.BodyType == bodyType);
        }

        var ordered = prioritizeReturnTrip
            ? joined.OrderByDescending(x => x.a.IsReturnTrip).ThenByDescending(x => x.a.CreatedAt).ThenByDescending(x => x.a.Id)
            : joined.OrderByDescending(x => x.a.CreatedAt).ThenByDescending(x => x.a.Id);
        var rows = await ordered.Skip(skip).Take(take).ToListAsync(cancellationToken);
        return rows.Select(x => Map(x.a, x.DisplayName, x.VehicleType, x.MaxPayloadKg, x.BodyType)).ToList();
    }

    public async Task<ShipmentResult<VehicleAvailabilityResponse>> PostAsync(CreateVehicleAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var from = request.FromAddress.Trim();
        var to = request.ToAddress.Trim();
        if (from.Length == 0 || to.Length == 0)
        {
            return ShipmentResult<VehicleAvailabilityResponse>.Fail("invalid_details");
        }

        var now = clock.GetUtcNow();
        var availableFrom = request.AvailableFrom.ToUniversalTime();
        var expiresAt = request.ExpiresAt.ToUniversalTime();
        if (expiresAt <= now || expiresAt <= availableFrom || expiresAt > now.Add(MaxWindow))
        {
            return ShipmentResult<VehicleAvailabilityResponse>.Fail("invalid_window");
        }

        if (!await EligibleAsync(actor.UserId, request.VehicleId, cancellationToken))
        {
            return ShipmentResult<VehicleAvailabilityResponse>.Fail("driver_not_eligible");
        }

        var vehicle = await db.Vehicles.AsNoTracking().Where(x => x.Id == request.VehicleId)
            .Select(x => new { x.VehicleType, x.MaxPayloadKg, x.BodyType }).SingleAsync(cancellationToken);

        var availability = new VehicleAvailability
        {
            Id = Guid.NewGuid(),
            DriverUserId = actor.UserId,
            VehicleId = request.VehicleId,
            FromAddress = from,
            ToAddress = to,
            AvailableFrom = availableFrom,
            ExpiresAt = expiresAt,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            IsReturnTrip = request.IsReturnTrip,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.VehicleAvailabilities.Add(availability);
        db.AuditEvents.Add(new AuditEvent { ActorUserId = actor.UserId, Action = "vehicle_availability.posted", TargetType = "VehicleAvailability", TargetId = availability.Id, Outcome = "Succeeded", OccurredAt = now });
        // Feature 25 ("Có xe phù hợp"): every broker who has this driver saved learns their truck is free, not tied to any one shipment.
        var interestedBrokers = await db.SavedDrivers.AsNoTracking().Where(x => x.DriverUserId == actor.UserId).Select(x => x.BrokerUserId).Distinct().ToListAsync(cancellationToken);
        var route = $"{from} → {to}";
        foreach (var brokerUserId in interestedBrokers)
        {
            await NotificationEvents.RaiseAsync(db, clock, NotificationTypes.SavedDriverAvailable, brokerUserId, null, actor.UserId, cancellationToken, subjectOverride: route);
        }

        await NotifyMatchingShipmentOwnersAsync(availability, vehicle.VehicleType, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var driverName = await db.Users.AsNoTracking().Where(x => x.Id == actor.UserId).Select(x => x.DisplayName).SingleAsync(cancellationToken);
        return new(Map(availability, driverName, vehicle.VehicleType, vehicle.MaxPayloadKg, vehicle.BodyType));
    }

    /// <summary>"Có xe phù hợp": every broker with a Published shipment whose pickup/delivery address matches this
    /// truck's route (either address containing the other, since both sides write free-text addresses of different
    /// granularity) and whose required vehicle type is "Any" or the same as this truck's hears about it, not only
    /// brokers who happen to have this specific driver saved. Unbatched, in memory, same tradeoff as
    /// <c>ShipmentService.NotifyMatchingRouteInterestsAsync</c> — the set of currently-Published shipments is small.</summary>
    private async Task NotifyMatchingShipmentOwnersAsync(VehicleAvailability availability, string vehicleType, CancellationToken cancellationToken)
    {
        var published = await db.Shipments.AsNoTracking().Where(x => x.Status == ShipmentStatuses.Published)
            .Select(x => new { x.Id, x.OwnerUserId, x.PickupAddress, x.DeliveryAddress, x.VehicleType }).ToListAsync(cancellationToken);
        var matches = published.Where(x =>
            (x.PickupAddress.Contains(availability.FromAddress, StringComparison.OrdinalIgnoreCase) || availability.FromAddress.Contains(x.PickupAddress, StringComparison.OrdinalIgnoreCase))
            && (x.DeliveryAddress.Contains(availability.ToAddress, StringComparison.OrdinalIgnoreCase) || availability.ToAddress.Contains(x.DeliveryAddress, StringComparison.OrdinalIgnoreCase))
            && (x.VehicleType == RequiredVehicleTypes.Any || x.VehicleType == vehicleType)).ToList();
        if (matches.Count == 0)
        {
            return;
        }

        var ownerIds = matches.Select(x => x.OwnerUserId).Distinct().ToList();
        var brokerOwnerIds = await (from u in db.Users join ur in db.UserRoles on u.Id equals ur.UserId join r in db.Roles on ur.RoleId equals r.Id
            where ownerIds.Contains(u.Id) && r.NormalizedName == "BROKER" select u.Id).ToListAsync(cancellationToken);
        foreach (var match in matches)
        {
            if (brokerOwnerIds.Contains(match.OwnerUserId))
            {
                await NotificationEvents.RaiseAsync(db, clock, NotificationTypes.MatchingVehicleAvailable, match.OwnerUserId, match.Id, actor.UserId, cancellationToken);
            }
        }
    }

    public async Task<ShipmentResult<IReadOnlyList<VehicleAvailabilityResponse>>> MineAsync(CancellationToken cancellationToken)
    {
        var mine = await PageAsync(db.VehicleAvailabilities.AsNoTracking().Where(x => x.DriverUserId == actor.UserId), null, null, null, false, 0, 50, cancellationToken);
        return new(mine);
    }

    public async Task<ShipmentResult<bool>> WithdrawAsync(Guid id, long version, CancellationToken cancellationToken)
    {
        var availability = await db.VehicleAvailabilities.SingleOrDefaultAsync(x => x.Id == id && x.DriverUserId == actor.UserId, cancellationToken);
        if (availability is null)
        {
            return ShipmentResult<bool>.Fail("not_found");
        }

        if (availability.Status != VehicleAvailabilityStatuses.Open)
        {
            return ShipmentResult<bool>.Fail("conflict");
        }

        if (availability.Version != version)
        {
            return ShipmentResult<bool>.Fail("conflict");
        }

        availability.Status = VehicleAvailabilityStatuses.Withdrawn;
        availability.UpdatedAt = clock.GetUtcNow();
        availability.Version++;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return ShipmentResult<bool>.Fail("conflict");
        }

        return new(true);
    }

    public async Task<ShipmentResult<IReadOnlyList<VehicleAvailabilityResponse>>> SearchAsync(
        string? fromArea, string? toArea, string? vehicleType, decimal? minPayloadKg, string? bodyType, bool returnTripOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize)
        {
            return ShipmentResult<IReadOnlyList<VehicleAvailabilityResponse>>.Fail("invalid_pagination");
        }

        if (minPayloadKg is < 0)
        {
            return ShipmentResult<IReadOnlyList<VehicleAvailabilityResponse>>.Fail("invalid_details");
        }

        var now = clock.GetUtcNow();
        var query = db.VehicleAvailabilities.AsNoTracking().Where(x => x.Status == VehicleAvailabilityStatuses.Open && x.ExpiresAt > now);
        if (!string.IsNullOrWhiteSpace(fromArea))
        {
            query = query.Where(x => EF.Functions.ILike(x.FromAddress, "%" + fromArea.Trim() + "%"));
        }

        if (!string.IsNullOrWhiteSpace(toArea))
        {
            query = query.Where(x => EF.Functions.ILike(x.ToAddress, "%" + toArea.Trim() + "%"));
        }

        if (returnTripOnly)
        {
            query = query.Where(x => x.IsReturnTrip);
        }

        var results = await PageAsync(query, vehicleType, minPayloadKg, bodyType, true, (page - 1) * pageSize, pageSize, cancellationToken);
        return new(results);
    }
}
