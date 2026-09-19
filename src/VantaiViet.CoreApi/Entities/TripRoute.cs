namespace VantaiViet.CoreApi.Entities;

public sealed class TripRoute
{
    public Guid TripId { get; set; }
    public required string InputHash { get; set; }
    public Guid LeaseId { get; set; }
    public DateTimeOffset LeaseUntil { get; set; }
    public string? ResponseJson { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
