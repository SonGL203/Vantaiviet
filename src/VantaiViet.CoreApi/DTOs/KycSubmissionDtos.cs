using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record KycDocumentInfo(string Type, string ContentType, long SizeBytes, DateTimeOffset UploadedAt);

/// <summary>Never carries the full identity number or any image content.</summary>
public sealed record KycStatusResponse(
    Guid? ApplicationId,
    string Status,
    string? SubjectType,
    string? FullName,
    DateOnly? DateOfBirth,
    string? IdentityNumberMasked,
    DateOnly? IssuedOn,
    DateOnly? ExpiresOn,
    IReadOnlyList<KycDocumentInfo> Documents,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    string? RejectionCode,
    string? ReviewNote,
    string AccountStatus);

public sealed record SaveKycIdentityRequest(
    [param: Required, RegularExpression("^(Individual|Business)$")] string SubjectType,
    [param: Required, StringLength(150, MinimumLength = 2)] string FullName,
    DateOnly DateOfBirth,
    [param: Required, RegularExpression("^[0-9]{12}$")] string IdentityNumber,
    DateOnly? IssuedOn = null,
    DateOnly? ExpiresOn = null);

public sealed record UploadKycDocumentRequest([param: Required] string ContentType, [param: Required] byte[] Content);

public sealed record RejectKycRequest(
    [param: Required, StringLength(100)] string RejectionCode,
    [param: StringLength(1000)] string? ReviewNote);
