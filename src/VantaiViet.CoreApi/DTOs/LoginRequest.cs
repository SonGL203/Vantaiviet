using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record LoginRequest(
    [param: Required] string PhoneNumber,
    [param: Required] string Password);
