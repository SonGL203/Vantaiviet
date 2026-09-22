using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IBrokerProfileService
{
    Task<ShipmentResult<BrokerProfileResponse>> GetAsync(CancellationToken cancellationToken);
    Task<ShipmentResult<BrokerProfileResponse>> UpsertAsync(UpsertBrokerProfileRequest request, CancellationToken cancellationToken);
}
