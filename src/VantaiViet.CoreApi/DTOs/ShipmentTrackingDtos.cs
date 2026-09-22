namespace VantaiViet.CoreApi.DTOs;

public sealed record TrackingCarrier(Guid UserId, string DisplayName, string? Phone);

public sealed record TrackingVehicle(string LicensePlate, string VehicleType);

public sealed record ShipmentTrip(Guid TripId, string Status, long Version, DateTimeOffset BookedAt, DateTimeOffset? StartedAt,
    DateTimeOffset? PickedUpAt, DateTimeOffset? DeliveredAt, DateTimeOffset? CompletedAt, DateTimeOffset? CancelledAt,
    TrackingCarrier Driver, TrackingVehicle Vehicle, GpsPointResponse? Latest, bool Stale, bool CanConfirmComplete);

/// <summary>Where a shipment stands for its owner: the order itself, and once someone is booked, the trip with its timestamps and last position.</summary>
public sealed record ShipmentTrackingResponse(Guid ShipmentId, string Code, string Status, long ShipmentVersion, DateTimeOffset CreatedAt,
    CoordinateDto? Pickup, CoordinateDto? Delivery, ShipmentTrip? Trip);
