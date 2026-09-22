using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

/// <summary>A broker sends and manages price offers on a shipment they source or dispatch, features 13/18/20.</summary>
[ApiController, Authorize(Roles = "Broker")]
public sealed class ShipmentVehicleProposalsController(IVehicleProposalService service) : ControllerBase
{
    [HttpPost("api/shipments/{shipmentId:guid}/proposals")]
    public async Task<IActionResult> ProposeAsync(Guid shipmentId, CreateVehicleProposalRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.ProposeAsync(shipmentId, request, cancellationToken), "vehicle_proposal");
    }

    [HttpGet("api/shipments/{shipmentId:guid}/proposals")]
    public async Task<IActionResult> ForShipmentAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.ForShipmentAsync(shipmentId, cancellationToken), "vehicle_proposal");
    }

    [HttpPost("api/broker/vehicle-proposals/{id:guid}/accept-counter")]
    public async Task<IActionResult> AcceptCounterAsync(Guid id, VehicleProposalVersionRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.AcceptCounterAsync(id, request.Version, cancellationToken), "vehicle_proposal");
    }

    [HttpPost("api/broker/vehicle-proposals/{id:guid}/withdraw")]
    public async Task<IActionResult> WithdrawAsync(Guid id, VehicleProposalVersionRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.WithdrawAsync(id, request.Version, cancellationToken), "vehicle_proposal");
    }
}

/// <summary>The driver's side of a broker's offer, features 19-20: counter once, accept ("tài xế nhận đơn"), or reject.</summary>
[ApiController, Authorize(Roles = "Driver"), Route("api/driver/vehicle-proposals")]
public sealed class DriverVehicleProposalsController(IVehicleProposalService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> MineAsync(CancellationToken cancellationToken)
    {
        return this.FromResult(await service.MineAsync(cancellationToken), "vehicle_proposal");
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> AcceptAsync(Guid id, VehicleProposalVersionRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.AcceptAsync(id, request.Version, cancellationToken), "vehicle_proposal");
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> RejectAsync(Guid id, VehicleProposalVersionRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.RejectAsync(id, request.Version, cancellationToken), "vehicle_proposal");
    }

    [HttpPost("{id:guid}/counter")]
    public async Task<IActionResult> CounterAsync(Guid id, CounterVehicleProposalRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.CounterAsync(id, request, cancellationToken), "vehicle_proposal");
    }
}
