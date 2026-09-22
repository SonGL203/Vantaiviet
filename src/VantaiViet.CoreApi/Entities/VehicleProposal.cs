namespace VantaiViet.CoreApi.Entities;

/// <summary>A broker's direct offer to a driver's open <see cref="VehicleAvailability"/> posting, features 13/18-20:
/// "Gửi đề nghị chuyến", "Báo giá chuyến", "Thương lượng giá" and "Xác nhận giao chuyến" (tài xế nhận đơn).</summary>
public sealed class VehicleProposal
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public Guid VehicleAvailabilityId { get; set; }
    public Guid BrokerUserId { get; set; }
    public Guid DriverUserId { get; set; }
    public Guid VehicleId { get; set; }

    /// <summary>What the broker offered to pay for the whole trip, whole dong.</summary>
    public decimal ProposedPrice { get; set; }

    /// <summary>The driver's one counter-offer, if they made one; null otherwise.</summary>
    public decimal? CounterPrice { get; set; }
    public string Status { get; set; } = VehicleProposalStatuses.Pending;
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public static class VehicleProposalStatuses
{
    public const string Pending = "Pending";
    public const string CounterOffered = "CounterOffered";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";
    public static readonly string[] All = [Pending, CounterOffered, Accepted, Rejected, Withdrawn];

    /// <summary>No further action changes a proposal once it lands here.</summary>
    public static bool IsFinal(string status) => status is Accepted or Rejected or Withdrawn;
}
