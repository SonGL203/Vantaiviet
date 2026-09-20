namespace VantaiViet.CoreApi.DTOs;

/// <summary>One row of the KYC review queue. Never carries the identity number or document contents.</summary>
public sealed record KycApplicationListItem(
    Guid Id,
    Guid? UserId,
    string? DisplayName,
    string? PhoneNumber,
    string? FullName,
    string Status,
    int DocumentCount,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote);
