using System.ComponentModel.DataAnnotations;
namespace VantaiViet.CoreApi.DTOs;
public sealed record CreateShipmentRequest(
    [param: Required, StringLength(200)] string Title,
    [param: Required, StringLength(500)] string PickupAddress,
    [param: Required, StringLength(500)] string DeliveryAddress,
    [param: Range(typeof(decimal), "0.01", "9999999999.99")] decimal WeightKg,
    DateTimeOffset PickupAt,
    bool IsExternalOrder = false,
    [param: StringLength(150)] string? ExternalCustomerName = null,
    [param: RegularExpression(@"^\+[1-9][0-9]{7,14}$")] string? ExternalCustomerPhone = null,
    CoordinateDto? Pickup = null, CoordinateDto? Delivery = null);
public sealed record UpdateShipmentRequest(
    [param: Required, StringLength(200)] string Title,
    [param: Required, StringLength(500)] string PickupAddress,
    [param: Required, StringLength(500)] string DeliveryAddress,
    [param: Range(typeof(decimal), "0.01", "9999999999.99")] decimal WeightKg,
    DateTimeOffset PickupAt,
    [param: Range(1, long.MaxValue)] long Version,
    CoordinateDto? Pickup = null, CoordinateDto? Delivery = null);
public sealed record ShipmentVersionRequest([param: Range(1, long.MaxValue)] long Version);
public sealed record ShipmentResponse(Guid Id, string Title, string PickupAddress, string DeliveryAddress,
    decimal WeightKg, DateTimeOffset PickupAt, bool IsExternalOrder, string Status, long Version,
    CoordinateDto? Pickup = null, CoordinateDto? Delivery = null);
public sealed record ShipmentOwnerResponse(ShipmentResponse Shipment, string? ExternalCustomerName, string? ExternalCustomerPhone);
