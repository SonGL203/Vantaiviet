using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController, Authorize, Route("api/account"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(IAccountProfileService service) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfileAsync(CancellationToken cancellationToken)
    {
        return this.FromResult(await service.GetAsync(cancellationToken), "account");
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfileAsync(UpdateAccountProfileRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.UpdateAsync(request, cancellationToken), "account");
    }
}
