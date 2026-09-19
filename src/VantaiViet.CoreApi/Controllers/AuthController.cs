using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController]
[EnableRateLimiting("auth")]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshAsync(RefreshSessionRequest request,CancellationToken ct) => Respond(await authService.RefreshAsync(request,ct));
    [Authorize,HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync(CancellationToken ct) => Respond(await authService.LogoutAsync(ct));
    [Authorize,HttpGet("sessions")]
    public async Task<IActionResult> SessionsAsync(CancellationToken ct) => Respond(await authService.SessionsAsync(ct));
    [Authorize,HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> RevokeAsync(Guid id,CancellationToken ct) => Respond(await authService.RevokeSessionAsync(id,ct));
    [Authorize,HttpPost("change-password")]
    public async Task<IActionResult> ChangePasswordAsync(ChangePasswordRequest request,CancellationToken ct) => Respond(await authService.ChangePasswordAsync(request,ct));

    private IActionResult Respond<T>(ShipmentResult<T> result)
    {
        if (result.ErrorCode is null) { return Ok(ApiResponse<T>.Ok(result.Data!,HttpContext.TraceIdentifier)); }
        var status = result.ErrorCode switch { "not_found" => 404,"invalid_credentials" => 401,_ => 400 };
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext,statusCode:status,title:"Authentication request rejected.");
        problem.Extensions["errorCode"] = "auth." + result.ErrorCode;
        return new ObjectResult(problem) { StatusCode=status };
    }
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.RegisterAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponse>.Ok(response, HttpContext.TraceIdentifier));
    }
}
