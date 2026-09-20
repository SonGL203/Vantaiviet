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
    public async Task<ActionResult<ApiResponse<DriverProfileResponse>>> UpsertProfileAsync(UpsertDriverProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await driverService.UpsertProfileAsync(GetUserId(), request, cancellationToken);
        return Ok(ApiResponse<DriverProfileResponse>.Ok(profile, HttpContext.TraceIdentifier));
    }

    [HttpGet("vehicles")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<VehicleResponse>>>> ListVehiclesAsync(CancellationToken cancellationToken)
    {
        var vehicles = await driverService.ListVehiclesAsync(GetUserId(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<VehicleResponse>>.Ok(vehicles, HttpContext.TraceIdentifier));
    }

    [HttpPost("vehicles")]
    public async Task<ActionResult<ApiResponse<VehicleResponse>>> CreateVehicleAsync(CreateVehicleRequest request, CancellationToken cancellationToken)
    {
        var vehicle = await driverService.CreateVehicleAsync(GetUserId(), request, cancellationToken);
        return Ok(ApiResponse<VehicleResponse>.Ok(vehicle, HttpContext.TraceIdentifier));
    }

    [HttpPut("vehicles/{vehicleId:guid}")]
    public async Task<ActionResult<ApiResponse<VehicleResponse>>> UpdateVehicleAsync(Guid vehicleId, UpdateVehicleRequest request, CancellationToken cancellationToken)
    {
        var vehicle = await driverService.UpdateVehicleAsync(GetUserId(), vehicleId, request, cancellationToken);
        return Ok(ApiResponse<VehicleResponse>.Ok(vehicle, HttpContext.TraceIdentifier));
    }

    [HttpDelete("vehicles/{vehicleId:guid}")]
    public async Task<IActionResult> DeleteVehicleAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        await driverService.DeleteVehicleAsync(GetUserId(), vehicleId, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? throw new UnauthorizedAccessException("Missing user id claim."));
    }
}
