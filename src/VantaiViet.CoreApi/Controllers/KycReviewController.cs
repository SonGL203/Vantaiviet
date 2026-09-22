using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin,KycReviewer")]
[Route("api/admin/kyc")]
public sealed class KycReviewController(IKycReviewService kycReviewService) : ControllerBase
{
    /// <summary>KYC review queue. Optional filter: status=Submitted|UnderReview|Approved|Rejected.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<KycApplicationListItem>>>> ListAsync(
        CancellationToken cancellationToken,
        string? status = null,
        int page = 1,
        int pageSize = 20)
    {
        var applications = await kycReviewService.ListAsync(status, page, pageSize, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<KycApplicationListItem>>.Ok(applications, HttpContext.TraceIdentifier));
    }

    [HttpPost("{kycApplicationId:guid}/approve")]
    public async Task<ActionResult<ApiResponse<KycApprovalResponse>>> ApproveAsync(
        Guid kycApplicationId,
        ApproveKycRequest request,
        CancellationToken cancellationToken)
    {
        var reviewerId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(reviewerId, out var reviewerUserId))
        {
            return Unauthorized();
        }

        var response = await kycReviewService.ApproveAsync(
            kycApplicationId, reviewerUserId, request, cancellationToken);
        return Ok(ApiResponse<KycApprovalResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    [HttpPost("{kycApplicationId:guid}/reject")]
    public async Task<ActionResult<ApiResponse<KycApprovalResponse>>> RejectAsync(
        Guid kycApplicationId,
        RejectKycRequest request,
        CancellationToken cancellationToken)
    {
        var reviewerId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(reviewerId, out var reviewerUserId))
        {
            return Unauthorized();
        }

        var response = await kycReviewService.RejectAsync(
            kycApplicationId, reviewerUserId, request, cancellationToken);
        return Ok(ApiResponse<KycApprovalResponse>.Ok(response, HttpContext.TraceIdentifier));
    }
}
