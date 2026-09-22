using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IBrokerRouteInterestService
{
    Task<ShipmentResult<IReadOnlyList<BrokerRouteInterestResponse>>> ListAsync(CancellationToken cancellationToken);
    Task<ShipmentResult<BrokerRouteInterestResponse>> CreateAsync(BrokerRouteInterestRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<IReadOnlyList<BrokerRouteInterestResponse>>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
