using System.ComponentModel.DataAnnotations;

namespace VantaiViet.CoreApi.DTOs;

public sealed record SetUserRolesRequest(
    [param: Required, MinLength(1)] IReadOnlyList<string> Roles);
