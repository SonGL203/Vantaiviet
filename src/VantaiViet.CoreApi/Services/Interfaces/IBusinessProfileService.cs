using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IBusinessProfileService
{
    Task<ShipmentResult<BusinessProfileResponse>> GetAsync(CancellationToken cancellationToken);
    Task<ShipmentResult<BusinessProfileResponse>> UpsertAsync(UpsertBusinessProfileRequest request, CancellationToken cancellationToken);
}
