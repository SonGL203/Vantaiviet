using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IAccountProfileService
{
    Task<ShipmentResult<AccountProfileResponse>> GetAsync(CancellationToken cancellationToken);
    Task<ShipmentResult<AccountProfileResponse>> UpdateAsync(UpdateAccountProfileRequest request, CancellationToken cancellationToken);
}
