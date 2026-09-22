namespace VantaiViet.CoreApi.Entities;

/// <summary>A route a broker watches for new cargo, "Có hàng phù hợp": any newly published shipment whose pickup and
/// delivery addresses contain these two areas notifies them, the same free-text partial match already used to
/// search vehicle availability.</summary>
public sealed class BrokerRouteInterest
{
    public Guid Id { get; set; }
    public Guid BrokerUserId { get; set; }
    public required string FromArea { get; set; }
    public required string ToArea { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
