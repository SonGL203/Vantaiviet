using VantaiViet.CoreApi.DTOs;
namespace VantaiViet.CoreApi.Services.Interfaces;
public interface IOtpService
{
    Task<ShipmentResult<OtpStatusDto>> RequestAsync(RequestOtpDto request,CancellationToken ct);
    Task<ShipmentResult<OtpStatusDto>> StatusAsync(Guid id,CancellationToken ct);
    Task<ShipmentResult<OtpVerificationDto>> VerifyAsync(Guid id,VerifyOtpDto request,CancellationToken ct);
    Task<ShipmentResult<DevelopmentOtpDto>> PreviewAsync(Guid id,CancellationToken ct);
}
