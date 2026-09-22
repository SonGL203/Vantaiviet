using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController, Authorize(Roles = "Shipper"), Route("api/shipper/business-profile"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ShipperBusinessProfileController(IBusinessProfileService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        return this.FromResult(await service.GetAsync(cancellationToken), "business_profile");
    }

    [HttpPut]
    public async Task<IActionResult> UpsertAsync(UpsertBusinessProfileRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.UpsertAsync(request, cancellationToken), "business_profile");
    }
}
