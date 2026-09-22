using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController, Authorize, Route("api/shipments/{shipmentId:guid}/tracking")]
public sealed class ShipmentProgressController(IShipmentProgressService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Track(Guid shipmentId, CancellationToken ct)
    {
        return this.FromResult(await service.TrackAsync(shipmentId, ct), "shipment");
    }
}
