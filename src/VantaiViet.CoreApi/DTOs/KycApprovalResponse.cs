namespace VantaiViet.CoreApi.DTOs;

public sealed record KycApprovalResponse(
    Guid KycApplicationId,
    Guid UserId,
    string KycStatus,
    string AccountStatus,
    DateTimeOffset ReviewedAt);
