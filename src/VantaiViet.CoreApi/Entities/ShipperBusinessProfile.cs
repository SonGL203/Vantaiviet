namespace VantaiViet.CoreApi.Entities;

public sealed class ShipperBusinessProfile
{
    public Guid UserId { get; set; }
    public required string CompanyName { get; set; }
    public required string TaxCode { get; set; }
    public required string Address { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
