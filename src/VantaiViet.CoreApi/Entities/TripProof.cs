namespace VantaiViet.CoreApi.Entities;
public sealed class TripProof
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Guid UploaderUserId { get; set; }
    public required string ContentType { get; set; }
    public required byte[] Content { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
