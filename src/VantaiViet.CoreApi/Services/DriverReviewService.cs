using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class DriverReviewService(CoreDbContext dbContext, TimeProvider timeProvider) : IDriverReviewService
{
    private static readonly string[] ReviewStatuses = ["Submitted", "Approved", "Rejected"];

    public async Task<IReadOnlyList<DriverProfileListItem>> ListDriverProfilesAsync(string? status, CancellationToken cancellationToken)
    {
        var filter = ValidateStatus(status);
        var profiles = dbContext.DriverProfiles.AsNoTracking().AsQueryable();
        if (filter is not null)
        {
            profiles = profiles.Where(x => x.Status == filter);
        }

        return await (from profile in profiles
                      join account in dbContext.Users.AsNoTracking() on profile.UserId equals account.Id into accounts
                      from user in accounts.DefaultIfEmpty()
                      orderby profile.CreatedAt descending
                      select new DriverProfileListItem(
                          profile.UserId,
                          user == null ? null : user.DisplayName,
                          user == null ? null : user.PhoneNumber,
                          profile.LicenseClass,
                          profile.LicenseExpiresOn,
                          profile.Status,
                          profile.ReviewedAt,
                          profile.ReviewNote,
                          profile.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VehicleListItem>> ListVehiclesAsync(string? status, CancellationToken cancellationToken)
    {
        var filter = ValidateStatus(status);
        var vehicles = dbContext.Vehicles.AsNoTracking().AsQueryable();
        if (filter is not null)
        {
            vehicles = vehicles.Where(x => x.Status == filter);
        }

        return await (from vehicle in vehicles
                      join account in dbContext.Users.AsNoTracking() on vehicle.DriverUserId equals account.Id into accounts
                      from user in accounts.DefaultIfEmpty()
                      orderby vehicle.CreatedAt descending
                      select new VehicleListItem(
                          vehicle.Id,
                          vehicle.DriverUserId,
                          user == null ? null : user.DisplayName,
                          user == null ? null : user.PhoneNumber,
                          vehicle.LicensePlate,
                          vehicle.VehicleType,
                          vehicle.MaxPayloadKg,
                          vehicle.Status,
                          vehicle.ReviewedAt,
                          vehicle.ReviewNote,
                          vehicle.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private static string? ValidateStatus(string? status)
    {
        var filter = status?.Trim();
        if (string.IsNullOrEmpty(filter))
        {
            return null;
        }

        return ReviewStatuses.Contains(filter) ? filter : throw new InvalidOperationException("Unknown review status filter.");
    }

    public async Task<DriverProfileResponse> ReviewProfileAsync(Guid driverUserId, Guid reviewerUserId, ReviewSubmissionRequest request, CancellationToken cancellationToken)
    {
        var profile = await dbContext.DriverProfiles.SingleOrDefaultAsync(x => x.UserId == driverUserId, cancellationToken)
            ?? throw new InvalidOperationException("Driver profile was not found.");
        ApplyReview(profile, reviewerUserId, request);
        await AddAuditAsync(reviewerUserId, "driver_profile.reviewed", driverUserId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new DriverProfileResponse(profile.UserId, profile.LicenseClass, profile.LicenseExpiresOn, profile.Status, profile.ReviewNote);
    }

    public async Task<VehicleResponse> ReviewVehicleAsync(Guid vehicleId, Guid reviewerUserId, ReviewSubmissionRequest request, CancellationToken cancellationToken)
    {
        var vehicle = await dbContext.Vehicles.SingleOrDefaultAsync(x => x.Id == vehicleId, cancellationToken)
            ?? throw new InvalidOperationException("Vehicle was not found.");
        ApplyReview(vehicle, reviewerUserId, request);
        await AddAuditAsync(reviewerUserId, "vehicle.reviewed", vehicle.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new VehicleResponse(vehicle.Id, vehicle.LicensePlate, vehicle.VehicleType, vehicle.MaxPayloadKg, vehicle.Status, vehicle.ReviewNote);
    }

    private void ApplyReview(DriverProfile profile, Guid reviewerUserId, ReviewSubmissionRequest request)
    {
        var now = timeProvider.GetUtcNow();
        profile.Status = request.Approved ? "Approved" : "Rejected";
        profile.ReviewerUserId = reviewerUserId;
        profile.ReviewedAt = now;
        profile.ReviewNote = request.ReviewNote?.Trim();
        profile.UpdatedAt = now;
        profile.Version++;
    }

    private void ApplyReview(Vehicle vehicle, Guid reviewerUserId, ReviewSubmissionRequest request)
    {
        var now = timeProvider.GetUtcNow();
        vehicle.Status = request.Approved ? "Approved" : "Rejected";
        vehicle.ReviewerUserId = reviewerUserId;
        vehicle.ReviewedAt = now;
        vehicle.ReviewNote = request.ReviewNote?.Trim();
        vehicle.UpdatedAt = now;
        vehicle.Version++;
    }

    private Task AddAuditAsync(Guid actorUserId, string action, Guid targetId, CancellationToken cancellationToken)
    {
        return dbContext.AuditEvents.AddAsync(new AuditEvent
        {
            ActorUserId = actorUserId,
            Action = action,
            TargetType = "DriverOnboarding",
            TargetId = targetId,
            Outcome = "Succeeded",
            OccurredAt = timeProvider.GetUtcNow()
        }, cancellationToken).AsTask();
    }

    public async Task<IReadOnlyList<DriverReportListItem>> ListDriverReportsAsync(string? status, CancellationToken cancellationToken)
    {
        var filter = status?.Trim();
        if (string.IsNullOrEmpty(filter))
        {
            filter = null;
        }
        else if (!DriverReportStatuses.All.Contains(filter))
        {
            throw new InvalidOperationException("Unknown driver-report status filter.");
        }

        var reports = dbContext.DriverReports.AsNoTracking().AsQueryable();
        if (filter is not null)
        {
            reports = reports.Where(x => x.Status == filter);
        }

        return await (from report in reports
                      join reporterAccount in dbContext.Users.AsNoTracking() on report.ReporterUserId equals reporterAccount.Id into reporters
                      from reporter in reporters.DefaultIfEmpty()
                      join driverAccount in dbContext.Users.AsNoTracking() on report.DriverUserId equals driverAccount.Id into drivers
                      from driver in drivers.DefaultIfEmpty()
                      orderby report.CreatedAt descending
                      select new DriverReportListItem(
                          report.Id,
                          report.ReporterUserId,
                          reporter == null ? null : reporter.DisplayName,
                          report.DriverUserId,
                          driver == null ? null : driver.DisplayName,
                          report.Category,
                          report.Description,
                          report.Status,
                          report.ReviewerUserId,
                          report.ReviewedAt,
                          report.ReviewNote,
                          report.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<DriverReportListItem> ReviewDriverReportAsync(Guid reportId, Guid reviewerUserId, DriverReportReviewRequest request, CancellationToken cancellationToken)
    {
        if (request.Status is not (DriverReportStatuses.Reviewed or DriverReportStatuses.Dismissed))
        {
            throw new InvalidOperationException("A driver report can only be resolved as Reviewed or Dismissed.");
        }

        var report = await dbContext.DriverReports.SingleOrDefaultAsync(x => x.Id == reportId, cancellationToken)
            ?? throw new InvalidOperationException("Driver report was not found.");
        var now = timeProvider.GetUtcNow();
        report.Status = request.Status;
        report.ReviewerUserId = reviewerUserId;
        report.ReviewedAt = now;
        report.ReviewNote = string.IsNullOrWhiteSpace(request.ReviewNote) ? null : request.ReviewNote.Trim();
        await AddAuditAsync(reviewerUserId, "driver_report.reviewed", reportId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var reporterName = await dbContext.Users.AsNoTracking().Where(x => x.Id == report.ReporterUserId).Select(x => (string?)x.DisplayName).SingleOrDefaultAsync(cancellationToken);
        var driverName = await dbContext.Users.AsNoTracking().Where(x => x.Id == report.DriverUserId).Select(x => (string?)x.DisplayName).SingleOrDefaultAsync(cancellationToken);
        return new DriverReportListItem(report.Id, report.ReporterUserId, reporterName, report.DriverUserId, driverName,
            report.Category, report.Description, report.Status, report.ReviewerUserId, report.ReviewedAt, report.ReviewNote, report.CreatedAt);
    }
}
