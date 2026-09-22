using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

/// <summary>The owner reports a problem with a shipment somebody was booked for. The report is kept, the trip log shows it, and the driver is told.</summary>
public sealed class ShipmentIncidentService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IShipmentIncidentService
{
    public async Task<ShipmentResult<IReadOnlyList<IncidentResponse>>> ListAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        if (!await db.Shipments.AnyAsync(x => x.Id == shipmentId && x.OwnerUserId == actor.UserId, cancellationToken))
        {
            return ShipmentResult<IReadOnlyList<IncidentResponse>>.Fail("not_found");
        }

        return new(await db.ShipmentIncidents.AsNoTracking().Where(x => x.ShipmentId == shipmentId).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new IncidentResponse(x.Id, x.Category, x.Description, x.Status, x.CreatedAt)).ToListAsync(cancellationToken));
    }

    public async Task<ShipmentResult<IncidentResponse>> ReportAsync(Guid shipmentId, ReportIncidentRequest request, CancellationToken cancellationToken)
    {
        var description = request.Description?.Trim() ?? string.Empty;
        if (!IncidentCategories.All.Contains(request.Category) || description.Length < FollowUpLimits.MinIncidentLength
            || description.Length > FollowUpLimits.MaxIncidentLength)
        {
            return ShipmentResult<IncidentResponse>.Fail("invalid_details");
        }

        if (!await db.Shipments.AnyAsync(x => x.Id == shipmentId && x.OwnerUserId == actor.UserId, cancellationToken))
        {
            return ShipmentResult<IncidentResponse>.Fail("not_found");
        }

        // Before anyone is booked there is nothing to report against; the shipper can simply cancel.
        var booked = await (from b in db.Bookings.AsNoTracking() join t in db.Trips on b.Id equals t.BookingId
            where b.ShipmentId == shipmentId select new { TripId = t.Id, t.DriverUserId }).SingleOrDefaultAsync(cancellationToken);
        if (booked is null)
        {
            return ShipmentResult<IncidentResponse>.Fail("conflict");
        }

        if (await db.ShipmentIncidents.CountAsync(x => x.ShipmentId == shipmentId, cancellationToken) >= FollowUpLimits.MaxOpenIncidents)
        {
            return ShipmentResult<IncidentResponse>.Fail("limit_reached");
        }

        var now = clock.GetUtcNow();
        var incident = new ShipmentIncident { Id = Guid.NewGuid(), ShipmentId = shipmentId, ReporterUserId = actor.UserId,
            Category = request.Category, Description = description, CreatedAt = now };
        db.ShipmentIncidents.Add(incident);
        // The trip log is what the driver reads, so the report appears there too.
        db.TripEvents.Add(new TripEvent { Id = Guid.NewGuid(), TripId = booked.TripId, ActorUserId = actor.UserId, Action = "Incident",
            Note = $"[{request.Category}] {description}", OccurredAt = now });
        db.AuditEvents.Add(new AuditEvent { ActorUserId = actor.UserId, Action = "shipment.incident", TargetType = "Shipment", TargetId = shipmentId,
            Outcome = "Succeeded", OccurredAt = now });
        await NotificationEvents.RaiseAsync(db, clock, NotificationTypes.IncidentReported, booked.DriverUserId, shipmentId, actor.UserId, cancellationToken);
        var broker = await db.Set<ShipmentBroker>().AsNoTracking().Where(x => x.ShipmentId == shipmentId && x.Status == "Accepted")
            .Select(x => (Guid?)x.BrokerUserId).SingleOrDefaultAsync(cancellationToken);
        if (broker is { } brokerId && brokerId != booked.DriverUserId)
        {
            await NotificationEvents.RaiseAsync(db, clock, NotificationTypes.IncidentReported, brokerId, shipmentId, actor.UserId, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new(new IncidentResponse(incident.Id, incident.Category, incident.Description, incident.Status, incident.CreatedAt));
    }
}
