using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController, Authorize, EnableRateLimiting("geocoding"), Route("api/geocoding")]
public sealed class GeocodingController(IGeocodingService service) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> SearchAsync([FromQuery] string q, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.SearchAsync(q, cancellationToken), "geocoding");
    }
}
