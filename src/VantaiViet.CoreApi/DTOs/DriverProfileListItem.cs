namespace VantaiViet.CoreApi.DTOs;

/// <summary>One row of the driver profile review queue.</summary>
public sealed record DriverProfileListItem(
    Guid UserId,
    string? DisplayName,
    string? PhoneNumber,
    string LicenseClass,
    DateOnly LicenseExpiresOn,
    string Status,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote,
    DateTimeOffset CreatedAt);
