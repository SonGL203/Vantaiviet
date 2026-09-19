using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record RegisterRequest(
    [param: Required, RegularExpression(@"^\+[1-9][0-9]{7,14}$")] string PhoneNumber,
    [param: Required, StringLength(150, MinimumLength = 2)] string DisplayName,
    [param: Required, StringLength(100, MinimumLength = 8)] string Password,
    [param: RegularExpression("^(Shipper|Broker|Driver)$")] string AccountType = "Shipper");
