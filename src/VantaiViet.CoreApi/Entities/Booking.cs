namespace VantaiViet.CoreApi.Entities;
public sealed class Booking
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public Guid RequestId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid DriverUserId { get; set; }
    public Guid VehicleId { get; set; }
    public DateTimeOffset ConfirmedAt { get; set; }
    public string Status { get; set; } = "Confirmed";
}
