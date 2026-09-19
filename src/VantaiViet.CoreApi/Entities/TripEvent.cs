namespace VantaiViet.CoreApi.Entities;
public sealed class TripEvent
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Guid ActorUserId { get; set; }
    public required string Action { get; set; }
    public string? Note { get; set; }
    public Guid? ProofId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
