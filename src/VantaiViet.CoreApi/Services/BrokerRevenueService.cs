using System.Globalization;
using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

/// <summary>Feature 21 ("Hoa hồng"): the commission a broker earns on their own external orders, computed from the
/// commission rate they set on each shipment and the price it actually finalized at — both already snapshotted on
/// the shipment itself, so nothing here recalculates history against a rate that may since have changed.</summary>
public sealed class BrokerRevenueService(CoreDbContext db, ICurrentActor actor) : IBrokerRevenueService
{
    private const int MaxPageSize = 100;

    private static string Money(decimal value) => value.ToString("F0", CultureInfo.InvariantCulture);

    private IQueryable<Shipment> BaseQuery() => db.Shipments.AsNoTracking().Where(x => x.OwnerUserId == actor.UserId
        && x.IsExternalOrder && x.BrokerCommissionRate != null && x.ProposedPrice != null
        && (x.Status == ShipmentStatuses.Booked || x.Status == ShipmentStatuses.Completed));

    private static BrokerRevenueItem Map(Shipment s)
    {
        var commission = s.ProposedPrice!.Value * s.BrokerCommissionRate!.Value / 100m;
        return new(s.Id, s.Code, s.Title, s.Status, Money(s.ProposedPrice.Value), s.BrokerCommissionRate.Value,
            Money(commission), s.Currency, s.Status == ShipmentStatuses.Completed ? s.UpdatedAt : null);
    }

    public async Task<ShipmentResult<IReadOnlyList<BrokerRevenueItem>>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize)
        {
            return ShipmentResult<IReadOnlyList<BrokerRevenueItem>>.Fail("invalid_pagination");
        }

        var rows = await BaseQuery().OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(rows.Select(Map).ToList());
    }

    public async Task<ShipmentResult<BrokerRevenueSummaryResponse>> SummaryAsync(CancellationToken cancellationToken)
    {
        var rows = await BaseQuery().Select(x => new { x.ProposedPrice, x.BrokerCommissionRate, x.Currency }).ToListAsync(cancellationToken);
        var currency = rows.Count == 0 ? ShipmentLimits.DefaultCurrency : rows[0].Currency;
        var totalRevenue = rows.Sum(x => x.ProposedPrice!.Value);
        var totalCommission = rows.Sum(x => x.ProposedPrice!.Value * x.BrokerCommissionRate!.Value / 100m);
        return new(new BrokerRevenueSummaryResponse(rows.Count, Money(totalRevenue), Money(totalCommission), currency));
    }

    /// <summary>Grouped in memory, not in SQL: a broker's own deals are always few enough that this is simpler and
    /// safer than an EF `GroupBy` translated to a database aggregate (see AGENTS.md's note on that pitfall).
    /// The period is the shipment's own <c>UpdatedAt</c> — when it last changed status, since a deal here counts
    /// from the moment it is Booked, not only once it is Completed.</summary>
    public async Task<ShipmentResult<IReadOnlyList<BrokerRevenuePeriodItem>>> ByPeriodAsync(string granularity, int take, CancellationToken cancellationToken)
    {
        if (granularity is not ("day" or "month") || take is < 1 or > 60)
        {
            return ShipmentResult<IReadOnlyList<BrokerRevenuePeriodItem>>.Fail("invalid_pagination");
        }

        var rows = await BaseQuery().Select(x => new { x.UpdatedAt, x.ProposedPrice, x.BrokerCommissionRate }).ToListAsync(cancellationToken);
        var format = granularity == "day" ? "yyyy-MM-dd" : "yyyy-MM";
        var grouped = rows.GroupBy(x => x.UpdatedAt.ToString(format, CultureInfo.InvariantCulture))
            .Select(g => new BrokerRevenuePeriodItem(g.Key, g.Count(),
                Money(g.Sum(x => x.ProposedPrice!.Value)), Money(g.Sum(x => x.ProposedPrice!.Value * x.BrokerCommissionRate!.Value / 100m))))
            .OrderByDescending(x => x.Period).Take(take).ToList();
        return new(grouped);
    }
}
