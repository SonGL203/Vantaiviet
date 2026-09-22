using System.ComponentModel.DataAnnotations;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.DTOs;

/// <summary>Feature 13 + 18: the broker offers a price on a driver's open vehicle-availability posting.</summary>
public sealed record CreateVehicleProposalRequest(
    [param: Required] Guid VehicleAvailabilityId,
    [param: Required, RegularExpression(ShipmentLimits.PricePattern)] string ProposedPrice);

/// <summary>Feature 19: the driver's one counter-offer.</summary>
public sealed record CounterVehicleProposalRequest(
    [param: Required, RegularExpression(ShipmentLimits.PricePattern)] string CounterPrice,
    [param: Range(1, long.MaxValue)] long Version);

public sealed record VehicleProposalVersionRequest([param: Range(1, long.MaxValue)] long Version);

public sealed record VehicleProposalResponse(
    Guid Id, Guid ShipmentId, string ShipmentTitle, string ShipmentCode,
    Guid VehicleAvailabilityId, Guid DriverUserId, string DriverDisplayName, Guid VehicleId, string VehicleType,
    string ProposedPrice, string? CounterPrice, string Currency, string Status, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
