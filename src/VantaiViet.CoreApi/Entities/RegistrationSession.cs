namespace VantaiViet.CoreApi.Entities;

public sealed class RegistrationSession
{
    public Guid Id { get; set; }
    public Guid RegistrationApplicationId { get; set; }
    public required byte[] TokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
