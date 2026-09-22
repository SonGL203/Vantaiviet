using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IBrokerRevenueService
{
    Task<ShipmentResult<IReadOnlyList<BrokerRevenueItem>>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<ShipmentResult<BrokerRevenueSummaryResponse>> SummaryAsync(CancellationToken cancellationToken);

    /// <summary>Thống kê doanh thu theo ngày/tháng. <paramref name="granularity"/> is "day" or "month".</summary>
    Task<ShipmentResult<IReadOnlyList<BrokerRevenuePeriodItem>>> ByPeriodAsync(string granularity, int take, CancellationToken cancellationToken);
}
