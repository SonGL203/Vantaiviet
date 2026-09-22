using System.ComponentModel.DataAnnotations;
using VantaiViet.CoreApi.Entities;
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
    CoordinateDto? Pickup = null, CoordinateDto? Delivery = null,
    [param: StringLength(20)] string CargoType = "General",
    [param: StringLength(1000)] string? CargoDescription = null,
    [param: Range(1, ShipmentLimits.MaxQuantity)] int Quantity = 1,
    [param: StringLength(20)] string QuantityUnit = QuantityUnits.Package,
    [param: StringLength(20)] string VehicleType = RequiredVehicleTypes.Any,
    // Money travels as an exact decimal string, never a JSON number; the currency is always VND.
    [param: RegularExpression(ShipmentLimits.PricePattern)] string? ProposedPrice = null,
    DateTimeOffset? DeliverByAt = null,
    [param: StringLength(1000)] string? Notes = null,
    // Feature 21: a broker's commission on this deal, a percentage of ProposedPrice.
    [param: Range(typeof(decimal), "0", "100")] decimal? BrokerCommissionRate = null);
public sealed record UpdateShipmentRequest(
    [param: Required, StringLength(200)] string Title,
    [param: Required, StringLength(500)] string PickupAddress,
    [param: Required, StringLength(500)] string DeliveryAddress,
    [param: Range(typeof(decimal), "0.01", "9999999999.99")] decimal WeightKg,
    DateTimeOffset PickupAt,
    [param: Range(1, long.MaxValue)] long Version,
    CoordinateDto? Pickup = null, CoordinateDto? Delivery = null,
    // Omitting CargoType keeps the stored cargo; sending it replaces both type and description.
    [param: StringLength(20)] string? CargoType = null,
    [param: StringLength(1000)] string? CargoDescription = null,
    // Omitting Quantity keeps every stored detail below and the stored pickup/delivery points; sending it means the
    // client sends the whole state, so all of them are replaced together and a null price, deadline, note or point
    // clears it. QuantityUnit and VehicleType are then required.
    [param: Range(1, ShipmentLimits.MaxQuantity)] int? Quantity = null,
    [param: StringLength(20)] string? QuantityUnit = null,
    [param: StringLength(20)] string? VehicleType = null,
    [param: RegularExpression(ShipmentLimits.PricePattern)] string? ProposedPrice = null,
    DateTimeOffset? DeliverByAt = null,
    [param: StringLength(1000)] string? Notes = null,
    [param: Range(typeof(decimal), "0", "100")] decimal? BrokerCommissionRate = null);
/// <summary>How the owner's shipments stand, for the start page: one count per status, and how many people are waiting for an answer.</summary>
/// <summary><see cref="FoundVehicleRate"/> ("tỷ lệ tìm được xe") is the percentage, 0-100, of shipments ever sent out
/// (Published, Booked, Completed or Cancelled — everything except a Draft that was never offered to anyone) that
/// went on to find a vehicle (Booked or Completed). Null when nothing has ever been published yet.</summary>
public sealed record ShipmentSummaryResponse(int Draft, int Published, int Booked, int Completed, int Cancelled, int PendingRequests, double? FoundVehicleRate);
public sealed record ShipmentVersionRequest([param: Range(1, long.MaxValue)] long Version);
/// <summary>Reposting or copying starts a new shipment; the old times are in the past, so the shipper says when it happens now.
/// Leaving out DeliverByAt keeps the original span between pickup and delivery deadline.</summary>
public sealed record CloneShipmentRequest(DateTimeOffset PickupAt, DateTimeOffset? DeliverByAt = null);
public sealed record ShipmentResponse(Guid Id, string Title, string PickupAddress, string DeliveryAddress,
    decimal WeightKg, DateTimeOffset PickupAt, bool IsExternalOrder, string Status, long Version,
    CoordinateDto? Pickup = null, CoordinateDto? Delivery = null,
    string CargoType = "General", string? CargoDescription = null,
    int Quantity = 1, string QuantityUnit = QuantityUnits.Package, string VehicleType = RequiredVehicleTypes.Any,
    string? ProposedPrice = null, string Currency = ShipmentLimits.DefaultCurrency,
    DateTimeOffset? DeliverByAt = null, string? Notes = null, int PhotoCount = 0, DateTimeOffset CreatedAt = default,
    string Code = "", int PendingRequests = 0, decimal? BrokerCommissionRate = null);
/// <summary>Feature 24 ("Quản lý"): a route the owner has posted more than once, most-used first.</summary>
public sealed record FrequentRouteResponse(string PickupAddress, string DeliveryAddress, int Count, DateTimeOffset LastUsedAt);
/// <summary>What the signed-in viewer may do to a shipment right now; the app shows buttons from this, the server still re-checks.</summary>
public sealed record ShipmentActions(bool CanEdit, bool CanPublish, bool CanCancel, string? EditLockedReason,
    bool CanRepost = false, bool CanCopy = false)
{
    public static ShipmentActions None { get; } = new(false, false, false, null);
}
public sealed record ShipmentOwnerResponse(ShipmentResponse Shipment, string? ExternalCustomerName, string? ExternalCustomerPhone,
    IReadOnlyList<ShipmentPhotoInfo>? Photos = null, ShipmentActions? Actions = null);
