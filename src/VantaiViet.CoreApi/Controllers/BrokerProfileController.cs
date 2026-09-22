using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController, Authorize(Roles = "Broker"), Route("api/broker/profile"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BrokerProfileController(IBrokerProfileService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        return this.FromResult(await service.GetAsync(cancellationToken), "broker_profile");
    }

    [HttpPut]
    public async Task<IActionResult> UpsertAsync(UpsertBrokerProfileRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.UpsertAsync(request, cancellationToken), "broker_profile");
    }
}
