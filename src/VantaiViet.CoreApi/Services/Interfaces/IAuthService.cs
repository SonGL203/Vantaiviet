using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<ShipmentResult<AuthResponse>> RefreshAsync(RefreshSessionRequest request,CancellationToken cancellationToken);
    Task<ShipmentResult<bool>> LogoutAsync(CancellationToken cancellationToken);
    Task<ShipmentResult<bool>> RevokeSessionAsync(Guid sessionId,CancellationToken cancellationToken);
    Task<ShipmentResult<IReadOnlyList<AuthSessionResponse>>> SessionsAsync(CancellationToken cancellationToken);
    Task<ShipmentResult<bool>> ChangePasswordAsync(ChangePasswordRequest request,CancellationToken cancellationToken);
    Task<bool> IsSessionActiveAsync(Guid userId,Guid sessionId,string securityStamp,CancellationToken cancellationToken);
}
