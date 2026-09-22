namespace VantaiViet.CoreApi.DTOs;

public sealed record CarrierVehicleSummary(Guid Id, string LicensePlate, string VehicleType, decimal MaxPayloadKg);

/// <summary>Someone who asked for the shipment: a driver with the vehicle they offered, or the broker the shipper handed it to.</summary>
public sealed record ShipmentTaker(Guid UserId, string Kind, string DisplayName, string? Phone, string Status, Guid? RequestId,
    DateTimeOffset At, CarrierVehicleSummary? Vehicle, bool Verified, int CompletedTrips, RatingSummary? Rating = null);

public sealed record ShipmentTakersResponse(IReadOnlyList<ShipmentTaker> Takers, long ShipmentVersion);

public sealed record CarrierDriverInfo(string LicenseClass, bool LicenseValid, IReadOnlyList<CarrierVehicleSummary> Vehicles);

public sealed record CarrierProfileResponse(Guid UserId, string Kind, string DisplayName, string? Phone, bool Verified,
    DateTimeOffset MemberSince, int CompletedTrips, CarrierDriverInfo? Driver, RatingSummary? Rating = null,
    IReadOnlyList<RecentReview>? RecentReviews = null);
