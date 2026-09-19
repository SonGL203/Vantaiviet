using VantaiViet.MatchingApi.DTOs;
using VantaiViet.MatchingApi.Services.Interfaces;

namespace VantaiViet.MatchingApi.Services;

public sealed class SystemInfoService(TimeProvider timeProvider) : ISystemInfoService
{
    public ServiceInfoResponse GetInfo()
    {
        var version = typeof(SystemInfoService).Assembly.GetName().Version?.ToString() ?? "unknown";
        return new ServiceInfoResponse("VantaiViet.MatchingApi", version, timeProvider.GetUtcNow());
    }
}
