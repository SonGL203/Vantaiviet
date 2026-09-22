using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

/// <summary>
/// The log of one shipment, oldest first: what was done to the order, who asked for it and how they were answered,
/// each stage of the trip with its notes, and the incidents and ratings that followed. Assembled from what is already recorded.
/// </summary>
public sealed class ShipmentHistoryService(CoreDbContext db, ICurrentActor actor) : IShipmentHistoryService
{
    private const int MaxEntries = 200;

    private sealed record Raw(DateTimeOffset At, string Action, Guid? ActorUserId, string? Note, string? Detail = null);

    public async Task<ShipmentResult<ShipmentHistoryResponse>> HistoryAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        if (!await db.Database.SqlQuery<bool>($"""SELECT public."CanDispatchShipment"({shipmentId}, {actor.UserId}) AS "Value" """).SingleAsync(cancellationToken))
        {
            return ShipmentResult<ShipmentHistoryResponse>.Fail("not_found");
        }

        var shipment = await db.Shipments.AsNoTracking().Where(x => x.Id == shipmentId).Select(x => new { x.Code, x.Status, x.OwnerUserId }).SingleAsync(cancellationToken);
        var requestIds = await db.TransportRequests.AsNoTracking().Where(x => x.ShipmentId == shipmentId).Select(x => x.Id).ToListAsync(cancellationToken);
        var tripId = await (from b in db.Bookings.AsNoTracking() join t in db.Trips on b.Id equals t.BookingId where b.ShipmentId == shipmentId select (Guid?)t.Id)
            .SingleOrDefaultAsync(cancellationToken);

        var raw = new List<Raw>();
        // "shipment.incident" is left out: the incident itself, added below, says more.
        raw.AddRange(await db.AuditEvents.AsNoTracking()
            .Where(x => ((x.TargetType == "Shipment" && x.TargetId == shipmentId) || (x.TargetType == "TransportRequest" && requestIds.Contains(x.TargetId)))
                && x.Action != "shipment.incident" && x.Action != "shipment.reviewed")
            .Select(x => new Raw(x.OccurredAt, x.Action, x.ActorUserId, null, null)).ToListAsync(cancellationToken));

        var incidents = await db.ShipmentIncidents.AsNoTracking().Where(x => x.ShipmentId == shipmentId)
            .Select(x => new Raw(x.CreatedAt, "incident.reported", x.ReporterUserId, x.Description, x.Category)).ToListAsync(cancellationToken);
        raw.AddRange(incidents);

        if (tripId is { } trip)
        {
            var events = await db.TripEvents.AsNoTracking().Where(x => x.TripId == trip)
                .Select(x => new Raw(x.OccurredAt, "trip." + x.Action.ToLower(), x.ActorUserId, x.Note, null)).ToListAsync(cancellationToken);
            // A report made through the incident service is also written into the trip log for the driver; it is shown once.
            raw.AddRange(events.Where(e => e.Action != "trip.incident" || !incidents.Any(i => i.ActorUserId == e.ActorUserId && e.Note == $"[{i.Detail}] {i.Note}")));
        }

        // Ratings are the owner's own business, so only the owner's history carries them.
        if (shipment.OwnerUserId == actor.UserId && tripId is { } reviewedTrip)
        {
            raw.AddRange(await db.TripReviews.AsNoTracking().Where(x => x.TripId == reviewedTrip)
                .Select(x => new Raw(x.CreatedAt, "review.created", x.ReviewerUserId, x.Comment, x.Stars.ToString())).ToListAsync(cancellationToken));
        }

        var ordered = raw.OrderBy(x => x.At).Take(MaxEntries).ToList();
        var actorIds = ordered.Where(x => x.ActorUserId.HasValue).Select(x => x.ActorUserId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(x => actorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        var entries = ordered.Select(x => new HistoryEntry(x.At, x.Action, x.ActorUserId is { } id ? names.GetValueOrDefault(id) : null,
            x.ActorUserId == actor.UserId, x.Note, x.Detail)).ToList();
        return new(new ShipmentHistoryResponse(shipment.Code, shipment.Status, entries));
    }
}
