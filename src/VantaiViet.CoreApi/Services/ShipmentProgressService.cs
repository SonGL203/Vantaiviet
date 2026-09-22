using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

/// <summary>The shipper's view of where a shipment stands: the order, and once someone is booked, the trip and its last known position.</summary>
public sealed class ShipmentProgressService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IShipmentProgressService
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(2);

    public async Task<ShipmentResult<ShipmentTrackingResponse>> TrackAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        if (!await db.Database.SqlQuery<bool>($"""SELECT public."CanDispatchShipment"({shipmentId}, {actor.UserId}) AS "Value" """).SingleAsync(cancellationToken))
        {
            return ShipmentResult<ShipmentTrackingResponse>.Fail("not_found");
        }

        var s = await db.Shipments.AsNoTracking().SingleAsync(x => x.Id == shipmentId, cancellationToken);
        var booked = await (from b in db.Bookings.AsNoTracking()
            join t in db.Trips on b.Id equals t.BookingId
            join u in db.Users on t.DriverUserId equals u.Id
            join v in db.Vehicles on t.VehicleId equals v.Id
            where b.ShipmentId == shipmentId
            select new { Trip = t, b.OwnerUserId, b.ConfirmedAt, u.DisplayName, u.PhoneNumber, v.LicensePlate, v.VehicleType })
            .SingleOrDefaultAsync(cancellationToken);

        ShipmentTrip? trip = null;
        if (booked is not null)
        {
            var t = booked.Trip;
            var latest = await db.TripLocations.AsNoTracking().Where(x => x.TripId == t.Id)
                .OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            trip = new ShipmentTrip(t.Id, t.Status, t.Version, booked.ConfirmedAt, t.StartedAt, t.PickedUpAt, t.DeliveredAt, t.CompletedAt, t.CancelledAt,
                new TrackingCarrier(t.DriverUserId, booked.DisplayName, booked.PhoneNumber), new TrackingVehicle(booked.LicensePlate, booked.VehicleType),
                latest is null ? null : new GpsPointResponse(latest.Id, latest.PointId, latest.Latitude, latest.Longitude, latest.AccuracyMeters, latest.RecordedAt, latest.ReceivedAt, latest.Simulated),
                latest is null || clock.GetUtcNow() - latest.RecordedAt > StaleAfter,
                t.Status == TripStates.Delivered && booked.OwnerUserId == actor.UserId);
        }

        return new(new ShipmentTrackingResponse(s.Id, s.Code, s.Status, s.Version, s.CreatedAt,
            s.PickupLatitude.HasValue ? new CoordinateDto(s.PickupLatitude.Value, s.PickupLongitude!.Value) : null,
            s.DeliveryLatitude.HasValue ? new CoordinateDto(s.DeliveryLatitude.Value, s.DeliveryLongitude!.Value) : null, trip));
    }
}
