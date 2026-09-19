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
}
