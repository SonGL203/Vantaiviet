using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record LoginRequest(
    [param: Required] string PhoneNumber,
    [param: Required] string Password,
    // What the person will recognise in the list of signed-in devices, e.g. "Windows" or "Android".
    [param: StringLength(80)] string? DeviceName = null);
