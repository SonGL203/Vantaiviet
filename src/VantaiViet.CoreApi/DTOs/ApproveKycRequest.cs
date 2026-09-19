using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record ApproveKycRequest(
    [param: StringLength(1000)] string? ReviewNote);
