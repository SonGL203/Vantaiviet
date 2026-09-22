using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class ShipmentPhotoService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IShipmentPhotoService
{
    private const string AcceptedStatus = "Accepted";

    public async Task<ShipmentResult<IReadOnlyList<ShipmentPhotoInfo>>> ListAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        if (!await CanViewAsync(shipmentId, cancellationToken))
        {
            return ShipmentResult<IReadOnlyList<ShipmentPhotoInfo>>.Fail("not_found");
        }

        return new(await PhotosOfAsync(shipmentId, cancellationToken));
    }

    public async Task<ShipmentResult<ShipmentPhotoContent>> GetContentAsync(Guid shipmentId, Guid photoId, CancellationToken cancellationToken)
    {
        if (!await CanViewAsync(shipmentId, cancellationToken))
        {
            return ShipmentResult<ShipmentPhotoContent>.Fail("not_found");
        }

        var photo = await db.ShipmentPhotos.AsNoTracking().Where(x => x.Id == photoId && x.ShipmentId == shipmentId)
            .Select(x => new ShipmentPhotoContent(x.ContentType, x.Content)).SingleOrDefaultAsync(cancellationToken);
        return photo is null ? ShipmentResult<ShipmentPhotoContent>.Fail("not_found") : new(photo);
    }

    public async Task<ShipmentResult<IReadOnlyList<ShipmentPhotoInfo>>> AddAsync(Guid shipmentId, UploadShipmentPhotoRequest request, CancellationToken cancellationToken)
    {
        if (!ImageContent.IsSupported(request.ContentType, request.Content))
        {
            return ShipmentResult<IReadOnlyList<ShipmentPhotoInfo>>.Fail("invalid_image");
        }

        if (!await IsOwnerAsync(shipmentId, cancellationToken))
        {
            return ShipmentResult<IReadOnlyList<ShipmentPhotoInfo>>.Fail("not_found");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var issue = await LockEditableAsync(shipmentId, cancellationToken);
        if (issue is not null)
        {
            return ShipmentResult<IReadOnlyList<ShipmentPhotoInfo>>.Fail(issue);
        }

        // The lock above serializes uploads, so this count cannot be overtaken by a concurrent upload.
        if (await db.ShipmentPhotos.CountAsync(x => x.ShipmentId == shipmentId, cancellationToken) >= ShipmentLimits.MaxPhotos)
        {
            return ShipmentResult<IReadOnlyList<ShipmentPhotoInfo>>.Fail("photo_limit");
        }

        var now = clock.GetUtcNow();
        db.ShipmentPhotos.Add(new ShipmentPhoto
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipmentId,
            ContentType = request.ContentType,
            SizeBytes = request.Content.Length,
            Content = request.Content,
            CreatedAt = now
        });
        AddAudit("shipment.photo_added", shipmentId, now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(await PhotosOfAsync(shipmentId, cancellationToken));
    }

    public async Task<ShipmentResult<IReadOnlyList<ShipmentPhotoInfo>>> RemoveAsync(Guid shipmentId, Guid photoId, CancellationToken cancellationToken)
    {
        if (!await IsOwnerAsync(shipmentId, cancellationToken))
        {
            return ShipmentResult<IReadOnlyList<ShipmentPhotoInfo>>.Fail("not_found");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var issue = await LockEditableAsync(shipmentId, cancellationToken);
        if (issue is not null)
        {
            return ShipmentResult<IReadOnlyList<ShipmentPhotoInfo>>.Fail(issue);
        }

        var removed = await db.ShipmentPhotos.Where(x => x.Id == photoId && x.ShipmentId == shipmentId).ExecuteDeleteAsync(cancellationToken);
        if (removed == 0)
        {
            return ShipmentResult<IReadOnlyList<ShipmentPhotoInfo>>.Fail("not_found");
        }

        AddAudit("shipment.photo_removed", shipmentId, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(await PhotosOfAsync(shipmentId, cancellationToken));
    }

    private Task<bool> IsOwnerAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        return db.Shipments.AnyAsync(x => x.Id == shipmentId && x.OwnerUserId == actor.UserId, cancellationToken);
    }

    // Anyone who may see the shipment may see its photos: the owner, the broker holding it, the driver booked on it,
    // and anybody browsing while it is still open for requests (the same audience as the public shipment list).
    private Task<bool> CanViewAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return db.Shipments.AsNoTracking().AnyAsync(x => x.Id == shipmentId
            && (x.OwnerUserId == actor.UserId
                || (x.Status == ShipmentStatuses.Published && x.PickupAt > now)
                || db.Set<ShipmentBroker>().Any(a => a.ShipmentId == x.Id && a.BrokerUserId == actor.UserId && a.Status == AcceptedStatus)
                || db.Bookings.Any(b => b.ShipmentId == x.Id && b.DriverUserId == actor.UserId)), cancellationToken);
    }

    /// <summary>Locks the shipment row and returns null when photos may change, otherwise the error to report.</summary>
    private async Task<string?> LockEditableAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        var shipment = await db.Shipments.FromSqlInterpolated($"""SELECT * FROM public."Shipments" WHERE "Id"={shipmentId} AND "OwnerUserId"={actor.UserId} FOR UPDATE""")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (shipment is null)
        {
            return "not_found";
        }

        if (!await ShipmentRules.CanPostAsync(db, actor.UserId, shipment.IsExternalOrder, cancellationToken))
        {
            return "forbidden";
        }

        if (shipment.Status is not (ShipmentStatuses.Draft or ShipmentStatuses.Published))
        {
            return "conflict";
        }

        if (shipment.Status == ShipmentStatuses.Published && await ShipmentRules.HasTakersAsync(db, shipmentId, cancellationToken))
        {
            return "has_takers";
        }

        return null;
    }

    private async Task<IReadOnlyList<ShipmentPhotoInfo>> PhotosOfAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        return await db.ShipmentPhotos.AsNoTracking().Where(x => x.ShipmentId == shipmentId).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new ShipmentPhotoInfo(x.Id, x.ContentType, x.SizeBytes, x.CreatedAt)).ToListAsync(cancellationToken);
    }

    private void AddAudit(string action, Guid shipmentId, DateTimeOffset now)
    {
        db.AuditEvents.Add(new AuditEvent { ActorUserId = actor.UserId, Action = action, TargetType = "Shipment", TargetId = shipmentId, Outcome = "Succeeded", OccurredAt = now });
    }
}
