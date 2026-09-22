using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

/// <summary>"Có hàng phù hợp": the routes a broker watches, so they hear about new cargo on them without searching.</summary>
public sealed class BrokerRouteInterestService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IBrokerRouteInterestService
{
    private static BrokerRouteInterestResponse Map(BrokerRouteInterest x) => new(x.Id, x.FromArea, x.ToArea, x.CreatedAt);

    private Task<List<BrokerRouteInterestResponse>> AllAsync(CancellationToken cancellationToken) =>
        db.BrokerRouteInterests.AsNoTracking().Where(x => x.BrokerUserId == actor.UserId).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => new BrokerRouteInterestResponse(x.Id, x.FromArea, x.ToArea, x.CreatedAt)).ToListAsync(cancellationToken);

    public async Task<ShipmentResult<IReadOnlyList<BrokerRouteInterestResponse>>> ListAsync(CancellationToken cancellationToken)
    {
        return new(await AllAsync(cancellationToken));
    }

    public async Task<ShipmentResult<BrokerRouteInterestResponse>> CreateAsync(BrokerRouteInterestRequest request, CancellationToken cancellationToken)
    {
        var from = request.FromArea.Trim();
        var to = request.ToArea.Trim();
        if (from.Length == 0 || to.Length == 0)
        {
            return ShipmentResult<BrokerRouteInterestResponse>.Fail("invalid_details");
        }

        var mine = db.BrokerRouteInterests.Where(x => x.BrokerUserId == actor.UserId);
        if (await mine.CountAsync(cancellationToken) >= FollowUpLimits.MaxRouteInterests)
        {
            return ShipmentResult<BrokerRouteInterestResponse>.Fail("limit_reached");
        }

        if (await mine.AnyAsync(x => x.FromArea.ToLower() == from.ToLower() && x.ToArea.ToLower() == to.ToLower(), cancellationToken))
        {
            return ShipmentResult<BrokerRouteInterestResponse>.Fail("duplicate_route");
        }

        var interest = new BrokerRouteInterest { Id = Guid.NewGuid(), BrokerUserId = actor.UserId, FromArea = from, ToArea = to, CreatedAt = clock.GetUtcNow() };
        db.BrokerRouteInterests.Add(interest);
        await db.SaveChangesAsync(cancellationToken);
        return new(Map(interest));
    }

    public async Task<ShipmentResult<IReadOnlyList<BrokerRouteInterestResponse>>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var removed = await db.BrokerRouteInterests.Where(x => x.Id == id && x.BrokerUserId == actor.UserId).ExecuteDeleteAsync(cancellationToken);
        if (removed == 0)
        {
            return ShipmentResult<IReadOnlyList<BrokerRouteInterestResponse>>.Fail("not_found");
        }

        return new(await AllAsync(cancellationToken));
    }
}
