using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IVehicleProposalService
{
    // Broker side: feature 13 (send), 18 (the price is the offer), the broker's own view of a shipment's offers.
    Task<ShipmentResult<VehicleProposalResponse>> ProposeAsync(Guid shipmentId, CreateVehicleProposalRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<IReadOnlyList<VehicleProposalResponse>>> ForShipmentAsync(Guid shipmentId, CancellationToken cancellationToken);
    Task<ShipmentResult<VehicleProposalResponse>> AcceptCounterAsync(Guid id, long version, CancellationToken cancellationToken);
    Task<ShipmentResult<bool>> WithdrawAsync(Guid id, long version, CancellationToken cancellationToken);

    // Driver side: feature 19 (one counter-offer), feature 20 ("tài xế nhận đơn").
    Task<ShipmentResult<IReadOnlyList<VehicleProposalResponse>>> MineAsync(CancellationToken cancellationToken);
    Task<ShipmentResult<VehicleProposalResponse>> AcceptAsync(Guid id, long version, CancellationToken cancellationToken);
    Task<ShipmentResult<VehicleProposalResponse>> RejectAsync(Guid id, long version, CancellationToken cancellationToken);
    Task<ShipmentResult<VehicleProposalResponse>> CounterAsync(Guid id, CounterVehicleProposalRequest request, CancellationToken cancellationToken);
}
