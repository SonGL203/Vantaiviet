namespace VantaiViet.CoreApi.Entities;

/// <summary>A driver a broker works with regularly, features 14-16: "Danh sách tài xế", "Tài xế yêu thích", and their group.</summary>
public sealed class SavedDriver
{
    public Guid Id { get; set; }
    public Guid BrokerUserId { get; set; }
    public Guid DriverUserId { get; set; }
    public Guid? GroupId { get; set; }
    public bool IsFavorite { get; set; }
    public string? Note { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
