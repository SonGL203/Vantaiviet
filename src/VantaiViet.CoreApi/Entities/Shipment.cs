namespace VantaiViet.CoreApi.Entities;

public sealed class Shipment
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string Title { get; set; }
    public required string PickupAddress { get; set; }
    public required string DeliveryAddress { get; set; }
    public decimal WeightKg { get; set; }
    public DateTimeOffset PickupAt { get; set; }
    public bool IsExternalOrder { get; set; }
    public string? ExternalCustomerName { get; set; }
    public string? ExternalCustomerPhone { get; set; }
    public string Status { get; set; } = "Draft";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public double? PickupLatitude { get; set; }
    public double? PickupLongitude { get; set; }
    public double? DeliveryLatitude { get; set; }
    public double? DeliveryLongitude { get; set; }
}
