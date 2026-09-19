namespace VantaiViet.CoreApi.Entities;
public sealed class Trip
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid DriverUserId { get; set; }
    public Guid VehicleId { get; set; }
    public string Status { get; set; } = "Assigned";
    public DateTimeOffset CreatedAt { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? PickedUpAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}
