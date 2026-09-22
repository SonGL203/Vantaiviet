using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class AccountKycService(CoreDbContext db, ICurrentActor actor, TimeProvider clock,
    IDataProtectionProvider protection, IConfiguration configuration) : IAccountKycService
{
    private const string ProtectorPurpose = "VantaiViet.Kyc.IdentityNumber.v1";
    private const string EncryptionKeyId = "dataprotection-v1";
    private const string HmacKeyId = "hmac-v1";
    private const int MinimumAge = 18;
    private const int MinimumNameLength = 2;
    private const int MinimumHmacKeyLength = 32;
    private const int MaskedVisibleDigits = 4;
    private const char MaskCharacter = '*';
    // Serializes submissions so one identity number cannot be claimed by two accounts at the same moment.
    private const long SubmissionLockKey = 74201920;

    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static readonly DateOnly EarliestBirthDate = new(1900, 1, 1);
    private static readonly string[] EditableStatuses = [KycStatuses.Draft, KycStatuses.Rejected];
    private static readonly string[] ClaimingStatuses = [KycStatuses.Submitted, KycStatuses.UnderReview, KycStatuses.Approved];

    private readonly IDataProtector _protector = protection.CreateProtector(ProtectorPurpose);

    private sealed record Account(Guid RegistrationApplicationId, string AccountStatus);

    public async Task<ShipmentResult<KycStatusResponse>> GetAsync(CancellationToken cancellationToken)
    {
        var account = await LoadAccountAsync(cancellationToken);
        if (account is null)
        {
            return Fail("forbidden");
        }

        var application = await db.KycApplications.AsNoTracking()
            .Where(x => x.RegistrationApplicationId == account.RegistrationApplicationId)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return new(await BuildStatusAsync(application, account.AccountStatus, cancellationToken));
    }

    public async Task<ShipmentResult<KycStatusResponse>> SaveIdentityAsync(SaveKycIdentityRequest request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var issue = IdentityIssue(request, Today(now));
        if (issue is not null)
        {
            return Fail(issue);
        }

        var hmacKey = configuration["Kyc:IdentityHmacKey"];
        if (string.IsNullOrWhiteSpace(hmacKey) || hmacKey.Length < MinimumHmacKeyLength)
        {
            return Fail("not_configured");
        }

        var account = await LoadAccountAsync(cancellationToken);
        if (account is null)
        {
            return Fail("forbidden");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var application = await LatestTrackedAsync(account.RegistrationApplicationId, cancellationToken);
        if (application is not null && !EditableStatuses.Contains(application.Status))
        {
            return Fail("conflict");
        }

        var identityBytes = Encoding.UTF8.GetBytes(request.IdentityNumber);
        var encrypted = _protector.Protect(identityBytes);
        var hmac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(hmacKey), identityBytes);
        KycIdentityDetail? detail = null;
        if (application is null || application.Status == KycStatuses.Rejected)
        {
            // A rejected application stays as history; the applicant starts a fresh draft.
            application = new KycApplication
            {
                Id = Guid.NewGuid(),
                RegistrationApplicationId = account.RegistrationApplicationId,
                Status = KycStatuses.Draft,
                SubjectType = request.SubjectType,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.KycApplications.Add(application);
        }
        else
        {
            application.SubjectType = request.SubjectType;
            application.UpdatedAt = now;
            application.Version++;
            detail = await db.KycIdentityDetails.SingleOrDefaultAsync(x => x.ApplicationId == application.Id, cancellationToken);
            if (request.SubjectType != KycSubjects.Business)
            {
                await db.KycDocuments.Where(x => x.ApplicationId == application.Id && x.DocumentType == KycDocumentTypes.BusinessLicense)
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }

        if (detail is null)
        {
            detail = new KycIdentityDetail
            {
                ApplicationId = application.Id,
                FullName = request.FullName.Trim(),
                DateOfBirth = request.DateOfBirth,
                IdentityNumberEncrypted = encrypted,
                EncryptionKeyId = EncryptionKeyId,
                IdentityNumberHmac = hmac,
                HmacKeyId = HmacKeyId,
                IssuedOn = request.IssuedOn,
                ExpiresOn = request.ExpiresOn
            };
            db.KycIdentityDetails.Add(detail);
        }
        else
        {
            detail.FullName = request.FullName.Trim();
            detail.DateOfBirth = request.DateOfBirth;
            detail.IdentityNumberEncrypted = encrypted;
            detail.EncryptionKeyId = EncryptionKeyId;
            detail.IdentityNumberHmac = hmac;
            detail.HmacKeyId = HmacKeyId;
            detail.IssuedOn = request.IssuedOn;
            detail.ExpiresOn = request.ExpiresOn;
        }

        AddAudit("kyc.identity_saved", application.Id, now);
        if (!await TryCommitAsync(transaction, cancellationToken))
        {
            return Fail("conflict");
        }

        return new(await BuildStatusAsync(application, account.AccountStatus, cancellationToken));
    }

    public async Task<ShipmentResult<KycStatusResponse>> UploadDocumentAsync(string documentType, UploadKycDocumentRequest request, CancellationToken cancellationToken)
    {
        if (!KycDocumentTypes.All.Contains(documentType))
        {
            return Fail("invalid_document_type");
        }

        if (!ImageContent.IsSupported(request.ContentType, request.Content))
        {
            return Fail("invalid_image");
        }

        var account = await LoadAccountAsync(cancellationToken);
        if (account is null)
        {
            return Fail("forbidden");
        }

        var application = await LatestTrackedAsync(account.RegistrationApplicationId, cancellationToken);
        if (application is null)
        {
            return Fail("identity_required");
        }

        if (application.Status != KycStatuses.Draft)
        {
            return Fail("conflict");
        }

        if (documentType == KycDocumentTypes.BusinessLicense && application.SubjectType != KycSubjects.Business)
        {
            return Fail("invalid_document_type");
        }

        var now = clock.GetUtcNow();
        var document = await db.KycDocuments.SingleOrDefaultAsync(x => x.ApplicationId == application.Id && x.DocumentType == documentType, cancellationToken);
        if (document is null)
        {
            db.KycDocuments.Add(new KycDocument
            {
                Id = Guid.NewGuid(),
                ApplicationId = application.Id,
                DocumentType = documentType,
                StorageKey = $"kyc/{application.Id}/{documentType}",
                ContentType = request.ContentType,
                SizeBytes = request.Content.Length,
                Content = request.Content,
                UploadedAt = now
            });
        }
        else
        {
            document.ContentType = request.ContentType;
            document.SizeBytes = request.Content.Length;
            document.Content = request.Content;
            document.UploadedAt = now;
            document.DeletedAt = null;
        }

        // Bumping the version makes an upload racing a submission fail one of the two instead of slipping in unreviewed.
        application.UpdatedAt = now;
        application.Version++;
        AddAudit("kyc.document_uploaded", application.Id, now);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return Fail("conflict");
        }
        catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception))
        {
            db.ChangeTracker.Clear();
            return Fail("conflict");
        }

        return new(await BuildStatusAsync(application, account.AccountStatus, cancellationToken));
    }

    public async Task<ShipmentResult<KycStatusResponse>> SubmitAsync(CancellationToken cancellationToken)
    {
        var account = await LoadAccountAsync(cancellationToken);
        if (account is null)
        {
            return Fail("forbidden");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({SubmissionLockKey})", cancellationToken);
        var application = await LatestTrackedAsync(account.RegistrationApplicationId, cancellationToken);
        if (application is null)
        {
            return Fail("identity_required");
        }

        if (application.Status != KycStatuses.Draft)
        {
            return Fail("conflict");
        }

        var detail = await db.KycIdentityDetails.AsNoTracking().SingleOrDefaultAsync(x => x.ApplicationId == application.Id, cancellationToken);
        if (detail is null)
        {
            return Fail("identity_required");
        }

        var uploaded = await db.KycDocuments.AsNoTracking()
            .Where(x => x.ApplicationId == application.Id && x.DeletedAt == null && x.Content != null)
            .Select(x => x.DocumentType).ToListAsync(cancellationToken);
        if (KycDocumentTypes.RequiredFor(application.SubjectType).Any(type => !uploaded.Contains(type)))
        {
            return Fail("incomplete");
        }

        if (application.SubjectType == KycSubjects.Business
            && !await db.ShipperBusinessProfiles.AnyAsync(x => x.UserId == actor.UserId, cancellationToken))
        {
            return Fail("business_profile_required");
        }

        var claimedElsewhere = await (from other in db.KycIdentityDetails
                                      join otherApplication in db.KycApplications on other.ApplicationId equals otherApplication.Id
                                      where other.HmacKeyId == detail.HmacKeyId && other.IdentityNumberHmac == detail.IdentityNumberHmac
                                          && otherApplication.RegistrationApplicationId != account.RegistrationApplicationId
                                          && ClaimingStatuses.Contains(otherApplication.Status)
                                      select other.ApplicationId).AnyAsync(cancellationToken);
        if (claimedElsewhere)
        {
            return Fail("identity_unavailable");
        }

        var now = clock.GetUtcNow();
        application.Status = KycStatuses.Submitted;
        application.SubmittedAt = now;
        application.UpdatedAt = now;
        application.Version++;
        AddAudit("kyc.submitted", application.Id, now);
        if (!await TryCommitAsync(transaction, cancellationToken))
        {
            return Fail("conflict");
        }

        return new(await BuildStatusAsync(application, account.AccountStatus, cancellationToken));
    }

    private static ShipmentResult<KycStatusResponse> Fail(string code)
    {
        return ShipmentResult<KycStatusResponse>.Fail(code);
    }

    private static DateOnly Today(DateTimeOffset now)
    {
        return DateOnly.FromDateTime(now.ToOffset(VietnamOffset).DateTime);
    }

    private static string? IdentityIssue(SaveKycIdentityRequest request, DateOnly today)
    {
        if (request.FullName.Trim().Length < MinimumNameLength)
        {
            return "invalid_details";
        }

        if (request.DateOfBirth < EarliestBirthDate || request.DateOfBirth > today.AddYears(-MinimumAge))
        {
            return "invalid_details";
        }

        if (request.IssuedOn is { } issuedOn && (issuedOn < request.DateOfBirth || issuedOn > today))
        {
            return "invalid_details";
        }

        if (request.ExpiresOn is { } expiresOn)
        {
            if (request.IssuedOn is { } issued && expiresOn < issued)
            {
                return "invalid_details";
            }

            if (expiresOn < today)
            {
                return "document_expired";
            }
        }

        return null;
    }

    private static string Mask(string number)
    {
        var visible = Math.Min(MaskedVisibleDigits, number.Length);
        return new string(MaskCharacter, number.Length - visible) + number[^visible..];
    }

    private Task<Account?> LoadAccountAsync(CancellationToken cancellationToken)
    {
        return db.Users.AsNoTracking()
            .Where(x => x.Id == actor.UserId && (x.AccountStatus == "Active" || x.AccountStatus == "PendingKyc"))
            .Select(x => new Account(x.RegistrationApplicationId, x.AccountStatus))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private Task<KycApplication?> LatestTrackedAsync(Guid registrationApplicationId, CancellationToken cancellationToken)
    {
        return db.KycApplications.Where(x => x.RegistrationApplicationId == registrationApplicationId)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private void AddAudit(string action, Guid applicationId, DateTimeOffset now)
    {
        db.AuditEvents.Add(new AuditEvent { ActorUserId = actor.UserId, Action = action, TargetType = "KycApplication", TargetId = applicationId, Outcome = "Succeeded", OccurredAt = now });
    }

    private async Task<bool> TryCommitAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
        catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception))
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    private async Task<KycStatusResponse> BuildStatusAsync(KycApplication? application, string accountStatus, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            return new KycStatusResponse(null, KycStatuses.None, null, null, null, null, null, null, [], null, null, null, null, accountStatus);
        }

        var detail = await db.KycIdentityDetails.AsNoTracking().SingleOrDefaultAsync(x => x.ApplicationId == application.Id, cancellationToken);
        var documents = await db.KycDocuments.AsNoTracking()
            .Where(x => x.ApplicationId == application.Id && x.DeletedAt == null && x.Content != null)
            .OrderBy(x => x.DocumentType)
            .Select(x => new KycDocumentInfo(x.DocumentType, x.ContentType, x.SizeBytes, x.UploadedAt))
            .ToListAsync(cancellationToken);
        return new KycStatusResponse(application.Id, application.Status, application.SubjectType,
            detail?.FullName, detail?.DateOfBirth, detail is null ? null : MaskedNumber(detail), detail?.IssuedOn, detail?.ExpiresOn,
            documents, application.SubmittedAt, application.ReviewedAt, application.RejectionCode, application.ReviewNote, accountStatus);
    }

    private string? MaskedNumber(KycIdentityDetail detail)
    {
        try
        {
            return Mask(Encoding.UTF8.GetString(_protector.Unprotect(detail.IdentityNumberEncrypted)));
        }
        catch (CryptographicException)
        {
            // A lost key ring must not make the whole status unreadable.
            return null;
        }
    }
}
