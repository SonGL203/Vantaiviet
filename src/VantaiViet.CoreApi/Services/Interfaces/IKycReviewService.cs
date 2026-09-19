using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IKycReviewService
{
    Task<KycApprovalResponse> ApproveAsync(
        Guid kycApplicationId,
        Guid reviewerUserId,
        ApproveKycRequest request,
        CancellationToken cancellationToken);
}
