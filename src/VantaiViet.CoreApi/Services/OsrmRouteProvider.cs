using System.Globalization;
using System.Text.Json;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Services;
public sealed class OsrmRouteProvider(HttpClient client,IConfiguration config) : IRouteProvider
{
    public async Task<RouteResponse> GetAsync(CoordinateDto pickup,CoordinateDto delivery,CancellationToken cancellationToken)
    {
        var baseUrl=config["Maps:RoutingBaseUrl"];
        if (!Uri.TryCreate(baseUrl,UriKind.Absolute,out var uri) || uri.Scheme!="https") { return new("NotConfigured",[],null,null); }
        var path=string.Create(CultureInfo.InvariantCulture,$"route/v1/driving/{pickup.Longitude},{pickup.Latitude};{delivery.Longitude},{delivery.Latitude}?overview=simplified&geometries=geojson");
        try
        {
            using var response=await client.GetAsync(new Uri(uri,path),cancellationToken);
            if (!response.IsSuccessStatusCode) { return new("Unavailable",[],null,null); }
            using var json=JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            if (json.RootElement.GetProperty("code").GetString()!="Ok") { return new("NoRoute",[],null,null); }
            var route=json.RootElement.GetProperty("routes")[0];
            var coords=route.GetProperty("geometry").GetProperty("coordinates").Deserialize<double[][]>() ?? [];
            return new("ReferenceCarRoute",coords,route.GetProperty("distance").GetDouble(),route.GetProperty("duration").GetDouble());
        }
        catch (HttpRequestException) { return new("Unavailable",[],null,null); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new("Unavailable",[],null,null); }
        catch (JsonException) { return new("Unavailable",[],null,null); }
        catch (KeyNotFoundException) { return new("Unavailable",[],null,null); }
    }
}
