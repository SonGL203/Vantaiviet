namespace VantaiViet.CoreApi.Entities;

public sealed class Shipment
{
    public Guid Id { get; set; }
    // The short reference people quote on the phone ("VT0000123"); the database hands it out from a sequence.
    public string Code { get; set; } = string.Empty;
    public Guid OwnerUserId { get; set; }
    public required string Title { get; set; }
    public required string PickupAddress { get; set; }
    public required string DeliveryAddress { get; set; }
    public decimal WeightKg { get; set; }
    public DateTimeOffset PickupAt { get; set; }
    public bool IsExternalOrder { get; set; }
    public string? ExternalCustomerName { get; set; }
    public string? ExternalCustomerPhone { get; set; }
    public string CargoType { get; set; } = "General";
    public string? CargoDescription { get; set; }
    public int Quantity { get; set; } = 1;
    public string QuantityUnit { get; set; } = QuantityUnits.Package;
    public string VehicleType { get; set; } = RequiredVehicleTypes.Any;
    // What the shipper hopes to pay for the whole shipment, in whole dong; null means "open to offers".
    public decimal? ProposedPrice { get; set; }
    public string Currency { get; set; } = ShipmentLimits.DefaultCurrency;
    // Feature 21 ("Hoa hồng"): a broker's commission on this deal, a percentage of ProposedPrice; null when not set
    // or not a broker's own order. Snapshotted here rather than recomputed from a rate that may since have changed.
    public decimal? BrokerCommissionRate { get; set; }
    public DateTimeOffset? DeliverByAt { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Draft";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public double? PickupLatitude { get; set; }
    public double? PickupLongitude { get; set; }
    public double? DeliveryLatitude { get; set; }
    public double? DeliveryLongitude { get; set; }
}
