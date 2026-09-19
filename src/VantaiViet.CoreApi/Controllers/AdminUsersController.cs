using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public sealed class AdminUsersController(IAdminUserService adminUserService) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminUserResponse>>>> ListUsersAsync(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<AdminUserResponse>>.Ok(
            await adminUserService.ListAsync(cancellationToken), HttpContext.TraceIdentifier));

    [HttpPut("users/{userId:guid}/roles")]
    public async Task<ActionResult<ApiResponse<AdminUserResponse>>> SetRolesAsync(
        Guid userId, SetUserRolesRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminUserResponse>.Ok(
            await adminUserService.SetRolesAsync(userId, request, GetActorId(), cancellationToken),
            HttpContext.TraceIdentifier));

    [HttpPut("users/{userId:guid}/status")]
    public async Task<ActionResult<ApiResponse<AdminUserResponse>>> SetStatusAsync(
        Guid userId, SetAccountStatusRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminUserResponse>.Ok(
            await adminUserService.SetStatusAsync(userId, request, GetActorId(), cancellationToken),
            HttpContext.TraceIdentifier));

    [HttpGet("audit-events")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditEventResponse>>>> ListAuditEventsAsync(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<AuditEventResponse>>.Ok(
            await adminUserService.ListAuditEventsAsync(cancellationToken), HttpContext.TraceIdentifier));

    private Guid GetActorId() =>
        Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? throw new UnauthorizedAccessException("Missing user id claim."));
}
