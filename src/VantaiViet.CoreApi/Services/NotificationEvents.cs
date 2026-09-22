using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Services;

/// <summary>
/// Puts a notification in the caller's unit of work, so it is saved with the change it describes or not at all.
/// The service that made the change still owns the SaveChanges.
/// </summary>
internal static class NotificationEvents
{
    public static async Task RaiseAsync(CoreDbContext db, TimeProvider clock, string type, Guid recipientUserId, Guid? shipmentId,
        Guid? actorUserId, CancellationToken cancellationToken, string? subjectOverride = null)
    {
        if (recipientUserId == actorUserId)
        {
            return;
        }

        var now = clock.GetUtcNow();
        if (type == NotificationTypes.ChatMessage)
        {
            // A chat is one notification per person however many messages they send, until it is read.
            var unread = await db.UserNotifications.FirstOrDefaultAsync(x => x.UserId == recipientUserId && x.Type == type
                && x.ShipmentId == shipmentId && x.RelatedUserId == actorUserId && x.ReadAt == null, cancellationToken);
            if (unread is not null)
            {
                unread.CreatedAt = now;
                return;
            }
        }

        // Some events (a saved driver posting a truck, say) are not about any one shipment; the caller supplies
        // the subject text itself instead of one looked up from a shipment's code.
        var subject = subjectOverride ?? (shipmentId is { } sid
            ? await db.Shipments.AsNoTracking().Where(x => x.Id == sid).Select(x => x.Code).SingleOrDefaultAsync(cancellationToken)
            : null);
        var actorName = actorUserId is null ? null
            : await db.Users.AsNoTracking().Where(x => x.Id == actorUserId).Select(x => x.DisplayName).SingleOrDefaultAsync(cancellationToken);
        db.UserNotifications.Add(new UserNotification { UserId = recipientUserId, Type = type, ShipmentId = shipmentId,
            RelatedUserId = actorUserId, Subject = subject, ActorName = actorName, CreatedAt = now });
    }
}
