namespace VantaiViet.CoreApi.DTOs;

public sealed record AdminUserResponse(
    Guid Id,
    string DisplayName,
    string PhoneNumber,
    string AccountStatus,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt);
