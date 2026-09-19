namespace VantaiViet.CoreApi.Entities;

public sealed class DriverProfile
{
    public Guid UserId { get; set; }
    public required string LicenseClass { get; set; }
    public DateOnly LicenseExpiresOn { get; set; }
    public string Status { get; set; } = "Submitted";
    public Guid? ReviewerUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
