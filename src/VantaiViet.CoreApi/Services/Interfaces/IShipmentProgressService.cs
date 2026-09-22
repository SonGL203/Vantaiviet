using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IShipmentProgressService
{
    Task<ShipmentResult<ShipmentTrackingResponse>> TrackAsync(Guid shipmentId, CancellationToken cancellationToken);
}
