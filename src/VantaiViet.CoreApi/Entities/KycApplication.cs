namespace VantaiViet.CoreApi.Entities;

public sealed class KycApplication
{
    public Guid Id { get; set; }
    public Guid RegistrationApplicationId { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewerUserId { get; set; }
    public string? ReviewerReference { get; set; }
    public string? RejectionCode { get; set; }
    public string? ReviewNote { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
