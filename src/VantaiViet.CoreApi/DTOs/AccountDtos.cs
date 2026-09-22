using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record AccountProfileResponse(
    Guid UserId,
    string DisplayName,
    string PhoneNumber,
    bool PhoneNumberConfirmed,
    string? Email,
    bool EmailConfirmed,
    string AccountStatus,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt);

// Phone is the login name and email is certified only through /api/verification, so only the name is editable here.
public sealed record UpdateAccountProfileRequest([param: Required, StringLength(150, MinimumLength = 2)] string DisplayName);
