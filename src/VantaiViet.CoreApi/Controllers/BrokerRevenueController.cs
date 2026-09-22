using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

/// <summary>Feature 21 ("Hoa hồng"): the commission a broker has earned on their own booked and completed orders.</summary>
[ApiController, Authorize(Roles = "Broker"), Route("api/broker/revenue")]
public sealed class BrokerRevenueController(IBrokerRevenueService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken, int page = 1, int pageSize = 20)
    {
        return this.FromResult(await service.ListAsync(page, pageSize, cancellationToken), "broker_revenue");
    }

    [HttpGet("summary")]
    public async Task<IActionResult> SummaryAsync(CancellationToken cancellationToken)
    {
        return this.FromResult(await service.SummaryAsync(cancellationToken), "broker_revenue");
    }

    [HttpGet("by-period")]
    public async Task<IActionResult> ByPeriodAsync(CancellationToken cancellationToken, string granularity = "month", int take = 12)
    {
        return this.FromResult(await service.ByPeriodAsync(granularity, take, cancellationToken), "broker_revenue");
    }
}
