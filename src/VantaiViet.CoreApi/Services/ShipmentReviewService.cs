using Microsoft.EntityFrameworkCore;
using Npgsql;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

/// <summary>The owner of a completed shipment rates the carrier who did the job (the driver, and the broker if one held it).</summary>
public sealed class ShipmentReviewService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IShipmentReviewService
{
    private const string DriverKind = "Driver";
    private const string BrokerKind = "Broker";

    private sealed record Job(Guid TripId, string TripStatus, Guid DriverUserId, Guid? BrokerUserId);

    private async Task<Job?> JobOfAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        var trip = await (from b in db.Bookings.AsNoTracking()
            join t in db.Trips on b.Id equals t.BookingId
            where b.ShipmentId == shipmentId && b.OwnerUserId == actor.UserId
            select new { t.Id, t.Status, t.DriverUserId }).SingleOrDefaultAsync(cancellationToken);
        if (trip is null)
        {
            return null;
        }

        var broker = await db.Set<ShipmentBroker>().AsNoTracking().Where(x => x.ShipmentId == shipmentId && x.Status == "Accepted")
            .Select(x => (Guid?)x.BrokerUserId).SingleOrDefaultAsync(cancellationToken);
        return new Job(trip.Id, trip.Status, trip.DriverUserId, broker);
    }

    public async Task<ShipmentResult<ShipmentReviewsResponse>> ListAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        if (!await db.Shipments.AnyAsync(x => x.Id == shipmentId && x.OwnerUserId == actor.UserId, cancellationToken))
        {
            return ShipmentResult<ShipmentReviewsResponse>.Fail("not_found");
        }

        var job = await JobOfAsync(shipmentId, cancellationToken);
        if (job is null)
        {
            return new(new ShipmentReviewsResponse(false, []));
        }

        var ids = new List<Guid> { job.DriverUserId };
        if (job.BrokerUserId is { } brokerId && brokerId != job.DriverUserId)
        {
            ids.Add(brokerId);
        }

        var names = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        var reviews = await db.TripReviews.AsNoTracking().Where(x => x.TripId == job.TripId).ToDictionaryAsync(x => x.TargetUserId, cancellationToken);
        var targets = ids.Select(id => new ReviewTarget(id, id == job.DriverUserId ? DriverKind : BrokerKind, names.GetValueOrDefault(id, string.Empty),
            reviews.TryGetValue(id, out var review) ? new ReviewInfo(review.Stars, review.Comment, review.CreatedAt) : null)).ToList();
        return new(new ShipmentReviewsResponse(job.TripStatus == TripStates.Completed, targets));
    }

    public async Task<ShipmentResult<ReviewInfo>> CreateAsync(Guid shipmentId, CreateReviewRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Shipments.AnyAsync(x => x.Id == shipmentId && x.OwnerUserId == actor.UserId, cancellationToken))
        {
            return ShipmentResult<ReviewInfo>.Fail("not_found");
        }

        var job = await JobOfAsync(shipmentId, cancellationToken);
        if (job is null || job.TripStatus != TripStates.Completed)
        {
            return ShipmentResult<ReviewInfo>.Fail("conflict");
        }

        if (request.TargetUserId != job.DriverUserId && request.TargetUserId != job.BrokerUserId)
        {
            return ShipmentResult<ReviewInfo>.Fail("invalid_target");
        }

        var comment = request.Comment?.Trim();
        var review = new TripReview { TripId = job.TripId, TargetUserId = request.TargetUserId, ReviewerUserId = actor.UserId,
            Stars = request.Stars, Comment = string.IsNullOrEmpty(comment) ? null : comment, CreatedAt = clock.GetUtcNow() };
        db.TripReviews.Add(review);
        db.AuditEvents.Add(new AuditEvent { ActorUserId = actor.UserId, Action = "shipment.reviewed", TargetType = "Shipment", TargetId = shipmentId,
            Outcome = "Succeeded", OccurredAt = review.CreatedAt });
        await NotificationEvents.RaiseAsync(db, clock, NotificationTypes.ReviewReceived, request.TargetUserId, shipmentId, actor.UserId, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ShipmentResult<ReviewInfo>.Fail("already_reviewed");
        }

        return new(new ReviewInfo(review.Stars, review.Comment, review.CreatedAt));
    }
}
