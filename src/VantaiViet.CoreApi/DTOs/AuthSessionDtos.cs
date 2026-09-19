using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record RefreshSessionRequest([param: Required, StringLength(128,MinimumLength=32)] string RefreshToken);
public sealed record ChangePasswordRequest([param: Required,StringLength(100)] string CurrentPassword,
    [param: Required,StringLength(100,MinimumLength=8)] string NewPassword);
public sealed record AuthSessionResponse(Guid Id,DateTimeOffset CreatedAt,DateTimeOffset ExpiresAt,bool Current);
public sealed record ForgotPasswordRequest([param: Required,EmailAddress,StringLength(256)] string Email);
public sealed record PasswordResetChallenge(Guid ChallengeId);
public sealed record ResetPasswordRequest(Guid ChallengeId,[param: Required,RegularExpression("^[0-9]{6}$")] string Code,
    [param: Required,StringLength(100,MinimumLength=8)] string NewPassword);
