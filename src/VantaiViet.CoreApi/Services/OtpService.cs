using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Services;
public sealed class OtpService(CoreDbContext db,ICurrentActor actor,TimeProvider clock,
    IDataProtectionProvider protection,IHostEnvironment environment,IConfiguration config) : IOtpService
{
    private readonly IDataProtector _protector=protection.CreateProtector("VantaiViet.OtpDelivery.v1");
    private static OtpStatusDto Map(OtpDelivery x)=>new(x.Id,x.Channel,x.State,x.ExpiresAt,x.Simulated);
    private Task<bool> AllowedAsync(CancellationToken ct)=>db.Users.AnyAsync(x=>x.Id==actor.UserId && (x.AccountStatus=="Active"||x.AccountStatus=="PendingKyc"),ct);
    public async Task<ShipmentResult<OtpStatusDto>> RequestAsync(RequestOtpDto r,CancellationToken ct)
    {
        if(!await AllowedAsync(ct))
        {
            return ShipmentResult<OtpStatusDto>.Fail("forbidden");
        }

        var destination=r.Destination.Trim();
        if(r.RequestKey==Guid.Empty || (r.Channel!="Email"&&r.Channel!="Phone") ||
            (r.Channel=="Email" ? !new EmailAddressAttribute().IsValid(destination) : !Regex.IsMatch(destination,@"^\+[1-9][0-9]{7,14}$")))
        {
            return ShipmentResult<OtpStatusDto>.Fail("invalid_destination");
        }

        if (r.Channel=="Email")
        {
            destination =destination.ToLowerInvariant();
        }

        var simulationRequested=config.GetValue($"Notifications:{r.Channel}:Simulate",config.GetValue("Notifications:Simulate",true));
        if(simulationRequested&&!environment.IsDevelopment()) { return ShipmentResult<OtpStatusDto>.Fail("not_configured"); }
        var simulated=environment.IsDevelopment() && simulationRequested;
        if(!simulated && (r.Channel=="Phone" || string.IsNullOrWhiteSpace(config["Notifications:Smtp:Host"]) || string.IsNullOrWhiteSpace(config["Notifications:Smtp:From"])))
        {
            return ShipmentResult<OtpStatusDto>.Fail("not_configured");
        }

        await using var tx=await db.Database.BeginTransactionAsync(ct);
        // Serialize intake globally: cheap short transaction, bounded per user and destination.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74201918)",ct);
        var previous=await db.OtpDeliveries.SingleOrDefaultAsync(x=>x.UserId==actor.UserId&&x.RequestKey==r.RequestKey,ct);
        if(previous is not null)
        {
            return previous.Purpose=="Verification"&&previous.Channel==r.Channel&&previous.Destination==destination ? new(Map(previous)) : ShipmentResult<OtpStatusDto>.Fail("conflict");
        }

        var now=clock.GetUtcNow();
        var limit = config.GetValue("Notifications:MaxRequestsPerHour",100);
        if(!await db.Database.SqlQuery<bool>($"""SELECT public."CanQueueOtp"({actor.UserId},{destination},{now},{limit}) AS "Value" """).SingleAsync(ct))
        {
            return ShipmentResult<OtpStatusDto>.Fail("rate_limited");
        }
        // One current challenge per user/channel. Superseded messages are no longer valid.
        await db.OtpDeliveries.Where(x=>x.UserId==actor.UserId&&x.Purpose=="Verification"&&x.Channel==r.Channel&&x.ConsumedAt==null)
            .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ConsumedAt,now).SetProperty(x=>x.ProtectedCode,"").SetProperty(x=>x.State,"Superseded"),ct);
        var entry=new OtpDelivery {Id=Guid.NewGuid(),UserId=actor.UserId,RequestKey=r.RequestKey,Channel=r.Channel,Destination=destination,
            ProtectedCode=_protector.Protect(RandomNumberGenerator.GetInt32(1000000).ToString("D6")),Simulated=simulated,
            CreatedAt=now,NextAttemptAt=now,ExpiresAt=now.AddMinutes(5)};
        db.OtpDeliveries.Add(entry);
        db.AuditEvents.Add(new AuditEvent {ActorUserId=actor.UserId,Action="verification.requested",TargetType="OtpDelivery",TargetId=entry.Id,Outcome="Succeeded",OccurredAt=now});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return new(Map(entry));
    }
    public async Task<ShipmentResult<OtpStatusDto>> StatusAsync(Guid id,CancellationToken ct)
    {
        if(!await AllowedAsync(ct))
        {
            return ShipmentResult<OtpStatusDto>.Fail("forbidden");
        }

        var row=await db.OtpDeliveries.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.Purpose=="Verification"&&x.UserId==actor.UserId,ct);
        return row is null?ShipmentResult<OtpStatusDto>.Fail("not_found"):new(Map(row));
    }
    public async Task<ShipmentResult<DevelopmentOtpDto>> PreviewAsync(Guid id,CancellationToken ct)
    {
        if(!environment.IsDevelopment()||!await AllowedAsync(ct))
        {
            return ShipmentResult<DevelopmentOtpDto>.Fail("not_found");
        }

        var row=await db.OtpDeliveries.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.Purpose=="Verification"&&x.UserId==actor.UserId&&x.Simulated&&x.State=="Sent"&&x.ConsumedAt==null&&x.ExpiresAt>clock.GetUtcNow(),ct);
        return row is null?ShipmentResult<DevelopmentOtpDto>.Fail("not_found"):new(new(_protector.Unprotect(row.ProtectedCode),row.ExpiresAt));
    }
    public async Task<ShipmentResult<OtpVerificationDto>> VerifyAsync(Guid id,VerifyOtpDto r,CancellationToken ct)
    {
        if(!await AllowedAsync(ct))
        {
            return ShipmentResult<OtpVerificationDto>.Fail("forbidden");
        }

        await using var tx=await db.Database.BeginTransactionAsync(ct);
        // Same lock as intake prevents an older request verifying during replacement.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74201918)",ct);
        var row=await db.OtpDeliveries.FromSqlInterpolated($"""SELECT * FROM public."OtpDeliveries" WHERE "Id"={id} AND "UserId"={actor.UserId} AND "Purpose"='Verification' FOR UPDATE""").SingleOrDefaultAsync(ct);
        if(row is null)
        {
            return ShipmentResult<OtpVerificationDto>.Fail("not_found");
        }

        if (row.ConsumedAt is not null||row.ExpiresAt<=clock.GetUtcNow()||row.VerifyAttempts>=5||row.State!="Sent"||(row.Simulated&&!environment.IsDevelopment()))
        {
            return ShipmentResult<OtpVerificationDto>.Fail("invalid_code");
        }

        row.VerifyAttempts++;
        var valid=CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(_protector.Unprotect(row.ProtectedCode)),Encoding.UTF8.GetBytes(r.Code));
        if(valid){
            row.ConsumedAt=clock.GetUtcNow();row.ProtectedCode="";
            // Fake transport must never certify ownership of a real phone/email.
            if(!row.Simulated){var user=await db.Users.SingleAsync(x=>x.Id==actor.UserId,ct);
                if(row.Channel=="Email"){user.Email=row.Destination;user.NormalizedEmail=row.Destination.ToUpperInvariant();user.EmailConfirmed=true;}
                else if(user.PhoneNumber==row.Destination)
                {
                    user.PhoneNumberConfirmed=true;
                }
            }
        }else if(row.VerifyAttempts>=5){row.ProtectedCode="";row.State="Locked";}
        db.AuditEvents.Add(new AuditEvent {ActorUserId=actor.UserId,Action=row.Simulated?"verification.simulated":"verification.checked",TargetType="OtpDelivery",TargetId=row.Id,Outcome=valid?"Succeeded":"Failed",OccurredAt=clock.GetUtcNow()});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return valid?new(new(!row.Simulated,row.Simulated)):ShipmentResult<OtpVerificationDto>.Fail("invalid_code");
    }
}
