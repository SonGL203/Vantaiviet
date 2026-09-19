namespace VantaiViet.CoreApi.Entities;

public sealed class RegistrationApplication
{
    public Guid Id { get; set; }
    public required string PhoneNumber { get; set; }
    public DateTimeOffset? PhoneVerifiedAt { get; set; }
    public string? DisplayName { get; set; }
    public string Status { get; set; } = "InProgress";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
