namespace VantaiViet.CoreApi.Entities;

/// <summary>A broker's own named group of drivers, feature 16: "nhóm container Hải Phòng" and the like.</summary>
public sealed class DriverGroup
{
    public Guid Id { get; set; }
    public Guid BrokerUserId { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
