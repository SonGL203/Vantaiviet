namespace VantaiViet.CoreApi.DTOs;

public sealed record VehicleResponse(
    Guid Id, string LicensePlate, string VehicleType, decimal MaxPayloadKg, string Status, string? ReviewNote);
