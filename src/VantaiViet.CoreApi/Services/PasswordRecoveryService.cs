using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class PasswordRecoveryService(CoreDbContext db,UserManager<User> users,IDataProtectionProvider protection,
    IConfiguration configuration,TimeProvider clock) : IPasswordRecoveryService
{
    private readonly IDataProtector _protector = protection.CreateProtector("VantaiViet.OtpDelivery.v1");

    public async Task<ShipmentResult<PasswordResetChallenge>> RequestAsync(ForgotPasswordRequest request,CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Notifications:WorkerEnabled",false)
            || configuration.GetValue("Notifications:Email:Simulate",configuration.GetValue("Notifications:Simulate",true))
            || string.IsNullOrWhiteSpace(configuration["Notifications:Smtp:Host"])
            || string.IsNullOrWhiteSpace(configuration["Notifications:Smtp:From"]))
        { return ShipmentResult<PasswordResetChallenge>.Fail("not_configured"); }
        var id = Guid.NewGuid();
        var email = request.Email.Trim().ToUpperInvariant();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74201918)",cancellationToken);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x=>x.NormalizedEmail==email && x.EmailConfirmed
            && (x.AccountStatus=="Active" || x.AccountStatus=="PendingKyc"),cancellationToken);
        if (user is null || user.Email is null) { return new(new(id)); }
        var now = clock.GetUtcNow();
        var destination = user.Email.ToLowerInvariant();
        var limit = configuration.GetValue("Notifications:MaxRequestsPerHour",100);
        var allowed = await db.Database.SqlQuery<bool>($"""SELECT public."CanQueueOtp"({user.Id},{destination},{now},{limit}) AS "Value" """).SingleAsync(cancellationToken);
        // Identical response for missing accounts and throttled recipients prevents account enumeration.
        if (!allowed) { return new(new(id)); }
        await db.OtpDeliveries.Where(x=>x.UserId==user.Id && x.Purpose=="PasswordReset" && x.ConsumedAt==null)
            .ExecuteUpdateAsync(x=>x.SetProperty(d=>d.ConsumedAt,now).SetProperty(d=>d.ProtectedCode,"").SetProperty(d=>d.State,"Superseded"),cancellationToken);
        db.OtpDeliveries.Add(new OtpDelivery
        {
            Id=id,UserId=user.Id,RequestKey=Guid.NewGuid(),Purpose="PasswordReset",Channel="Email",Destination=destination,
            CredentialStamp=user.SecurityStamp,ProtectedCode=_protector.Protect(RandomNumberGenerator.GetInt32(1000000).ToString("D6")),
            CreatedAt=now,ExpiresAt=now.AddMinutes(5),NextAttemptAt=now
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(new(id));
    }

    public async Task<ShipmentResult<bool>> ResetAsync(ResetPasswordRequest request,CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74201918)",cancellationToken);
        var row = await db.OtpDeliveries.FromSqlInterpolated($"""SELECT * FROM public."OtpDeliveries" WHERE "Id"={request.ChallengeId} AND "Purpose"='PasswordReset' FOR UPDATE""")
            .SingleOrDefaultAsync(cancellationToken);
        var now = clock.GetUtcNow();
        if (row is null || row.ConsumedAt is not null || row.ExpiresAt<=now || row.VerifyAttempts>=5 || row.State!="Sent" || row.Simulated)
        { return ShipmentResult<bool>.Fail("invalid_code"); }
        var user = await db.Users.FromSqlInterpolated($"""SELECT * FROM public."Users" WHERE "Id"={row.UserId} FOR UPDATE""").SingleAsync(cancellationToken);
        if (user.SecurityStamp!=row.CredentialStamp || !user.EmailConfirmed || !string.Equals(user.Email,row.Destination,StringComparison.OrdinalIgnoreCase)
            || user.AccountStatus is "Suspended" or "Disabled")
        { return ShipmentResult<bool>.Fail("invalid_code"); }
        row.VerifyAttempts++;
        var correct = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(_protector.Unprotect(row.ProtectedCode)),Encoding.UTF8.GetBytes(request.Code));
        if (!correct)
        {
            if (row.VerifyAttempts>=5) { row.State="Locked";row.ProtectedCode=""; }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ShipmentResult<bool>.Fail("invalid_code");
        }
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user,token,request.NewPassword);
        if (!result.Succeeded) { return ShipmentResult<bool>.Fail("invalid_password"); }
        if (!(await users.ResetAccessFailedCountAsync(user)).Succeeded || !(await users.SetLockoutEndDateAsync(user,null)).Succeeded)
        { return ShipmentResult<bool>.Fail("conflict"); }
        row.ConsumedAt=now;row.ProtectedCode="";
        await db.AuthSessions.Where(x=>x.UserId==user.Id && x.RevokedAt==null)
            .ExecuteUpdateAsync(x=>x.SetProperty(s=>s.RevokedAt,now).SetProperty(s=>s.RevocationReason,"password_reset"),cancellationToken);
        db.AuditEvents.Add(new AuditEvent { ActorUserId=user.Id,TargetType="User",TargetId=user.Id,Action="auth.password_reset",Outcome="Succeeded",OccurredAt=now });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true);
    }
}
