using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class DriverService(CoreDbContext dbContext, TimeProvider timeProvider) : IDriverService
{
    public async Task<DriverProfileResponse?> GetProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.DriverProfiles.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new DriverProfileResponse(x.UserId, x.LicenseClass, x.LicenseExpiresOn, x.Status, x.ReviewNote))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<DriverProfileResponse> UpsertProfileAsync(Guid userId, UpsertDriverProfileRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var profile = await dbContext.DriverProfiles.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null)
        {
            profile = new DriverProfile { UserId = userId, LicenseClass = request.LicenseClass.Trim(), LicenseExpiresOn = request.LicenseExpiresOn };
            dbContext.DriverProfiles.Add(profile);
        }
        else
        {
            profile.LicenseClass = request.LicenseClass.Trim();
            profile.LicenseExpiresOn = request.LicenseExpiresOn;
            profile.Status = "Submitted";
            profile.ReviewNote = null;
            profile.UpdatedAt = now;
            profile.Version++;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return new DriverProfileResponse(profile.UserId, profile.LicenseClass, profile.LicenseExpiresOn, profile.Status, profile.ReviewNote);
    }

    public async Task<IReadOnlyList<VehicleResponse>> ListVehiclesAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Vehicles.AsNoTracking().Where(x => x.DriverUserId == userId).OrderBy(x => x.CreatedAt)
            .Select(x => new VehicleResponse(x.Id, x.LicensePlate, x.VehicleType, x.MaxPayloadKg, x.Status, x.ReviewNote))
            .ToListAsync(cancellationToken);

    public async Task<VehicleResponse> CreateVehicleAsync(Guid userId, CreateVehicleRequest request, CancellationToken cancellationToken)
    {
        var vehicle = new Vehicle
        {
            DriverUserId = userId,
            LicensePlate = request.LicensePlate.Trim().ToUpperInvariant(),
            VehicleType = request.VehicleType.Trim(),
            MaxPayloadKg = request.MaxPayloadKg
        };
        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new VehicleResponse(vehicle.Id, vehicle.LicensePlate, vehicle.VehicleType, vehicle.MaxPayloadKg, vehicle.Status, vehicle.ReviewNote);
    }

    public async Task<VehicleResponse> UpdateVehicleAsync(Guid userId, Guid vehicleId, UpdateVehicleRequest request, CancellationToken cancellationToken)
    {
        var vehicle = await dbContext.Vehicles.SingleOrDefaultAsync(x => x.Id == vehicleId && x.DriverUserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Vehicle was not found.");
        vehicle.LicensePlate = request.LicensePlate.Trim().ToUpperInvariant();
        vehicle.VehicleType = request.VehicleType.Trim();
        vehicle.MaxPayloadKg = request.MaxPayloadKg;
        vehicle.Status = "Submitted";
        vehicle.ReviewerUserId = null;
        vehicle.ReviewedAt = null;
        vehicle.ReviewNote = null;
        vehicle.UpdatedAt = timeProvider.GetUtcNow();
        vehicle.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new VehicleResponse(vehicle.Id, vehicle.LicensePlate, vehicle.VehicleType, vehicle.MaxPayloadKg, vehicle.Status, vehicle.ReviewNote);
    }

    public async Task DeleteVehicleAsync(Guid userId, Guid vehicleId, CancellationToken cancellationToken)
    {
        var vehicle = await dbContext.Vehicles.SingleOrDefaultAsync(x => x.Id == vehicleId && x.DriverUserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Vehicle was not found.");
        dbContext.Vehicles.Remove(vehicle);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
