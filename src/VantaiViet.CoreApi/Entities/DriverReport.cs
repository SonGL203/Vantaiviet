namespace VantaiViet.CoreApi.Entities;

/// <summary>Feature 23 ("Báo cáo tài xế"): a broker flags a driver for admin attention. Batch 6 added the admin
/// review queue (<see cref="Status"/> onward) — before that this was write-only, visible only to the reporter.</summary>
public sealed class DriverReport
{
    public Guid Id { get; set; }
    public Guid ReporterUserId { get; set; }
    public Guid DriverUserId { get; set; }
    public required string Category { get; set; }
    public required string Description { get; set; }
    public string Status { get; set; } = DriverReportStatuses.Open;
    public Guid? ReviewerUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
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

public static class DriverReportStatuses
{
    public const string Open = "Open";
    public const string Reviewed = "Reviewed";
    public const string Dismissed = "Dismissed";
    public static readonly string[] All = [Open, Reviewed, Dismissed];
}
