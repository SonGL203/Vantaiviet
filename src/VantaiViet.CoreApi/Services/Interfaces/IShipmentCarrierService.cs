using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IShipmentCarrierService
{
    Task<ShipmentResult<ShipmentTakersResponse>> TakersAsync(Guid shipmentId, CancellationToken cancellationToken);
    Task<ShipmentResult<CarrierProfileResponse>> ProfileAsync(Guid shipmentId, Guid carrierUserId, CancellationToken cancellationToken);
}
