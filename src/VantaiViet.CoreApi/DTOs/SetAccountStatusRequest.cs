using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record SetAccountStatusRequest(
    [param: Required, RegularExpression("^(Active|Suspended|Disabled)$")] string Status);
