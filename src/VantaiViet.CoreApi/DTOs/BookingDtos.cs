namespace VantaiViet.CoreApi.DTOs;
public sealed record RequestTransportDto(Guid VehicleId);
public sealed record TransportRequestResponse(Guid Id, Guid ShipmentId, Guid DriverUserId, Guid VehicleId, string Status);
public sealed record BookingResponse(Guid Id, Guid ShipmentId, Guid DriverUserId, Guid VehicleId, Guid TripId, string TripStatus, DateTimeOffset ConfirmedAt);
