using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IPasswordRecoveryService
{
    Task<ShipmentResult<PasswordResetChallenge>> RequestAsync(ForgotPasswordRequest request,CancellationToken cancellationToken);
    Task<ShipmentResult<bool>> ResetAsync(ResetPasswordRequest request,CancellationToken cancellationToken);
}
