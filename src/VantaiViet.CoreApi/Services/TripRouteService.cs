using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class TripRouteService(CoreDbContext db, ICurrentActor actor, IRouteProvider provider,
    IConfiguration configuration, TimeProvider clock) : ITripRouteService
{
    public async Task<RouteResponse> GetAsync(Guid tripId, CoordinateDto pickup, CoordinateDto delivery, CancellationToken cancellationToken)
    {
        var input = JsonSerializer.Serialize(new { pickup, delivery, Provider = configuration["Maps:RoutingBaseUrl"], Version = 1 });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
        var lease = Guid.NewGuid();
        var waitUntil = clock.GetUtcNow().AddSeconds(10);
        do
        {
            var now = clock.GetUtcNow();
            var state = await db.Database.SqlQuery<int>($"""
                SELECT public."ClaimTripRoute"({tripId}, {actor.UserId}, {hash}, {lease}, {now}) AS "Value"
                """).SingleAsync(cancellationToken);
            if (state == 0) { return new("Unavailable", [], null, null); }
            if (state == 3)
            {
                var json = await db.Set<Entities.TripRoute>().Where(x => x.TripId == tripId && x.InputHash == hash)
                    .Select(x => x.ResponseJson).SingleAsync(cancellationToken);
                if (json is not null) { return JsonSerializer.Deserialize<RouteResponse>(json) ?? new("Unavailable", [], null, null); }
            }
            if (state == 1)
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(8));
                    var result = await provider.GetAsync(pickup, delivery, timeout.Token);
                    var seconds = result.Status is "ReferenceCarRoute" or "NoRoute"
                        ? Math.Clamp(configuration.GetValue("Maps:RouteCacheSeconds", 3600), 1, 86400) : 5;
                    var json = JsonSerializer.Serialize(result);
                    var expires = clock.GetUtcNow().AddSeconds(seconds);
                    await db.Set<Entities.TripRoute>().Where(x => x.TripId == tripId && x.InputHash == hash && x.LeaseId == lease)
                        .ExecuteUpdateAsync(x => x.SetProperty(r => r.ResponseJson, json)
                            .SetProperty(r => r.ExpiresAt, expires).SetProperty(r => r.LeaseUntil, now), cancellationToken);
                    return result;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return new("Unavailable", [], null, null);
                }
                // An interrupted caller leaves a bounded lease; another process can recover it.
            }
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        } while (clock.GetUtcNow() < waitUntil);
        return new("Unavailable", [], null, null);
    }
}
