namespace VantaiViet.CoreApi.Entities;
public sealed class TripParticipant
{
    public Guid TripId { get; set; }
    public Guid UserId { get; set; }
    public Guid GrantedByUserId { get; set; }
    public DateTimeOffset GrantedAt { get; set; }
}
