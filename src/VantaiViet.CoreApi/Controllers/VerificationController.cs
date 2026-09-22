using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Controllers;
[ApiController,Authorize,Route("api/verification"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class VerificationController(IOtpService service) : ControllerBase
{
    [HttpPost("requests")]
    public async Task<IActionResult> RequestAsync(RequestOtpDto request,CancellationToken ct)
    {
        return Respond(await service.RequestAsync(request,ct));
    }
    [HttpGet("requests/{id:guid}")]
    public async Task<IActionResult> StatusAsync(Guid id,CancellationToken ct)
    {
        return Respond(await service.StatusAsync(id,ct));
    }
    [HttpPost("requests/{id:guid}/verify")]
    public async Task<IActionResult> VerifyAsync(Guid id,VerifyOtpDto request,CancellationToken ct)
    {
        return Respond(await service.VerifyAsync(id,request,ct));
    }
    [HttpGet("requests/{id:guid}/development-code")]
    public async Task<IActionResult> PreviewAsync(Guid id,CancellationToken ct)
    {
        return Respond(await service.PreviewAsync(id,ct));
    }
    private IActionResult Respond<T>(ShipmentResult<T> r)
    {
        // Problem(extensions: ...) collides with the errorCode that CustomizeProblemDetails already adds and answers 500.
        return this.FromResult(r,"verification");
    }
}
