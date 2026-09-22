namespace VantaiViet.CoreApi.Entities;

/// <summary>What a broker tells shippers and drivers about themselves: their working area and how long they have been doing this.</summary>
public sealed class BrokerProfile
{
    public Guid UserId { get; set; }
    public required string OperatingArea { get; set; }
    public int YearsExperience { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
