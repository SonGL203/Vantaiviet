using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

/// <summary>Who asked for a shipment (drivers with their vehicle, the broker it was handed to) and what they look like, for the person deciding.</summary>
public sealed class ShipmentCarrierService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IShipmentCarrierService
{
    private const string DriverKind = "Driver";
    private const string BrokerKind = "Broker";
    private const string PendingStatus = "Pending";
    private const string AcceptedStatus = "Accepted";
    private const int MaxTakers = 100;

    /// <summary>A number is shared only once the two have been connected: the request or offer has been accepted. Before that they can write.</summary>
    private static string? PhoneOnceConnected(string? phone, string status)
    {
        return status == AcceptedStatus ? phone : null;
    }

    private Task<bool> CanManageAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        return db.Database.SqlQuery<bool>($"""SELECT public."CanDispatchShipment"({shipmentId}, {actor.UserId}) AS "Value" """).SingleAsync(cancellationToken);
    }

    public async Task<ShipmentResult<ShipmentTakersResponse>> TakersAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(shipmentId, cancellationToken))
        {
            return ShipmentResult<ShipmentTakersResponse>.Fail("not_found");
        }

        var version = await db.Shipments.AsNoTracking().Where(x => x.Id == shipmentId).Select(x => x.Version).SingleAsync(cancellationToken);
        var drivers = await (from r in db.TransportRequests.AsNoTracking()
            join u in db.Users on r.DriverUserId equals u.Id
            join v in db.Vehicles on r.VehicleId equals v.Id
            where r.ShipmentId == shipmentId
            select new { r.Id, UserId = u.Id, u.DisplayName, u.PhoneNumber, u.RegistrationApplicationId, r.Status, At = r.CreatedAt,
                Vehicle = new CarrierVehicleSummary(v.Id, v.LicensePlate, v.VehicleType, v.MaxPayloadKg) })
            .Take(MaxTakers).ToListAsync(cancellationToken);
        var brokers = await (from a in db.Set<ShipmentBroker>().AsNoTracking()
            join u in db.Users on a.BrokerUserId equals u.Id
            where a.ShipmentId == shipmentId
            select new { UserId = u.Id, u.DisplayName, u.PhoneNumber, u.RegistrationApplicationId, a.Status, At = a.UpdatedAt })
            .ToListAsync(cancellationToken);

        var userIds = drivers.Select(x => x.UserId).Concat(brokers.Select(x => x.UserId)).Distinct().ToArray();
        var registrations = drivers.Select(x => x.RegistrationApplicationId).Concat(brokers.Select(x => x.RegistrationApplicationId)).Distinct().ToArray();
        var verified = (await db.KycApplications.AsNoTracking().Where(x => registrations.Contains(x.RegistrationApplicationId) && x.Status == "Approved")
            .Select(x => x.RegistrationApplicationId).ToListAsync(cancellationToken)).ToHashSet();
        var completed = await CompletedTripsAsync(userIds, cancellationToken);
        var ratings = await RatingsAsync(userIds, cancellationToken);

        var takers = new List<ShipmentTaker>();
        foreach (var d in drivers)
        {
            takers.Add(new ShipmentTaker(d.UserId, DriverKind, d.DisplayName, PhoneOnceConnected(d.PhoneNumber, d.Status), d.Status, d.Id, d.At, d.Vehicle,
                verified.Contains(d.RegistrationApplicationId), completed.GetValueOrDefault(d.UserId), ratings.GetValueOrDefault(d.UserId)));
        }

        foreach (var b in brokers)
        {
            takers.Add(new ShipmentTaker(b.UserId, BrokerKind, b.DisplayName, PhoneOnceConnected(b.PhoneNumber, b.Status), b.Status, null, b.At, null,
                verified.Contains(b.RegistrationApplicationId), completed.GetValueOrDefault(b.UserId), ratings.GetValueOrDefault(b.UserId)));
        }

        // Whoever still needs an answer comes first, then the newest.
        var ordered = takers.OrderBy(x => x.Status is PendingStatus or AcceptedStatus ? 0 : 1).ThenByDescending(x => x.At).ToList();
        return new(new ShipmentTakersResponse(ordered, version));
    }

    public async Task<ShipmentResult<CarrierProfileResponse>> ProfileAsync(Guid shipmentId, Guid carrierUserId, CancellationToken cancellationToken)
    {
        // Only someone who actually asked for this shipment can be looked at through it, so the endpoint cannot be used to browse users.
        var requestStatus = await db.TransportRequests.Where(x => x.ShipmentId == shipmentId && x.DriverUserId == carrierUserId)
            .Select(x => x.Status).SingleOrDefaultAsync(cancellationToken);
        var offerStatus = requestStatus is null
            ? await db.Set<ShipmentBroker>().Where(x => x.ShipmentId == shipmentId && x.BrokerUserId == carrierUserId).Select(x => x.Status).SingleOrDefaultAsync(cancellationToken)
            : null;
        var isDriver = requestStatus is not null;
        if ((requestStatus is null && offerStatus is null) || !await CanManageAsync(shipmentId, cancellationToken))
        {
            return ShipmentResult<CarrierProfileResponse>.Fail("not_found");
        }

        var user = await db.Users.AsNoTracking().Where(x => x.Id == carrierUserId)
            .Select(x => new { x.DisplayName, x.PhoneNumber, x.CreatedAt, x.RegistrationApplicationId }).SingleAsync(cancellationToken);
        var verified = await db.KycApplications.AsNoTracking().AnyAsync(x => x.RegistrationApplicationId == user.RegistrationApplicationId && x.Status == "Approved", cancellationToken);
        var completed = await CompletedTripsAsync([carrierUserId], cancellationToken);
        var rating = (await RatingsAsync([carrierUserId], cancellationToken)).GetValueOrDefault(carrierUserId);
        var recent = await db.TripReviews.AsNoTracking().Where(x => x.TargetUserId == carrierUserId)
            .OrderByDescending(x => x.CreatedAt).Take(FollowUpLimits.RecentReviews)
            .Select(x => new RecentReview(x.Stars, x.Comment, x.CreatedAt)).ToListAsync(cancellationToken);

        CarrierDriverInfo? driver = null;
        var license = await db.DriverProfiles.AsNoTracking().Where(x => x.UserId == carrierUserId)
            .Select(x => new { x.LicenseClass, x.LicenseExpiresOn, x.Status }).SingleOrDefaultAsync(cancellationToken);
        if (license is not null)
        {
            var vehicles = await db.Vehicles.AsNoTracking().Where(x => x.DriverUserId == carrierUserId && x.Status == "Approved")
                .OrderBy(x => x.LicensePlate).Select(x => new CarrierVehicleSummary(x.Id, x.LicensePlate, x.VehicleType, x.MaxPayloadKg)).ToListAsync(cancellationToken);
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            driver = new CarrierDriverInfo(license.LicenseClass, license.Status == "Approved" && license.LicenseExpiresOn >= today, vehicles);
        }

        return new(new CarrierProfileResponse(carrierUserId, isDriver ? DriverKind : BrokerKind, user.DisplayName, PhoneOnceConnected(user.PhoneNumber, requestStatus ?? offerStatus!), verified,
            user.CreatedAt, completed.GetValueOrDefault(carrierUserId), driver, rating, recent));
    }

    /// <summary>What shippers have rated each person: the mean of their stars (one decimal) and how many ratings it rests on.</summary>
    private async Task<Dictionary<Guid, RatingSummary>> RatingsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var rows = await db.TripReviews.AsNoTracking().Where(x => userIds.Contains(x.TargetUserId)).GroupBy(x => x.TargetUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count(), Average = g.Average(x => x.Stars) }).ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.UserId, x => new RatingSummary(Math.Round(x.Average, 1), x.Count));
    }

    /// <summary>Trips a person finished as the driver, or was trusted with as a broker; the number a shipper reads as "has done this before".</summary>
    private async Task<Dictionary<Guid, int>> CompletedTripsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var asDriver = await db.Trips.AsNoTracking().Where(x => x.Status == TripStates.Completed && userIds.Contains(x.DriverUserId))
            .GroupBy(x => x.DriverUserId).Select(g => new { UserId = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var asBroker = await (from p in db.TripParticipants.AsNoTracking() join t in db.Trips on p.TripId equals t.Id
            where t.Status == TripStates.Completed && userIds.Contains(p.UserId) && t.DriverUserId != p.UserId
            group t by p.UserId into g select new { UserId = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var totals = new Dictionary<Guid, int>();
        foreach (var row in asDriver.Concat(asBroker))
        {
            totals[row.UserId] = totals.GetValueOrDefault(row.UserId) + row.Count;
        }

        return totals;
    }
}
