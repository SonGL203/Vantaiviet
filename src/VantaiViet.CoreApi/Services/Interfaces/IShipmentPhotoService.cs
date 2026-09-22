using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IShipmentPhotoService
{
    Task<ShipmentResult<IReadOnlyList<ShipmentPhotoInfo>>> ListAsync(Guid shipmentId, CancellationToken cancellationToken);

    Task<ShipmentResult<ShipmentPhotoContent>> GetContentAsync(Guid shipmentId, Guid photoId, CancellationToken cancellationToken);

    /// <summary>Adds one photo and returns the shipment's photos afterwards.</summary>
    Task<ShipmentResult<IReadOnlyList<ShipmentPhotoInfo>>> AddAsync(Guid shipmentId, UploadShipmentPhotoRequest request, CancellationToken cancellationToken);

    /// <summary>Removes one photo and returns the shipment's photos afterwards.</summary>
    Task<ShipmentResult<IReadOnlyList<ShipmentPhotoInfo>>> RemoveAsync(Guid shipmentId, Guid photoId, CancellationToken cancellationToken);
}
