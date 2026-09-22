using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IVehicleAvailabilityService
{
    Task<ShipmentResult<VehicleAvailabilityResponse>> PostAsync(CreateVehicleAvailabilityRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<IReadOnlyList<VehicleAvailabilityResponse>>> MineAsync(CancellationToken cancellationToken);
    Task<ShipmentResult<bool>> WithdrawAsync(Guid id, long version, CancellationToken cancellationToken);
    Task<ShipmentResult<IReadOnlyList<VehicleAvailabilityResponse>>> SearchAsync(
        string? fromArea, string? toArea, string? vehicleType, decimal? minPayloadKg, string? bodyType, bool returnTripOnly, int page, int pageSize, CancellationToken cancellationToken);
}
