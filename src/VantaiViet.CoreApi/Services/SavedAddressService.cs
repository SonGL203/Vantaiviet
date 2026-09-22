using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

/// <summary>The places a shipper uses again and again, so they need not be typed or searched for every time.</summary>
public sealed class SavedAddressService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : ISavedAddressService
{
    private const int MinAddressLength = 3;

    private static SavedAddressResponse Map(SavedAddress a)
    {
        return new(a.Id, a.Label, a.Address, a.Latitude.HasValue ? new CoordinateDto(a.Latitude.Value, a.Longitude!.Value) : null, a.Version);
    }

    private static bool ValidPoint(CoordinateDto? p)
    {
        return p is null || (double.IsFinite(p.Latitude) && double.IsFinite(p.Longitude) && p.Latitude is >= -90 and <= 90 && p.Longitude is >= -180 and <= 180);
    }

    private Task<List<SavedAddressResponse>> AllAsync(CancellationToken cancellationToken)
    {
        return db.SavedAddresses.AsNoTracking().Where(x => x.OwnerUserId == actor.UserId).OrderBy(x => x.Label).ThenBy(x => x.Id)
            .Select(x => new SavedAddressResponse(x.Id, x.Label, x.Address, x.Latitude == null ? null : new CoordinateDto(x.Latitude.Value, x.Longitude!.Value), x.Version))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Text checks shared by create and update; returns the cleaned label and address, or the reason it was refused.</summary>
    private static (string? Label, string? Address, string? Error) Clean(SaveAddressRequest r)
    {
        var label = r.Label?.Trim();
        var address = r.Address?.Trim();
        if (string.IsNullOrEmpty(label) || label.Length > FollowUpLimits.MaxAddressLabel || string.IsNullOrEmpty(address)
            || address.Length < MinAddressLength || address.Length > FollowUpLimits.MaxAddressLength || !ValidPoint(r.Point))
        {
            return (null, null, "invalid_details");
        }

        return (label, address, null);
    }

    public async Task<ShipmentResult<IReadOnlyList<SavedAddressResponse>>> ListAsync(CancellationToken cancellationToken)
    {
        return new(await AllAsync(cancellationToken));
    }

    public async Task<ShipmentResult<SavedAddressResponse>> CreateAsync(SaveAddressRequest request, CancellationToken cancellationToken)
    {
        var (label, address, error) = Clean(request);
        if (error is not null)
        {
            return ShipmentResult<SavedAddressResponse>.Fail(error);
        }

        var mine = db.SavedAddresses.Where(x => x.OwnerUserId == actor.UserId);
        if (await mine.CountAsync(cancellationToken) >= FollowUpLimits.MaxSavedAddresses)
        {
            return ShipmentResult<SavedAddressResponse>.Fail("limit_reached");
        }

        if (await mine.AnyAsync(x => x.Label.ToLower() == label!.ToLower(), cancellationToken))
        {
            return ShipmentResult<SavedAddressResponse>.Fail("duplicate_label");
        }

        var now = clock.GetUtcNow();
        var saved = new SavedAddress { Id = Guid.NewGuid(), OwnerUserId = actor.UserId, Label = label!, Address = address!,
            Latitude = request.Point?.Latitude, Longitude = request.Point?.Longitude, CreatedAt = now, UpdatedAt = now };
        db.SavedAddresses.Add(saved);
        await db.SaveChangesAsync(cancellationToken);
        return new(Map(saved));
    }

    public async Task<ShipmentResult<SavedAddressResponse>> UpdateAsync(Guid id, SaveAddressRequest request, CancellationToken cancellationToken)
    {
        var (label, address, error) = Clean(request);
        if (error is not null || request.Version is null)
        {
            return ShipmentResult<SavedAddressResponse>.Fail(error ?? "invalid_details");
        }

        var saved = await db.SavedAddresses.SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == actor.UserId, cancellationToken);
        if (saved is null)
        {
            return ShipmentResult<SavedAddressResponse>.Fail("not_found");
        }

        if (saved.Version != request.Version)
        {
            return ShipmentResult<SavedAddressResponse>.Fail("conflict");
        }

        if (await db.SavedAddresses.AnyAsync(x => x.OwnerUserId == actor.UserId && x.Id != id && x.Label.ToLower() == label!.ToLower(), cancellationToken))
        {
            return ShipmentResult<SavedAddressResponse>.Fail("duplicate_label");
        }

        saved.Label = label!;
        saved.Address = address!;
        saved.Latitude = request.Point?.Latitude;
        saved.Longitude = request.Point?.Longitude;
        saved.Version++;
        saved.UpdatedAt = clock.GetUtcNow();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return ShipmentResult<SavedAddressResponse>.Fail("conflict");
        }

        return new(Map(saved));
    }

    public async Task<ShipmentResult<IReadOnlyList<SavedAddressResponse>>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var removed = await db.SavedAddresses.Where(x => x.Id == id && x.OwnerUserId == actor.UserId).ExecuteDeleteAsync(cancellationToken);
        if (removed == 0)
        {
            return ShipmentResult<IReadOnlyList<SavedAddressResponse>>.Fail("not_found");
        }

        return new(await AllAsync(cancellationToken));
    }
}
