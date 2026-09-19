namespace VantaiViet.CoreApi.DTOs;

public sealed record RegistrationCreatedResponse(
    Guid RegistrationApplicationId,
    string RegistrationSessionToken,
    DateTimeOffset ExpiresAt);
