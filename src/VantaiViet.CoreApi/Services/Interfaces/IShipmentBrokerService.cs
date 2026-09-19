using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IShipmentBrokerService
{
    Task<ShipmentResult<ShipmentBrokerResponse>> AssignAsync(Guid shipmentId, AssignBrokerRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<ShipmentBrokerResponse>> DecideAsync(Guid shipmentId, string action, BrokerDecisionRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<IReadOnlyList<ShipmentBrokerResponse>>> ListAsync(int page, CancellationToken cancellationToken);
}
