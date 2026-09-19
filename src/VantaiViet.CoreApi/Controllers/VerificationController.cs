using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Controllers;
[ApiController,Authorize,Route("api/verification"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class VerificationController(IOtpService service) : ControllerBase
{
    [HttpPost("requests")]
    public async Task<IActionResult> RequestAsync(RequestOtpDto request,CancellationToken ct)=>Respond(await service.RequestAsync(request,ct));
    [HttpGet("requests/{id:guid}")]
    public async Task<IActionResult> StatusAsync(Guid id,CancellationToken ct)=>Respond(await service.StatusAsync(id,ct));
    [HttpPost("requests/{id:guid}/verify")]
    public async Task<IActionResult> VerifyAsync(Guid id,VerifyOtpDto request,CancellationToken ct)=>Respond(await service.VerifyAsync(id,request,ct));
    [HttpGet("requests/{id:guid}/development-code")]
    public async Task<IActionResult> PreviewAsync(Guid id,CancellationToken ct)=>Respond(await service.PreviewAsync(id,ct));
    private IActionResult Respond<T>(ShipmentResult<T> r)=>r.ErrorCode is null?Ok(ApiResponse<T>.Ok(r.Data!,HttpContext.TraceIdentifier)):
        Problem(statusCode:r.ErrorCode switch {"not_found"=>404,"forbidden"=>403,"conflict"=>409,"rate_limited"=>429,"not_configured"=>503,_=>400},
        title:"Verification request rejected.",extensions:new Dictionary<string,object?>{["errorCode"]="verification."+r.ErrorCode,["traceId"]=HttpContext.TraceIdentifier});
}
