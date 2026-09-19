namespace VantaiViet.CoreApi.Entities;
public sealed class TransportRequest
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public Guid DriverUserId { get; set; }
    public Guid VehicleId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; }
}
