namespace VantaiViet.CoreApi.DTOs;

public sealed record AuditEventResponse(
    long Id, Guid? ActorUserId, string Action, string TargetType, Guid TargetId,
    string Outcome, DateTimeOffset OccurredAt);
