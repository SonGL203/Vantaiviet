namespace VantaiViet.CoreApi.DTOs;

public sealed record ApiResponse<T>(
    bool Success,
    T Data,
    string TraceId)
{
    public static ApiResponse<T> Ok(T data, string traceId) => new(true, data, traceId);
}
