using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

/// <summary>A driver posts and withdraws their own truck's availability.</summary>
[ApiController, Authorize(Roles = "Driver"), Route("api/driver/vehicle-availability")]
public sealed class DriverVehicleAvailabilityController(IVehicleAvailabilityService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> PostAsync(CreateVehicleAvailabilityRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.PostAsync(request, cancellationToken), "vehicle_availability");
    }

    [HttpGet("mine")]
    public async Task<IActionResult> MineAsync(CancellationToken cancellationToken)
    {
        return this.FromResult(await service.MineAsync(cancellationToken), "vehicle_availability");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> WithdrawAsync(Guid id, [FromQuery] long version, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.WithdrawAsync(id, version, cancellationToken), "vehicle_availability");
    }
}

/// <summary>A broker searches the vehicles that are open and still looking for cargo, features 9-12 of "Tìm xe".</summary>
[ApiController, Authorize(Roles = "Broker"), Route("api/broker/vehicle-availability")]
public sealed class BrokerVehicleSearchController(IVehicleAvailabilityService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> SearchAsync(CancellationToken cancellationToken,
        string? fromArea = null, string? toArea = null, string? vehicleType = null, decimal? minPayloadKg = null, string? bodyType = null,
        bool returnTripOnly = false, int page = 1, int pageSize = 20)
    {
        return this.FromResult(await service.SearchAsync(fromArea, toArea, vehicleType, minPayloadKg, bodyType, returnTripOnly, page, pageSize, cancellationToken), "vehicle_availability");
    }
}
