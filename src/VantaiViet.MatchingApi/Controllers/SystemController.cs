using Microsoft.AspNetCore.Mvc;
using VantaiViet.MatchingApi.DTOs;
using VantaiViet.MatchingApi.Services.Interfaces;

namespace VantaiViet.MatchingApi.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController(ISystemInfoService systemInfoService) : ControllerBase
{
    [HttpGet("info")]
    [ProducesResponseType<ServiceInfoResponse>(StatusCodes.Status200OK)]
    public ActionResult<ServiceInfoResponse> GetInfo()
    {
        return Ok(systemInfoService.GetInfo());
    }
}
