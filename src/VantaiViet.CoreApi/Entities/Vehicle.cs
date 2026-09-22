namespace VantaiViet.CoreApi.Entities;

public sealed class Vehicle
{
    public Guid Id { get; set; }
    public Guid DriverUserId { get; set; }
    public required string LicensePlate { get; set; }
    public required string VehicleType { get; set; }
    public decimal MaxPayloadKg { get; set; }

    /// <summary>"Loại thùng" — the cargo box itself (kín/bạt/lửng...), a separate axis from <see cref="VehicleType"/>'s
    /// size/specialty. Free text like <see cref="VehicleType"/>, not enforced server-side; a driver may leave it blank.</summary>
    public string? BodyType { get; set; }
    public string Status { get; set; } = "Submitted";
    public Guid? ReviewerUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
