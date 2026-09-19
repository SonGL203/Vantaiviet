using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class SystemInfoService(TimeProvider timeProvider) : ISystemInfoService
{
    public ServiceInfoResponse GetInfo()
    {
        var version = typeof(SystemInfoService).Assembly.GetName().Version?.ToString() ?? "unknown";
        return new ServiceInfoResponse("VantaiViet.CoreApi", version, timeProvider.GetUtcNow());
    }
}
