using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class BusinessProfileService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IBusinessProfileService
{
    private const int MinimumNameLength = 2;
    private const int MinimumAddressLength = 5;

    // While a business KYC is pending or approved, the identity of the company must not silently change.
    private static readonly string[] LockingKycStatuses = [KycStatuses.Submitted, KycStatuses.UnderReview, KycStatuses.Approved];

    public async Task<ShipmentResult<BusinessProfileResponse>> GetAsync(CancellationToken cancellationToken)
    {
        var profile = await db.ShipperBusinessProfiles.AsNoTracking().Where(x => x.UserId == actor.UserId)
            .Select(x => new BusinessProfileResponse(x.CompanyName, x.TaxCode, x.Address, x.Version, x.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
        return profile is null ? ShipmentResult<BusinessProfileResponse>.Fail("not_found") : new(profile);
    }

    public async Task<ShipmentResult<BusinessProfileResponse>> UpsertAsync(UpsertBusinessProfileRequest request, CancellationToken cancellationToken)
    {
        var companyName = request.CompanyName.Trim();
        var address = request.Address.Trim();
        var taxCode = request.TaxCode.Trim();
        if (companyName.Length < MinimumNameLength || address.Length < MinimumAddressLength)
        {
            return ShipmentResult<BusinessProfileResponse>.Fail("invalid_details");
        }

        var now = clock.GetUtcNow();
        var profile = await db.ShipperBusinessProfiles.SingleOrDefaultAsync(x => x.UserId == actor.UserId, cancellationToken);
        if (profile is null)
        {
            profile = new ShipperBusinessProfile
            {
                UserId = actor.UserId,
                CompanyName = companyName,
                TaxCode = taxCode,
                Address = address,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.ShipperBusinessProfiles.Add(profile);
        }
        else
        {
            if (request.Version != profile.Version)
            {
                return ShipmentResult<BusinessProfileResponse>.Fail("conflict");
            }

            var identityChanged = profile.CompanyName != companyName || profile.TaxCode != taxCode;
            if (identityChanged && await IsLockedByKycAsync(cancellationToken))
            {
                return ShipmentResult<BusinessProfileResponse>.Fail("locked");
            }

            profile.CompanyName = companyName;
            profile.TaxCode = taxCode;
            profile.Address = address;
            profile.UpdatedAt = now;
            profile.Version++;
        }

        db.AuditEvents.Add(new AuditEvent { ActorUserId = actor.UserId, Action = "business_profile.saved", TargetType = "User", TargetId = actor.UserId, Outcome = "Succeeded", OccurredAt = now });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return ShipmentResult<BusinessProfileResponse>.Fail("conflict");
        }
        catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception))
        {
            db.ChangeTracker.Clear();
            return ShipmentResult<BusinessProfileResponse>.Fail("conflict");
        }

        return new(new BusinessProfileResponse(profile.CompanyName, profile.TaxCode, profile.Address, profile.Version, profile.UpdatedAt));
    }

    private async Task<bool> IsLockedByKycAsync(CancellationToken cancellationToken)
    {
        return await (from application in db.KycApplications
                      join user in db.Users on application.RegistrationApplicationId equals user.RegistrationApplicationId
                      where user.Id == actor.UserId && application.SubjectType == KycSubjects.Business
                          && LockingKycStatuses.Contains(application.Status)
                      select application.Id).AnyAsync(cancellationToken);
    }
}
