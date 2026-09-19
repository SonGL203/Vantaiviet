namespace VantaiViet.CoreApi.Entities;

// Durable verification request and its single outbound delivery share one transaction.
public sealed class OtpDelivery
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RequestKey { get; set; }
    public string Purpose { get; set; } = "Verification";
    public string? CredentialStamp { get; set; }
    public required string Channel { get; set; }
    public required string Destination { get; set; }
    public required string ProtectedCode { get; set; }
    public bool Simulated { get; set; }
    public string State { get; set; } = "Pending";
    public int SendAttempts { get; set; }
    public int VerifyAttempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}
