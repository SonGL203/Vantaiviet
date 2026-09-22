using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class UserNotificationService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IUserNotificationService
{
    public async Task<ShipmentResult<NotificationPage>> ListAsync(long beforeId, int pageSize, bool unreadOnly, CancellationToken cancellationToken)
    {
        if (beforeId < 0 || pageSize < 1 || pageSize > FollowUpLimits.NotificationPageSize)
        {
            return ShipmentResult<NotificationPage>.Fail("invalid_pagination");
        }

        var mine = db.UserNotifications.AsNoTracking().Where(x => x.UserId == actor.UserId);
        var query = unreadOnly ? mine.Where(x => x.ReadAt == null) : mine;
        if (beforeId > 0)
        {
            query = query.Where(x => x.Id < beforeId);
        }

        // One extra row tells whether there is another page without a second count.
        var rows = await query.OrderByDescending(x => x.Id).Take(pageSize + 1)
            .Select(x => new NotificationResponse(x.Id, x.Type, x.ShipmentId, x.RelatedUserId, x.Subject, x.ActorName, x.CreatedAt, x.ReadAt != null))
            .ToListAsync(cancellationToken);
        var unread = await mine.CountAsync(x => x.ReadAt == null, cancellationToken);
        return new(new NotificationPage(rows.Take(pageSize).ToList(), unread, rows.Count > pageSize));
    }

    public async Task<ShipmentResult<UnreadCountResponse>> UnreadCountAsync(CancellationToken cancellationToken)
    {
        return new(new UnreadCountResponse(await db.UserNotifications.CountAsync(x => x.UserId == actor.UserId && x.ReadAt == null, cancellationToken)));
    }

    public async Task<ShipmentResult<UnreadCountResponse>> MarkReadAsync(IReadOnlyCollection<long>? ids, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var unread = db.UserNotifications.Where(x => x.UserId == actor.UserId && x.ReadAt == null);
        // Ids that belong to someone else simply match nothing, so nobody learns whether they exist.
        if (ids is not null)
        {
            unread = unread.Where(x => ids.Contains(x.Id));
        }

        await unread.ExecuteUpdateAsync(x => x.SetProperty(n => n.ReadAt, now), cancellationToken);
        return await UnreadCountAsync(cancellationToken);
    }
}
