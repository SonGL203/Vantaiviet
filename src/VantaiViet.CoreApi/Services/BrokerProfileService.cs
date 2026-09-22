using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class BrokerProfileService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IBrokerProfileService
{
    private const int MinimumAreaLength = 2;

    private async Task<RatingSummary> RatingAsync(CancellationToken cancellationToken)
    {
        var stars = await db.TripReviews.AsNoTracking().Where(x => x.TargetUserId == actor.UserId).Select(x => x.Stars).ToListAsync(cancellationToken);
        return stars.Count == 0 ? new RatingSummary(null, 0) : new RatingSummary(Math.Round(stars.Average(), 1), stars.Count);
    }

    public async Task<ShipmentResult<BrokerProfileResponse>> GetAsync(CancellationToken cancellationToken)
    {
        var profile = await db.BrokerProfiles.AsNoTracking().Where(x => x.UserId == actor.UserId)
            .Select(x => new { x.OperatingArea, x.YearsExperience, x.Version, x.UpdatedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (profile is null) { return ShipmentResult<BrokerProfileResponse>.Fail("not_found"); }
        var rating = await RatingAsync(cancellationToken);
        return new(new BrokerProfileResponse(profile.OperatingArea, profile.YearsExperience, profile.Version, profile.UpdatedAt, rating));
    }

    public async Task<ShipmentResult<BrokerProfileResponse>> UpsertAsync(UpsertBrokerProfileRequest request, CancellationToken cancellationToken)
    {
        var area = request.OperatingArea.Trim();
        if (area.Length < MinimumAreaLength)
        {
            return ShipmentResult<BrokerProfileResponse>.Fail("invalid_details");
        }

        var now = clock.GetUtcNow();
        var profile = await db.BrokerProfiles.SingleOrDefaultAsync(x => x.UserId == actor.UserId, cancellationToken);
        if (profile is null)
        {
            profile = new BrokerProfile
            {
                UserId = actor.UserId,
                OperatingArea = area,
                YearsExperience = request.YearsExperience,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.BrokerProfiles.Add(profile);
        }
        else
        {
            if (request.Version != profile.Version)
            {
                return ShipmentResult<BrokerProfileResponse>.Fail("conflict");
            }

            profile.OperatingArea = area;
            profile.YearsExperience = request.YearsExperience;
            profile.UpdatedAt = now;
            profile.Version++;
        }

        db.AuditEvents.Add(new AuditEvent { ActorUserId = actor.UserId, Action = "broker_profile.saved", TargetType = "User", TargetId = actor.UserId, Outcome = "Succeeded", OccurredAt = now });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return ShipmentResult<BrokerProfileResponse>.Fail("conflict");
        }

        var rating = await RatingAsync(cancellationToken);
        return new(new BrokerProfileResponse(profile.OperatingArea, profile.YearsExperience, profile.Version, profile.UpdatedAt, rating));
    }
}
