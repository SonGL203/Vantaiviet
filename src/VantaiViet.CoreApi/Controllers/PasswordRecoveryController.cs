using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController,Route("api/auth"),EnableRateLimiting("auth")]
public sealed class PasswordRecoveryController(IPasswordRecoveryService service) : ControllerBase
{
    [HttpPost("forgot-password")]
    public async Task<IActionResult> RequestAsync(ForgotPasswordRequest request,CancellationToken ct) => Respond(await service.RequestAsync(request,ct));
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetAsync(ResetPasswordRequest request,CancellationToken ct) => Respond(await service.ResetAsync(request,ct));

    private IActionResult Respond<T>(ShipmentResult<T> result)
    {
        if (result.ErrorCode is null) { return Ok(ApiResponse<T>.Ok(result.Data!,HttpContext.TraceIdentifier)); }
        var status = result.ErrorCode=="not_configured"?503:400;
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext,statusCode:status,title:"Password recovery request rejected.");
        problem.Extensions["errorCode"] = "auth." + result.ErrorCode;
        return new ObjectResult(problem) { StatusCode=status };
    }
}
