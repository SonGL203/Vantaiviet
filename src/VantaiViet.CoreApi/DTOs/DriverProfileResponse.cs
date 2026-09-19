namespace VantaiViet.CoreApi.DTOs;

public sealed record DriverProfileResponse(
    Guid UserId, string LicenseClass, DateOnly LicenseExpiresOn, string Status, string? ReviewNote);
