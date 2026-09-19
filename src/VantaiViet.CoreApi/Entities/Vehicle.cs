namespace VantaiViet.CoreApi.Entities;

public sealed class Vehicle
{
    public Guid Id { get; set; }
    public Guid DriverUserId { get; set; }
    public required string LicensePlate { get; set; }
    public required string VehicleType { get; set; }
    public decimal MaxPayloadKg { get; set; }
    public string Status { get; set; } = "Submitted";
    public Guid? ReviewerUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
