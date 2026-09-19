using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IAdminUserService
{
    Task<IReadOnlyList<AdminUserResponse>> ListAsync(CancellationToken cancellationToken);
    Task<AdminUserResponse> SetRolesAsync(Guid userId, SetUserRolesRequest request, Guid actorId, CancellationToken cancellationToken);
    Task<AdminUserResponse> SetStatusAsync(Guid userId, SetAccountStatusRequest request, Guid actorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditEventResponse>> ListAuditEventsAsync(CancellationToken cancellationToken);
}
