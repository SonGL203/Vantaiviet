using Microsoft.EntityFrameworkCore;
using Npgsql;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

/// <summary>A broker's own network: named groups (feature 16), drivers they save and favourite (features 14-15),
/// and who has joined through their invite code (feature 17).</summary>
public sealed class DriverNetworkService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IDriverNetworkService
{
    private const int MaxGroupNameLength = 60;

    private static DriverGroupResponse MapGroup(DriverGroup g, int count) => new(g.Id, g.Name, count, g.CreatedAt);

    public async Task<ShipmentResult<DriverGroupResponse>> CreateGroupAsync(DriverGroupRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length == 0 || name.Length > MaxGroupNameLength)
        {
            return ShipmentResult<DriverGroupResponse>.Fail("invalid_details");
        }

        if (await db.DriverGroups.AnyAsync(x => x.BrokerUserId == actor.UserId && x.Name.ToLower() == name.ToLower(), cancellationToken))
        {
            return ShipmentResult<DriverGroupResponse>.Fail("duplicate_name");
        }

        var group = new DriverGroup { Id = Guid.NewGuid(), BrokerUserId = actor.UserId, Name = name, CreatedAt = clock.GetUtcNow() };
        db.DriverGroups.Add(group);
        await db.SaveChangesAsync(cancellationToken);
        return new(MapGroup(group, 0));
    }

    public async Task<ShipmentResult<IReadOnlyList<DriverGroupResponse>>> ListGroupsAsync(CancellationToken cancellationToken)
    {
        var groups = await db.DriverGroups.AsNoTracking().Where(x => x.BrokerUserId == actor.UserId)
            .OrderBy(x => x.Name).Take(200).ToListAsync(cancellationToken);
        var counts = await db.SavedDrivers.AsNoTracking().Where(x => x.BrokerUserId == actor.UserId && x.GroupId != null)
            .GroupBy(x => x.GroupId!.Value).Select(g => new { GroupId = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var byGroup = counts.ToDictionary(x => x.GroupId, x => x.Count);
        return new(groups.Select(g => MapGroup(g, byGroup.GetValueOrDefault(g.Id))).ToList());
    }

    public async Task<ShipmentResult<DriverGroupResponse>> RenameGroupAsync(Guid id, DriverGroupRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length == 0 || name.Length > MaxGroupNameLength)
        {
            return ShipmentResult<DriverGroupResponse>.Fail("invalid_details");
        }

        var group = await db.DriverGroups.SingleOrDefaultAsync(x => x.Id == id && x.BrokerUserId == actor.UserId, cancellationToken);
        if (group is null) { return ShipmentResult<DriverGroupResponse>.Fail("not_found"); }
        if (await db.DriverGroups.AnyAsync(x => x.BrokerUserId == actor.UserId && x.Id != id && x.Name.ToLower() == name.ToLower(), cancellationToken))
        {
            return ShipmentResult<DriverGroupResponse>.Fail("duplicate_name");
        }

        group.Name = name;
        await db.SaveChangesAsync(cancellationToken);
        var count = await db.SavedDrivers.CountAsync(x => x.GroupId == id, cancellationToken);
        return new(MapGroup(group, count));
    }

    public async Task<ShipmentResult<bool>> DeleteGroupAsync(Guid id, CancellationToken cancellationToken)
    {
        var group = await db.DriverGroups.SingleOrDefaultAsync(x => x.Id == id && x.BrokerUserId == actor.UserId, cancellationToken);
        if (group is null) { return ShipmentResult<bool>.Fail("not_found"); }
        // Drivers in the group are kept, just unassigned; only the folder goes away.
        await db.SavedDrivers.Where(x => x.GroupId == id).ExecuteUpdateAsync(x => x.SetProperty(d => d.GroupId, (Guid?)null), cancellationToken);
        db.DriverGroups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);
        return new(true);
    }

    private async Task<Dictionary<Guid, RatingSummary>> RatingsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var rows = await db.TripReviews.AsNoTracking().Where(x => userIds.Contains(x.TargetUserId)).GroupBy(x => x.TargetUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count(), Average = g.Average(x => x.Stars) }).ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.UserId, x => new RatingSummary(Math.Round(x.Average, 1), x.Count));
    }

    private async Task<Dictionary<Guid, int>> CompletedTripsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var rows = await db.Trips.AsNoTracking().Where(x => x.Status == TripStates.Completed && userIds.Contains(x.DriverUserId))
            .GroupBy(x => x.DriverUserId).Select(g => new { UserId = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.UserId, x => x.Count);
    }

    public async Task<ShipmentResult<SavedDriverResponse>> SaveAsync(SaveDriverRequest request, CancellationToken cancellationToken)
    {
        var isDriver = await (from u in db.Users join ur in db.UserRoles on u.Id equals ur.UserId join r in db.Roles on ur.RoleId equals r.Id
            where u.Id == request.DriverUserId && r.NormalizedName == "DRIVER" select u.Id).AnyAsync(cancellationToken);
        if (!isDriver) { return ShipmentResult<SavedDriverResponse>.Fail("driver_not_found"); }

        if (request.GroupId is { } groupId && !await db.DriverGroups.AnyAsync(x => x.Id == groupId && x.BrokerUserId == actor.UserId, cancellationToken))
        {
            return ShipmentResult<SavedDriverResponse>.Fail("invalid_group");
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        var now = clock.GetUtcNow();
        var saved = new SavedDriver
        {
            Id = Guid.NewGuid(), BrokerUserId = actor.UserId, DriverUserId = request.DriverUserId,
            GroupId = request.GroupId, IsFavorite = request.IsFavorite, Note = note, CreatedAt = now, UpdatedAt = now
        };
        db.SavedDrivers.Add(saved);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ShipmentResult<SavedDriverResponse>.Fail("already_saved");
        }

        return new(await MapSavedAsync(saved, cancellationToken));
    }

    private async Task<SavedDriverResponse> MapSavedAsync(SavedDriver s, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().Where(x => x.Id == s.DriverUserId).Select(x => new { x.DisplayName, x.PhoneNumber }).SingleAsync(cancellationToken);
        var groupName = s.GroupId is null ? null : await db.DriverGroups.AsNoTracking().Where(x => x.Id == s.GroupId).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken);
        var rating = (await RatingsAsync([s.DriverUserId], cancellationToken)).GetValueOrDefault(s.DriverUserId, new RatingSummary(null, 0));
        var trips = (await CompletedTripsAsync([s.DriverUserId], cancellationToken)).GetValueOrDefault(s.DriverUserId);
        return new(s.Id, s.DriverUserId, user.DisplayName, user.PhoneNumber ?? "", s.GroupId, groupName, s.IsFavorite, s.Note, rating, trips, s.Version, s.CreatedAt);
    }

    public async Task<ShipmentResult<IReadOnlyList<SavedDriverResponse>>> ListAsync(Guid? groupId, bool favoritesOnly, CancellationToken cancellationToken)
    {
        var query = db.SavedDrivers.AsNoTracking().Where(x => x.BrokerUserId == actor.UserId);
        if (groupId is { } g) { query = query.Where(x => x.GroupId == g); }
        if (favoritesOnly) { query = query.Where(x => x.IsFavorite); }
        var rows = await query.OrderByDescending(x => x.IsFavorite).ThenByDescending(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
        if (rows.Count == 0) { return new(Array.Empty<SavedDriverResponse>()); }

        var driverIds = rows.Select(x => x.DriverUserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(x => driverIds.Contains(x.Id)).Select(x => new { x.Id, x.DisplayName, x.PhoneNumber }).ToDictionaryAsync(x => x.Id, cancellationToken);
        var groupIds = rows.Where(x => x.GroupId != null).Select(x => x.GroupId!.Value).Distinct().ToList();
        var groups = await db.DriverGroups.AsNoTracking().Where(x => groupIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var ratings = await RatingsAsync(driverIds, cancellationToken);
        var trips = await CompletedTripsAsync(driverIds, cancellationToken);

        return new(rows.Select(s =>
        {
            var user = users[s.DriverUserId];
            var groupName = s.GroupId is { } gid ? groups.GetValueOrDefault(gid) : null;
            var rating = ratings.GetValueOrDefault(s.DriverUserId, new RatingSummary(null, 0));
            return new SavedDriverResponse(s.Id, s.DriverUserId, user.DisplayName, user.PhoneNumber ?? "", s.GroupId, groupName,
                s.IsFavorite, s.Note, rating, trips.GetValueOrDefault(s.DriverUserId), s.Version, s.CreatedAt);
        }).ToList());
    }

    public async Task<ShipmentResult<SavedDriverResponse>> UpdateAsync(Guid id, UpdateSavedDriverRequest request, CancellationToken cancellationToken)
    {
        var saved = await db.SavedDrivers.SingleOrDefaultAsync(x => x.Id == id && x.BrokerUserId == actor.UserId, cancellationToken);
        if (saved is null) { return ShipmentResult<SavedDriverResponse>.Fail("not_found"); }
        if (saved.Version != request.Version) { return ShipmentResult<SavedDriverResponse>.Fail("conflict"); }
        if (request.GroupId is { } groupId && !await db.DriverGroups.AnyAsync(x => x.Id == groupId && x.BrokerUserId == actor.UserId, cancellationToken))
        {
            return ShipmentResult<SavedDriverResponse>.Fail("invalid_group");
        }

        saved.GroupId = request.GroupId;
        saved.IsFavorite = request.IsFavorite;
        saved.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        saved.UpdatedAt = clock.GetUtcNow();
        saved.Version++;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return ShipmentResult<SavedDriverResponse>.Fail("conflict"); }
        return new(await MapSavedAsync(saved, cancellationToken));
    }

    public async Task<ShipmentResult<bool>> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var saved = await db.SavedDrivers.SingleOrDefaultAsync(x => x.Id == id && x.BrokerUserId == actor.UserId, cancellationToken);
        if (saved is null) { return ShipmentResult<bool>.Fail("not_found"); }
        db.SavedDrivers.Remove(saved);
        await db.SaveChangesAsync(cancellationToken);
        return new(true);
    }

    public Task<ShipmentResult<InviteCodeResponse>> InviteCodeAsync(CancellationToken cancellationToken)
    {
        // The broker's own user id, unadorned: resolvable with no extra storage or generation step.
        // A short link or QR code is a natural later upgrade; this is enough to invite someone today.
        return Task.FromResult(new ShipmentResult<InviteCodeResponse>(new InviteCodeResponse(actor.UserId.ToString("N"))));
    }

    public async Task<ShipmentResult<IReadOnlyList<ReferredDriverResponse>>> ReferralsAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Users.AsNoTracking().Where(x => x.ReferredByUserId == actor.UserId)
            .OrderByDescending(x => x.CreatedAt).Take(200)
            .Select(x => new ReferredDriverResponse(x.Id, x.DisplayName, x.PhoneNumber ?? "", x.CreatedAt)).ToListAsync(cancellationToken);
        return new(rows);
    }

    public async Task<ShipmentResult<DriverReportResponse>> ReportDriverAsync(Guid driverUserId, DriverReportRequest request, CancellationToken cancellationToken)
    {
        if (!DriverReportCategories.All.Contains(request.Category))
        {
            return ShipmentResult<DriverReportResponse>.Fail("invalid_category");
        }

        var isDriver = await (from u in db.Users join ur in db.UserRoles on u.Id equals ur.UserId join r in db.Roles on ur.RoleId equals r.Id
            where u.Id == driverUserId && r.NormalizedName == "DRIVER" select u.Id).AnyAsync(cancellationToken);
        if (!isDriver) { return ShipmentResult<DriverReportResponse>.Fail("driver_not_found"); }

        var report = new DriverReport
        {
            Id = Guid.NewGuid(), ReporterUserId = actor.UserId, DriverUserId = driverUserId,
            Category = request.Category, Description = request.Description.Trim(), CreatedAt = clock.GetUtcNow()
        };
        db.DriverReports.Add(report);
        db.AuditEvents.Add(new AuditEvent { ActorUserId = actor.UserId, Action = "driver_report.created", TargetType = "DriverReport", TargetId = report.Id, Outcome = "Succeeded", OccurredAt = report.CreatedAt });
        await db.SaveChangesAsync(cancellationToken);
        var name = await db.Users.AsNoTracking().Where(x => x.Id == driverUserId).Select(x => x.DisplayName).SingleAsync(cancellationToken);
        return new(new DriverReportResponse(report.Id, driverUserId, name, report.Category, report.Description, report.CreatedAt));
    }

    public async Task<ShipmentResult<IReadOnlyList<DriverReportResponse>>> MyReportsAsync(CancellationToken cancellationToken)
    {
        var rows = await db.DriverReports.AsNoTracking().Where(x => x.ReporterUserId == actor.UserId)
            .OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
        if (rows.Count == 0) { return new(Array.Empty<DriverReportResponse>()); }

        var ids = rows.Select(x => x.DriverUserId).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        return new(rows.Select(x => new DriverReportResponse(x.Id, x.DriverUserId, names.GetValueOrDefault(x.DriverUserId, string.Empty), x.Category, x.Description, x.CreatedAt)).ToList());
    }
}
