namespace VantaiViet.CoreApi.DTOs;

/// <summary>Feature 21 ("Hoa hồng"): one of the broker's own external orders that reached Booked or Completed with a
/// commission rate set. FinalPrice is the shipment's own ProposedPrice at that point (already the price actually
/// struck, not the shipper's original ask, once a proposal was involved); CommissionAmount is derived from the two
/// snapshotted values on the row, never recomputed against today's rate.</summary>
public sealed record BrokerRevenueItem(Guid ShipmentId, string ShipmentCode, string ShipmentTitle, string Status,
    string FinalPrice, decimal CommissionRate, string CommissionAmount, string Currency, DateTimeOffset? CompletedAt);

public sealed record BrokerRevenueSummaryResponse(int DealCount, string TotalRevenue, string TotalCommission, string Currency);

/// <summary>One day or one month's worth of the broker's revenue, most recent first. <see cref="Period"/> is
/// "yyyy-MM-dd" or "yyyy-MM" depending on the granularity asked for.</summary>
public sealed record BrokerRevenuePeriodItem(string Period, int DealCount, string TotalRevenue, string TotalCommission);
