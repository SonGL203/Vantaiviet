using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Services;
public sealed class NotificationQueueService(CoreDbContext db,TimeProvider clock,IDataProtectionProvider protection,
    IConfiguration config,IHostEnvironment environment) : INotificationQueueService
{
    public async Task ProcessNextAsync(CancellationToken ct)
    {
        var now=clock.GetUtcNow();
        // Idle polling is one indexed query, avoiding a lock and maintenance writes every tick.
        if(!await db.OtpDeliveries.AnyAsync(x=>
            (x.State=="Pending"&&x.NextAttemptAt<=now)||(x.State=="Processing"&&x.LeaseUntil<=now)||
            (x.ExpiresAt<=now&&x.ProtectedCode!="")||x.CreatedAt<now.AddDays(-7),ct)) { return; }
        OtpDelivery? row;
        await using(var tx=await db.Database.BeginTransactionAsync(ct)){
            // Short claim transaction only. SMTP happens after commit.
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74201919)",ct);
            await db.OtpDeliveries.Where(x=>x.ExpiresAt<=now&&x.ProtectedCode!="")
                .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ProtectedCode,"").SetProperty(x=>x.State,"Expired"),ct);
            await db.OtpDeliveries.Where(x=>x.CreatedAt<now.AddDays(-7)).ExecuteDeleteAsync(ct);
            await db.OtpDeliveries.Where(x=>x.State=="Processing"&&x.LeaseUntil<=now&&x.SendAttempts>=3)
                .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Failed").SetProperty(x=>x.ProtectedCode,""),ct);
            // Global throughput <= 1 attempt per 2s, including multiple worker instances.
            if(await db.OtpDeliveries.AnyAsync(x=>x.LastAttemptAt>now.AddSeconds(-2)||(x.State=="Processing"&&x.LeaseUntil>now),ct))
            {
                return;
            }

            row =await db.OtpDeliveries.Where(x=>x.ConsumedAt==null&&x.ExpiresAt>now&&x.SendAttempts<3&&
                ((x.State=="Pending"&&x.NextAttemptAt<=now)||(x.State=="Processing"&&x.LeaseUntil<=now)))
                .OrderBy(x=>x.NextAttemptAt).ThenBy(x=>x.Id).FirstOrDefaultAsync(ct);
            if(row is null){await tx.CommitAsync(ct);return;}
            row.State="Processing";row.LeaseId=Guid.NewGuid();row.LeaseUntil=now.AddMinutes(1);row.SendAttempts++;row.LastAttemptAt=now;
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        }
        var state="Sent";
        try{
            if(row.Simulated){if(!environment.IsDevelopment())
                {
                    state ="Failed";
                }
            }
            else if(row.Channel!="Email")
            {
                state ="Failed";
            }
            else {
                var host=config["Notifications:Smtp:Host"];
                var from=config["Notifications:Smtp:From"];
                if(string.IsNullOrWhiteSpace(host)||string.IsNullOrWhiteSpace(from))
                {
                    state ="Failed";
                }
                else {
                    var code=protection.CreateProtector("VantaiViet.OtpDelivery.v1").Unprotect(row.ProtectedCode);
                    var purpose = row.Purpose=="PasswordReset" ? "đặt lại mật khẩu" : "xác thực email";
                    using var mail=new MailMessage(from,row.Destination){Subject=$"Vạn Tải Việt - Mã {purpose}",Body=$"Mã {purpose} của bạn: {code}. Mã hết hạn sau 5 phút kể từ lúc yêu cầu. Không chia sẻ mã này. Nếu bạn không yêu cầu, hãy bỏ qua email này."};
                    using var smtp=new SmtpClient(host,config.GetValue("Notifications:Smtp:Port",587)){EnableSsl=true,UseDefaultCredentials=false};
                    if(!string.IsNullOrWhiteSpace(config["Notifications:Smtp:Username"]))
                    {
                        smtp.Credentials=new NetworkCredential(config["Notifications:Smtp:Username"],config["Notifications:Smtp:Password"]);
                    }

                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(20));
                    await smtp.SendMailAsync(mail,timeout.Token);
                }
            }
        }
        catch(SmtpException ex){state=(int)ex.StatusCode>=500?"Failed":"Pending";}
        catch(OperationCanceledException) when(!ct.IsCancellationRequested){state="Pending";}
        catch(System.Security.Cryptography.CryptographicException){state="Failed";}
        catch(FormatException){state="Failed";}
        if(state=="Pending"&&row.SendAttempts>=3)
        {
            state ="Failed";
        }
        // A lease token prevents an expired worker overwriting a later worker or cancellation.
        await db.OtpDeliveries.Where(x=>x.Id==row.Id&&x.LeaseId==row.LeaseId&&x.State=="Processing"&&x.ConsumedAt==null)
            .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,state).SetProperty(x=>x.LeaseUntil,(DateTimeOffset?)null)
                .SetProperty(x=>x.NextAttemptAt,clock.GetUtcNow().AddSeconds(15*row.SendAttempts))
                .SetProperty(x=>x.ProtectedCode,state=="Failed"?"":row.ProtectedCode),ct);
    }
}
