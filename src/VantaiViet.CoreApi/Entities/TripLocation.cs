namespace VantaiViet.CoreApi.Entities;
public sealed class TripLocation
{
    public long Id { get; set; }
    public Guid TripId { get; set; }
    public Guid PointId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double AccuracyMeters { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public bool Simulated { get; set; }
}
