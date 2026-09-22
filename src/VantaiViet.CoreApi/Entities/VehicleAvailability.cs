namespace VantaiViet.CoreApi.Entities;

/// <summary>A driver's truck is empty and looking for cargo on a route, for a while: what a broker searches to propose a trip.</summary>
public sealed class VehicleAvailability
{
    public Guid Id { get; set; }
    public Guid DriverUserId { get; set; }
    public Guid VehicleId { get; set; }
    public required string FromAddress { get; set; }
    public required string ToAddress { get; set; }
    public DateTimeOffset AvailableFrom { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? Notes { get; set; }

    /// <summary>The driver is heading back toward their base and would rather not run this leg empty (feature 12).</summary>
    public bool IsReturnTrip { get; set; }
    public string Status { get; set; } = VehicleAvailabilityStatuses.Open;
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public static class VehicleAvailabilityStatuses
{
    public const string Open = "Open";
    public const string Withdrawn = "Withdrawn";

    /// <summary>A proposal against this posting was accepted; the truck is booked.</summary>
    public const string Matched = "Matched";
    public static readonly string[] All = [Open, Withdrawn, Matched];
}
