using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IKycReviewService
{
    Task<KycApprovalResponse> ApproveAsync(
        Guid kycApplicationId,
        Guid reviewerUserId,
        ApproveKycRequest request,
        CancellationToken cancellationToken);

    Task<KycApprovalResponse> RejectAsync(
        Guid kycApplicationId,
        Guid reviewerUserId,
        RejectKycRequest request,
        CancellationToken cancellationToken);

    /// <summary>Review queue, newest submission first. <paramref name="status"/> is optional.</summary>
    Task<IReadOnlyList<KycApplicationListItem>> ListAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
