using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class KycReviewService(CoreDbContext dbContext, TimeProvider timeProvider)
    : IKycReviewService
{
    public async Task<KycApprovalResponse> ApproveAsync(
        Guid kycApplicationId,
        Guid reviewerUserId,
        ApproveKycRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var application = await dbContext.KycApplications.SingleOrDefaultAsync(
            x => x.Id == kycApplicationId,
            cancellationToken) ?? throw new InvalidOperationException("KYC application was not found.");
        if (application.Status is not ("Submitted" or "UnderReview"))
        {
            throw new InvalidOperationException("Only submitted KYC applications can be approved.");
        }

        var user = await dbContext.Users.SingleAsync(
            x => x.RegistrationApplicationId == application.RegistrationApplicationId,
            cancellationToken);
        application.Status = "Approved";
        application.ReviewedAt = now;
        application.ReviewerUserId = reviewerUserId;
        application.ReviewerReference = reviewerUserId.ToString();
        application.ReviewNote = request.ReviewNote?.Trim();
        application.UpdatedAt = now;
        application.Version++;
        user.AccountStatus = "Active";
        user.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new KycApprovalResponse(application.Id, user.Id, application.Status, user.AccountStatus, now);
    }
}
