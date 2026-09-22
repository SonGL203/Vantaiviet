using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

/// <summary>"Có hàng phù hợp": the routes a broker watches for new cargo.</summary>
[ApiController, Authorize(Roles = "Broker"), Route("api/broker/route-interests")]
public sealed class BrokerRouteInterestController(IBrokerRouteInterestService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        return this.FromResult(await service.ListAsync(cancellationToken), "route_interest");
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(BrokerRouteInterestRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.CreateAsync(request, cancellationToken), "route_interest");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.DeleteAsync(id, cancellationToken), "route_interest");
    }
}
