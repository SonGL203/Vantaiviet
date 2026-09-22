namespace VantaiViet.CoreApi.Entities;

/// <summary>Feature 23 ("Báo cáo tài xế"): a broker flags a driver for admin attention. No moderation workflow yet;
/// this is just the record, visible to the reporter themselves — an admin queue is a later batch.</summary>
public sealed class DriverReport
{
    public Guid Id { get; set; }
    public Guid ReporterUserId { get; set; }
    public Guid DriverUserId { get; set; }
    public required string Category { get; set; }
    public required string Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public static class DriverReportCategories
{
    public const string NoShow = "NoShow";
    public const string Misconduct = "Misconduct";
    public const string UnsafeDriving = "UnsafeDriving";
    public const string Other = "Other";
    public static readonly string[] All = [NoShow, Misconduct, UnsafeDriving, Other];
}
