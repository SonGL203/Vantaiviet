using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record ReviewSubmissionRequest(
    [param: Required] bool Approved,
    [param: StringLength(1000)] string? ReviewNote);
