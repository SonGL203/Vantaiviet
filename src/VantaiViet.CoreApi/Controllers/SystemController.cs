using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController(ISystemInfoService systemInfoService) : ControllerBase
{
    [HttpGet("info")]
    [ProducesResponseType<ApiResponse<ServiceInfoResponse>>(StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<ServiceInfoResponse>> GetInfo()
    {
        return Ok(ApiResponse<ServiceInfoResponse>.Ok(
            systemInfoService.GetInfo(),
            HttpContext.TraceIdentifier));
    }
}
