using VantaiViet.CoreApi.DTOs;
namespace VantaiViet.CoreApi.Services.Interfaces;
public interface IRouteProvider
{
    Task<RouteResponse> GetAsync(CoordinateDto pickup,CoordinateDto delivery,CancellationToken cancellationToken);
}
