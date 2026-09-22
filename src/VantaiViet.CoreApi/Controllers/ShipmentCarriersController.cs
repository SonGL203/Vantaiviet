using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController, Authorize, Route("api/shipments/{shipmentId:guid}")]
public sealed class ShipmentCarriersController(IShipmentCarrierService service) : ControllerBase
{
    [HttpGet("takers")]
    public async Task<IActionResult> Takers(Guid shipmentId, CancellationToken ct)
    {
        return this.FromResult(await service.TakersAsync(shipmentId, ct), "shipment");
    }

    [HttpGet("takers/{carrierUserId:guid}")]
    public async Task<IActionResult> Profile(Guid shipmentId, Guid carrierUserId, CancellationToken ct)
    {
        return this.FromResult(await service.ProfileAsync(shipmentId, carrierUserId, ct), "shipment");
    }
}
