using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record CreateVehicleAvailabilityRequest(
    [param: Required] Guid VehicleId,
    [param: Required, StringLength(500)] string FromAddress,
    [param: Required, StringLength(500)] string ToAddress,
    DateTimeOffset AvailableFrom,
    [param: Required] DateTimeOffset ExpiresAt,
    [param: StringLength(1000)] string? Notes = null,
    bool IsReturnTrip = false);

public sealed record VehicleAvailabilityResponse(
    Guid Id, Guid DriverUserId, string DriverDisplayName, Guid VehicleId, string VehicleType, decimal MaxPayloadKg,
    string FromAddress, string ToAddress, DateTimeOffset AvailableFrom, DateTimeOffset ExpiresAt,
    string? Notes, bool IsReturnTrip, string Status, long Version, DateTimeOffset CreatedAt, string? BodyType = null);

public sealed record WithdrawVehicleAvailabilityRequest([param: Range(1, long.MaxValue)] long Version);
