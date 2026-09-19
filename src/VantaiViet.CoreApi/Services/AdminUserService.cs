using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class AdminUserService(
    CoreDbContext dbContext,
    UserManager<User> userManager,
    TimeProvider timeProvider) : IAdminUserService
{
    public async Task<IReadOnlyList<AdminUserResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await dbContext.Users.AsNoTracking().OrderBy(x => x.CreatedAt)
            .Select(x => new { x.Id, x.DisplayName, x.PhoneNumber, x.AccountStatus, x.CreatedAt })
            .ToListAsync(cancellationToken);
        var roleRows = await (from userRole in dbContext.UserRoles
                              join role in dbContext.Roles on userRole.RoleId equals role.Id
                              select new { userRole.UserId, Role = role.Name! }).ToListAsync(cancellationToken);
        return users.Select(user => new AdminUserResponse(
            user.Id, user.DisplayName, user.PhoneNumber!, user.AccountStatus,
            roleRows.Where(x => x.UserId == user.Id).Select(x => x.Role).Order().ToList(),
            user.CreatedAt)).ToList();
    }

    public async Task<AdminUserResponse> SetRolesAsync(Guid userId, SetUserRolesRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("User was not found.");
        var requestedRoles = request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var role in requestedRoles)
        {
            if (!await dbContext.Roles.AnyAsync(x => x.NormalizedName == role.ToUpperInvariant(), cancellationToken))
            {
                throw new InvalidOperationException($"Role '{role}' was not found.");
            }
        }
        var currentRoles = await userManager.GetRolesAsync(user);
        var remove = await userManager.RemoveFromRolesAsync(user, currentRoles.Except(requestedRoles, StringComparer.OrdinalIgnoreCase));
        var add = await userManager.AddToRolesAsync(user, requestedRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase));
        if (!remove.Succeeded || !add.Succeeded)
        {
            throw new InvalidOperationException("Unable to update roles.");
        }
        await AddAuditAsync(actorId, "user.roles_changed", user.Id, cancellationToken);
        if (!(await userManager.UpdateSecurityStampAsync(user)).Succeeded)
        { throw new InvalidOperationException("Unable to invalidate old permissions."); }
        return await ToResponseAsync(user, cancellationToken);
    }

    public async Task<AdminUserResponse> SetStatusAsync(Guid userId, SetAccountStatusRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("User was not found.");
        user.AccountStatus = request.Status;
        user.UpdatedAt = timeProvider.GetUtcNow();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Unable to update account status.");
        }
        await AddAuditAsync(actorId, $"user.{request.Status.ToLowerInvariant()}", user.Id, cancellationToken);
        return await ToResponseAsync(user, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEventResponse>> ListAuditEventsAsync(CancellationToken cancellationToken) =>
        await dbContext.AuditEvents.AsNoTracking().OrderByDescending(x => x.OccurredAt).Take(200)
            .Select(x => new AuditEventResponse(x.Id, x.ActorUserId, x.Action, x.TargetType, x.TargetId, x.Outcome, x.OccurredAt))
            .ToListAsync(cancellationToken);

    private async Task AddAuditAsync(Guid actorId, string action, Guid targetId, CancellationToken cancellationToken)
    {
        dbContext.AuditEvents.Add(new AuditEvent
        {
            ActorUserId = actorId, Action = action, TargetType = "User", TargetId = targetId, Outcome = "Succeeded"
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<AdminUserResponse> ToResponseAsync(User user, CancellationToken cancellationToken) =>
        new(user.Id, user.DisplayName, user.PhoneNumber!, user.AccountStatus,
            (await userManager.GetRolesAsync(user)).Order().ToList(), user.CreatedAt);
}
