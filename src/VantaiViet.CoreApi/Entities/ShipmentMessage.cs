namespace VantaiViet.CoreApi.Entities;

/// <summary>One chat message. A conversation is the shipment owner talking with one carrier (a driver or broker who took or asked for the job).</summary>
public sealed class ShipmentMessage
{
    public long Id { get; set; }
    public Guid ShipmentId { get; set; }
    public Guid CarrierUserId { get; set; }
    public Guid SenderUserId { get; set; }
    public required string Body { get; set; }
    public DateTimeOffset SentAt { get; set; }
}
