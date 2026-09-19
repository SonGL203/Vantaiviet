using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IDriverService
{
    Task<DriverProfileResponse?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
    Task<DriverProfileResponse> UpsertProfileAsync(Guid userId, UpsertDriverProfileRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<VehicleResponse>> ListVehiclesAsync(Guid userId, CancellationToken cancellationToken);
    Task<VehicleResponse> CreateVehicleAsync(Guid userId, CreateVehicleRequest request, CancellationToken cancellationToken);
    Task<VehicleResponse> UpdateVehicleAsync(Guid userId, Guid vehicleId, UpdateVehicleRequest request, CancellationToken cancellationToken);
    Task DeleteVehicleAsync(Guid userId, Guid vehicleId, CancellationToken cancellationToken);
}
