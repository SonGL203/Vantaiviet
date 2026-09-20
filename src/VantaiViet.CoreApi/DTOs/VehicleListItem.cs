namespace VantaiViet.CoreApi.DTOs;

/// <summary>One row of the vehicle review queue.</summary>
public sealed record VehicleListItem(
    Guid Id,
    Guid DriverUserId,
    string? DriverDisplayName,
    string? DriverPhoneNumber,
    string LicensePlate,
    string VehicleType,
    decimal MaxPayloadKg,
    string Status,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote,
    DateTimeOffset CreatedAt);
