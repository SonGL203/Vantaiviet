using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class KycReviewService(CoreDbContext dbContext, TimeProvider timeProvider)
    : IKycReviewService
{
    // Draft applications are still being filled in by the applicant, so they are not reviewable yet.
    private static readonly string[] ListableStatuses = ["Submitted", "UnderReview", "Approved", "Rejected"];

    public async Task<IReadOnlyList<KycApplicationListItem>> ListAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100)
        {
            throw new InvalidOperationException("Invalid pagination.");
        }

        var filter = status?.Trim();
        if (!string.IsNullOrEmpty(filter) && !ListableStatuses.Contains(filter))
        {
            throw new InvalidOperationException("Unknown KYC status filter.");
        }

        var applications = dbContext.KycApplications.AsNoTracking().Where(x => x.Status != "Draft");
        if (!string.IsNullOrEmpty(filter))
        {
            applications = applications.Where(x => x.Status == filter);
        }

        // Left joins: an application may not have an account or identity details yet.
        return await (from application in applications
                      join account in dbContext.Users.AsNoTracking()
                          on application.RegistrationApplicationId equals account.RegistrationApplicationId into accounts
                      from user in accounts.DefaultIfEmpty()
                      join detail in dbContext.KycIdentityDetails.AsNoTracking()
                          on application.Id equals detail.ApplicationId into details
                      from identity in details.DefaultIfEmpty()
                      orderby application.SubmittedAt descending, application.CreatedAt descending, application.Id
                      select new KycApplicationListItem(
                          application.Id,
                          user == null ? (Guid?)null : user.Id,
                          user == null ? null : user.DisplayName,
                          user == null ? null : user.PhoneNumber,
                          identity == null ? null : identity.FullName,
                          application.Status,
                          dbContext.KycDocuments.Count(d => d.ApplicationId == application.Id && d.DeletedAt == null),
                          application.SubmittedAt,
                          application.ReviewedAt,
                          application.ReviewNote))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

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

    public async Task<KycApprovalResponse> RejectAsync(
        Guid kycApplicationId,
        Guid reviewerUserId,
        RejectKycRequest request,
        CancellationToken cancellationToken)
    {
        var rejectionCode = request.RejectionCode.Trim();
        if (rejectionCode.Length == 0)
        {
            throw new InvalidOperationException("A rejection code is required.");
        }

        var now = timeProvider.GetUtcNow();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var application = await dbContext.KycApplications.SingleOrDefaultAsync(
            x => x.Id == kycApplicationId,
            cancellationToken) ?? throw new InvalidOperationException("KYC application was not found.");
        if (application.Status is not ("Submitted" or "UnderReview"))
        {
            throw new InvalidOperationException("Only submitted KYC applications can be rejected.");
        }

        var user = await dbContext.Users.SingleAsync(
            x => x.RegistrationApplicationId == application.RegistrationApplicationId,
            cancellationToken);
        application.Status = "Rejected";
        application.ReviewedAt = now;
        application.ReviewerUserId = reviewerUserId;
        application.ReviewerReference = reviewerUserId.ToString();
        application.RejectionCode = rejectionCode;
        application.ReviewNote = request.ReviewNote?.Trim();
        application.UpdatedAt = now;
        application.Version++;
        dbContext.AuditEvents.Add(new Entities.AuditEvent
        {
            ActorUserId = reviewerUserId,
            Action = "kyc.rejected",
            TargetType = "KycApplication",
            TargetId = application.Id,
            Outcome = "Succeeded",
            OccurredAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new KycApprovalResponse(application.Id, user.Id, application.Status, user.AccountStatus, now);
    }
}
