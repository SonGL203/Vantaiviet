using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController, Authorize, Route("api/shipments/{shipmentId:guid}/photos"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ShipmentPhotosController(IShipmentPhotoService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.ListAsync(shipmentId, cancellationToken), "shipment");
    }

    [HttpPost, RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> AddAsync(Guid shipmentId, UploadShipmentPhotoRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.AddAsync(shipmentId, request, cancellationToken), "shipment");
    }

    [HttpGet("{photoId:guid}")]
    public async Task<IActionResult> GetContentAsync(Guid shipmentId, Guid photoId, CancellationToken cancellationToken)
    {
        var result = await service.GetContentAsync(shipmentId, photoId, cancellationToken);
        if (result.Data is null)
        {
            return this.FromResult(result, "shipment");
        }

        return File(result.Data.Content, result.Data.ContentType);
    }

    [HttpDelete("{photoId:guid}")]
    public async Task<IActionResult> RemoveAsync(Guid shipmentId, Guid photoId, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.RemoveAsync(shipmentId, photoId, cancellationToken), "shipment");
    }
}
