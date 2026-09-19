namespace VantaiViet.CoreApi.Entities;

public sealed class VerificationChallenge
{
    public Guid Id { get; set; }
    public Guid? RegistrationApplicationId { get; set; }
    public Guid? UserId { get; set; }
    public required string Purpose { get; set; }
    public required string Destination { get; set; }
    public required byte[] CodeHmac { get; set; }
    public required string HmacKeyId { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
