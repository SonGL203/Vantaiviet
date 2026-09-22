using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController, Authorize, Route("api/shipments/{shipmentId:guid}/reviews")]
public sealed class ShipmentReviewsController(IShipmentReviewService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid shipmentId, CancellationToken ct)
    {
        return this.FromResult(await service.ListAsync(shipmentId, ct), "shipment");
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid shipmentId, CreateReviewRequest request, CancellationToken ct)
    {
        return this.FromResult(await service.CreateAsync(shipmentId, request, ct), "shipment");
    }
}

[ApiController, Authorize, Route("api/shipments/{shipmentId:guid}/incidents")]
public sealed class ShipmentIncidentsController(IShipmentIncidentService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid shipmentId, CancellationToken ct)
    {
        return this.FromResult(await service.ListAsync(shipmentId, ct), "shipment");
    }

    [HttpPost]
    public async Task<IActionResult> Report(Guid shipmentId, ReportIncidentRequest request, CancellationToken ct)
    {
        return this.FromResult(await service.ReportAsync(shipmentId, request, ct), "shipment");
    }
}

[ApiController, Authorize, Route("api/shipments/{shipmentId:guid}/history")]
public sealed class ShipmentHistoryController(IShipmentHistoryService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> History(Guid shipmentId, CancellationToken ct)
    {
        return this.FromResult(await service.HistoryAsync(shipmentId, ct), "shipment");
    }
}

[ApiController, Authorize, Route("api/notifications")]
public sealed class NotificationsController(IUserNotificationService service) : ControllerBase
{
    /// <summary>Newest first. Pass the id of the last item you have as <c>beforeId</c> to get the next page.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, long beforeId = 0, int pageSize = 20, bool unreadOnly = false)
    {
        return this.FromResult(await service.ListAsync(beforeId, pageSize, unreadOnly, ct), "notification");
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        return this.FromResult(await service.UnreadCountAsync(ct), "notification");
    }

    [HttpPost("read")]
    public async Task<IActionResult> MarkRead(MarkNotificationsRequest request, CancellationToken ct)
    {
        return this.FromResult(await service.MarkReadAsync(request.Ids, ct), "notification");
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        return this.FromResult(await service.MarkReadAsync(null, ct), "notification");
    }
}

[ApiController, Authorize, Route("api/account/addresses")]
public sealed class SavedAddressesController(ISavedAddressService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        return this.FromResult(await service.ListAsync(ct), "address");
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveAddressRequest request, CancellationToken ct)
    {
        return this.FromResult(await service.CreateAsync(request, ct), "address");
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveAddressRequest request, CancellationToken ct)
    {
        return this.FromResult(await service.UpdateAsync(id, request, ct), "address");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        return this.FromResult(await service.DeleteAsync(id, ct), "address");
    }
}
