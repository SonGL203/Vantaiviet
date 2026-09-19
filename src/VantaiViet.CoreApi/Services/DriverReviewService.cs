using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class DriverReviewService(CoreDbContext dbContext, TimeProvider timeProvider) : IDriverReviewService
{
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

    private Task AddAuditAsync(Guid actorUserId, string action, Guid targetId, CancellationToken cancellationToken) =>
        dbContext.AuditEvents.AddAsync(new AuditEvent
        {
            ActorUserId = actorUserId,
            Action = action,
            TargetType = "DriverOnboarding",
            TargetId = targetId,
            Outcome = "Succeeded",
            OccurredAt = timeProvider.GetUtcNow()
        }, cancellationToken).AsTask();
}
