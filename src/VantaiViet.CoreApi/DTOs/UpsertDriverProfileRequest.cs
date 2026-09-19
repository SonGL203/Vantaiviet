using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record UpsertDriverProfileRequest(
    [param: Required, StringLength(20)] string LicenseClass,
    DateOnly LicenseExpiresOn);
