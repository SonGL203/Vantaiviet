namespace VantaiViet.CoreApi.Entities;

public sealed class ShipmentBroker
{
    public Guid ShipmentId { get; set; }
    public Guid BrokerUserId { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
