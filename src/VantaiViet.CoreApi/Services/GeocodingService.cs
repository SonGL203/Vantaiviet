using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class GeocodingService(HttpClient client, IMemoryCache cache, IConfiguration config) : IGeocodingService
{
    private const int MinimumQueryLength = 3;
    private const int MaximumQueryLength = 200;
    private const int ResultLimit = 5;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

    // Nominatim returns coordinates as JSON strings.
    private sealed record Place(
        [property: JsonPropertyName("display_name")] string? DisplayName,
        [property: JsonPropertyName("lat")] string? Latitude,
        [property: JsonPropertyName("lon")] string? Longitude);

    public async Task<ShipmentResult<IReadOnlyList<GeocodeResult>>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var text = string.Join(' ', (query ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length < MinimumQueryLength || text.Length > MaximumQueryLength)
        {
            return ShipmentResult<IReadOnlyList<GeocodeResult>>.Fail("invalid_query");
        }

        var baseUrl = config["Maps:GeocodingBaseUrl"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https")
        {
            return ShipmentResult<IReadOnlyList<GeocodeResult>>.Fail("not_configured");
        }

        var cacheKey = "geocode:" + text.ToLowerInvariant();
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<GeocodeResult>? cached) && cached is not null)
        {
            return new(cached);
        }

        var path = string.Create(CultureInfo.InvariantCulture,
            $"search?format=jsonv2&limit={ResultLimit}&countrycodes=vn&accept-language=vi&q={Uri.EscapeDataString(text)}");
        try
        {
            using var response = await client.GetAsync(new Uri(uri, path), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ShipmentResult<IReadOnlyList<GeocodeResult>>.Fail("unavailable");
            }

            var places = await response.Content.ReadFromJsonAsync<List<Place>>(cancellationToken) ?? [];
            var results = places.Select(ToResult).OfType<GeocodeResult>().ToList();
            cache.Set(cacheKey, (IReadOnlyList<GeocodeResult>)results, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = CacheLifetime });
            return new(results);
        }
        catch (HttpRequestException)
        {
            return ShipmentResult<IReadOnlyList<GeocodeResult>>.Fail("unavailable");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ShipmentResult<IReadOnlyList<GeocodeResult>>.Fail("unavailable");
        }
        catch (JsonException)
        {
            return ShipmentResult<IReadOnlyList<GeocodeResult>>.Fail("unavailable");
        }
    }

    private static GeocodeResult? ToResult(Place place)
    {
        if (string.IsNullOrWhiteSpace(place.DisplayName)
            || !double.TryParse(place.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
            || !double.TryParse(place.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
            || !double.IsFinite(latitude) || !double.IsFinite(longitude)
            || latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return null;
        }

        return new GeocodeResult(place.DisplayName, latitude, longitude);
    }
}
