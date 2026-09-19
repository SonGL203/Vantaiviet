namespace VantaiViet.CoreApi.DTOs;

public sealed record AuthResponse(
    Guid UserId,
    string DisplayName,
    string AccountStatus,
    IReadOnlyList<string> Roles,
    bool AuthenticationEnabled,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
