namespace VantaiViet.CoreApi.Entities;

public sealed class AuditEvent
{
    public long Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorReference { get; set; }
    public required string Action { get; set; }
    public required string TargetType { get; set; }
    public Guid TargetId { get; set; }
    public required string Outcome { get; set; }
    public string? TraceId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
