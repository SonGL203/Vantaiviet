namespace VantaiViet.CoreApi.Entities;

public sealed class ShipmentPhoto
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public required string ContentType { get; set; }
    public int SizeBytes { get; set; }
    // Image bytes stay in the database so a shipment and its photos never disagree; move to object storage at scale.
    public required byte[] Content { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
