using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record CreateRegistrationRequest(
    [param: Required, RegularExpression(@"^\\+[1-9][0-9]{7,14}$")] string PhoneNumber,
    [param: StringLength(150)] string? DisplayName);
