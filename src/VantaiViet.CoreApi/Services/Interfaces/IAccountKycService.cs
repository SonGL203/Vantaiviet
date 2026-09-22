using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IAccountKycService
{
    Task<ShipmentResult<KycStatusResponse>> GetAsync(CancellationToken cancellationToken);
    Task<ShipmentResult<KycStatusResponse>> SaveIdentityAsync(SaveKycIdentityRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<KycStatusResponse>> UploadDocumentAsync(string documentType, UploadKycDocumentRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<KycStatusResponse>> SubmitAsync(CancellationToken cancellationToken);
}
