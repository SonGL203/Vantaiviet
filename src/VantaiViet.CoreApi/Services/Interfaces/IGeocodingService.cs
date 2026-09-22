using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IGeocodingService
{
    Task<ShipmentResult<IReadOnlyList<GeocodeResult>>> SearchAsync(string query, CancellationToken cancellationToken);
}
