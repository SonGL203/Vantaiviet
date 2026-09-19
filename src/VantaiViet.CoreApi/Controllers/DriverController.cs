using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController]
[Authorize(Roles = "Driver")]
[Route("api/driver")]
public sealed class DriverController(IDriverService driverService) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<ActionResult<ApiResponse<DriverProfileResponse>>> GetProfileAsync(CancellationToken cancellationToken)
    {
        var profile = await driverService.GetProfileAsync(GetUserId(), cancellationToken);
        return profile is null ? NotFound() : Ok(ApiResponse<DriverProfileResponse>.Ok(profile, HttpContext.TraceIdentifier));
    }

    [HttpPut("profile")]
    public async Task<ActionResult<ApiResponse<DriverProfileResponse>>> UpsertProfileAsync(UpsertDriverProfileRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverProfileResponse>.Ok(await driverService.UpsertProfileAsync(GetUserId(), request, cancellationToken), HttpContext.TraceIdentifier));

    [HttpGet("vehicles")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<VehicleResponse>>>> ListVehiclesAsync(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<VehicleResponse>>.Ok(await driverService.ListVehiclesAsync(GetUserId(), cancellationToken), HttpContext.TraceIdentifier));

    [HttpPost("vehicles")]
    public async Task<ActionResult<ApiResponse<VehicleResponse>>> CreateVehicleAsync(CreateVehicleRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<VehicleResponse>.Ok(await driverService.CreateVehicleAsync(GetUserId(), request, cancellationToken), HttpContext.TraceIdentifier));

    [HttpPut("vehicles/{vehicleId:guid}")]
    public async Task<ActionResult<ApiResponse<VehicleResponse>>> UpdateVehicleAsync(Guid vehicleId, UpdateVehicleRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<VehicleResponse>.Ok(await driverService.UpdateVehicleAsync(GetUserId(), vehicleId, request, cancellationToken), HttpContext.TraceIdentifier));

    [HttpDelete("vehicles/{vehicleId:guid}")]
    public async Task<IActionResult> DeleteVehicleAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        await driverService.DeleteVehicleAsync(GetUserId(), vehicleId, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? throw new UnauthorizedAccessException("Missing user id claim."));
}
