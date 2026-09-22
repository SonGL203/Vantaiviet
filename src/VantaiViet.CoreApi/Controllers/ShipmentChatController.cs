using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController, Authorize, Route("api/shipments/{shipmentId:guid}/messages")]
public sealed class ShipmentChatController(IShipmentChatService service) : ControllerBase
{
    /// <summary>Messages after <c>afterId</c> (0 for all), oldest first; clients poll with the last id they have.</summary>
    [HttpGet]
    public async Task<IActionResult> List(Guid shipmentId, CancellationToken ct, Guid? carrierUserId = null, long afterId = 0)
    {
        return this.FromResult(await service.ListAsync(shipmentId, carrierUserId, afterId, ct), "shipment");
    }

    [HttpPost]
    public async Task<IActionResult> Send(Guid shipmentId, SendChatMessageRequest request, CancellationToken ct)
    {
        return this.FromResult(await service.SendAsync(shipmentId, request, ct), "shipment");
    }
}
