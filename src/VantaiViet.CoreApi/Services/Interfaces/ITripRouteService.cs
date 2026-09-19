using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface ITripRouteService
{
    Task<RouteResponse> GetAsync(Guid tripId, CoordinateDto pickup, CoordinateDto delivery, CancellationToken cancellationToken);
}
