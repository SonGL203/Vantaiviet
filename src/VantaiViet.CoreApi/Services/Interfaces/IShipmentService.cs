using VantaiViet.CoreApi.DTOs;
namespace VantaiViet.CoreApi.Services.Interfaces;
public interface IShipmentService
{
    Task<ShipmentResult<ShipmentResponse>> CreateAsync(CreateShipmentRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<ShipmentOwnerResponse>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ShipmentResult<IReadOnlyList<ShipmentResponse>>> ListAsync(bool mine, int page, int pageSize, CancellationToken cancellationToken);
    Task<ShipmentResult<ShipmentResponse>> UpdateAsync(Guid id, UpdateShipmentRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<ShipmentResponse>> TransitionAsync(Guid id, string action, long version, CancellationToken cancellationToken);
}
