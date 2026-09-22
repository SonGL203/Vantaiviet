using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record BusinessProfileResponse(
    string CompanyName,
    string TaxCode,
    string Address,
    long Version,
    DateTimeOffset UpdatedAt);

/// <summary>Version is required to edit an existing profile and ignored when the profile is first created.</summary>
public sealed record UpsertBusinessProfileRequest(
    [param: Required, StringLength(200, MinimumLength = 2)] string CompanyName,
    [param: Required, RegularExpression(@"^\s*[0-9]{10}(-[0-9]{3})?\s*$")] string TaxCode,
    [param: Required, StringLength(500, MinimumLength = 5)] string Address,
    long? Version = null);
