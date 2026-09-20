using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public sealed class DriverReviewController(IDriverReviewService driverReviewService) : ControllerBase
{
    /// <summary>Driver profile review queue. Optional filter: status=Submitted|Approved|Rejected.</summary>
    [HttpGet("drivers")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DriverProfileListItem>>>> ListDriversAsync(
        CancellationToken cancellationToken, string? status = null) =>
        Ok(ApiResponse<IReadOnlyList<DriverProfileListItem>>.Ok(
            await driverReviewService.ListDriverProfilesAsync(status, cancellationToken), HttpContext.TraceIdentifier));

    /// <summary>Vehicle review queue. Optional filter: status=Submitted|Approved|Rejected.</summary>
    [HttpGet("vehicles")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<VehicleListItem>>>> ListVehiclesAsync(
        CancellationToken cancellationToken, string? status = null) =>
        Ok(ApiResponse<IReadOnlyList<VehicleListItem>>.Ok(
            await driverReviewService.ListVehiclesAsync(status, cancellationToken), HttpContext.TraceIdentifier));

    [HttpPut("drivers/{driverUserId:guid}/review")]
    public async Task<ActionResult<ApiResponse<DriverProfileResponse>>> ReviewDriverAsync(Guid driverUserId, ReviewSubmissionRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverProfileResponse>.Ok(
            await driverReviewService.ReviewProfileAsync(driverUserId, GetActorId(), request, cancellationToken),
            HttpContext.TraceIdentifier));

    [HttpPut("vehicles/{vehicleId:guid}/review")]
    public async Task<ActionResult<ApiResponse<VehicleResponse>>> ReviewVehicleAsync(Guid vehicleId, ReviewSubmissionRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<VehicleResponse>.Ok(
            await driverReviewService.ReviewVehicleAsync(vehicleId, GetActorId(), request, cancellationToken),
            HttpContext.TraceIdentifier));

    private Guid GetActorId() => Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? throw new UnauthorizedAccessException("Missing user id claim."));
}
