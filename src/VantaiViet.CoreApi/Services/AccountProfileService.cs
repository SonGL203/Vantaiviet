using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class AccountProfileService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IAccountProfileService
{
    private const int MinimumNameLength = 2;

    public async Task<ShipmentResult<AccountProfileResponse>> GetAsync(CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().Where(x => x.Id == actor.UserId)
            .Select(x => new { x.Id, x.DisplayName, x.PhoneNumber, x.PhoneNumberConfirmed, x.Email, x.EmailConfirmed, x.AccountStatus, x.CreatedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return ShipmentResult<AccountProfileResponse>.Fail("not_found");
        }

        var roles = await (from userRole in db.UserRoles
                           join role in db.Roles on userRole.RoleId equals role.Id
                           where userRole.UserId == actor.UserId
                           orderby role.Name
                           select role.Name!).ToListAsync(cancellationToken);
        return new(new AccountProfileResponse(user.Id, user.DisplayName, user.PhoneNumber!, user.PhoneNumberConfirmed,
            user.Email, user.EmailConfirmed, user.AccountStatus, roles, user.CreatedAt));
    }

    public async Task<ShipmentResult<AccountProfileResponse>> UpdateAsync(UpdateAccountProfileRequest request, CancellationToken cancellationToken)
    {
        var name = request.DisplayName.Trim();
        if (name.Length < MinimumNameLength)
        {
            return ShipmentResult<AccountProfileResponse>.Fail("invalid_details");
        }

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == actor.UserId, cancellationToken);
        if (user is null)
        {
            return ShipmentResult<AccountProfileResponse>.Fail("not_found");
        }

        if (user.AccountStatus is "Suspended" or "Disabled")
        {
            return ShipmentResult<AccountProfileResponse>.Fail("forbidden");
        }

        var now = clock.GetUtcNow();
        user.DisplayName = name;
        user.UpdatedAt = now;
        db.AuditEvents.Add(new AuditEvent { ActorUserId = actor.UserId, Action = "account.profile_updated", TargetType = "User", TargetId = user.Id, Outcome = "Succeeded", OccurredAt = now });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return ShipmentResult<AccountProfileResponse>.Fail("conflict");
        }

        return await GetAsync(cancellationToken);
    }
}
