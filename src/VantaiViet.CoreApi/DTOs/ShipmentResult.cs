namespace VantaiViet.CoreApi.DTOs;
public sealed record ShipmentResult<T>(T? Data, string? ErrorCode = null)
{
    public static ShipmentResult<T> Fail(string code) => new(default, code);
}
