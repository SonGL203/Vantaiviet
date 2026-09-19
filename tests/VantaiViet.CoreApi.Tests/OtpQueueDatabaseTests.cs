using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Npgsql;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services;
using VantaiViet.CoreApi.Services.Interfaces;
using Xunit;
namespace VantaiViet.CoreApi.Tests;
public sealed class OtpQueueDatabaseTests
{
    private sealed record Actor(Guid UserId):ICurrentActor;
    private sealed class Clock:TimeProvider
    {
        public DateTimeOffset Now {get;set;}=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>Now;
    }
    [PostgreSqlFact]
    public async Task QueueDeduplicationLimitsVerificationAndLeaseRecoveryAsync()
    {
        var source=Environment.GetEnvironmentVariable("CORE_DATABASE_TEST_CONNECTION");
        var builder=new NpgsqlConnectionStringBuilder(source);
        var name="otp_test_"+Guid.NewGuid().ToString("N");
        await using var admin=new NpgsqlConnection(builder.ConnectionString);await admin.OpenAsync();
        await using(var create=new NpgsqlCommand($"CREATE DATABASE \"{name}\"",admin)){await create.ExecuteNonQueryAsync();}
        builder.Database=name;builder.Pooling=false;
        try{
            var options=new DbContextOptionsBuilder<CoreDbContext>().UseNpgsql(builder.ConnectionString).Options;
            await using var db=new CoreDbContext(options);await db.Database.MigrateAsync();
            var reg=new RegistrationApplication {Id=Guid.NewGuid(),PhoneNumber="+84999900918",PhoneVerifiedAt=DateTimeOffset.UtcNow,DisplayName="Queue test",Status="Completed",ExpiresAt=DateTimeOffset.UtcNow.AddDays(1),CompletedAt=DateTimeOffset.UtcNow};
            db.RegistrationApplications.Add(reg);
            var user=new User {Id=Guid.NewGuid(),RegistrationApplicationId=reg.Id,DisplayName="Queue test",UserName=reg.PhoneNumber,NormalizedUserName=reg.PhoneNumber,PhoneNumber=reg.PhoneNumber,PasswordHash="test-only",SecurityStamp=Guid.NewGuid().ToString(),ConcurrencyStamp=Guid.NewGuid().ToString()};
            db.Users.Add(user);await db.SaveChangesAsync();
            var clock=new Clock();var protection=new EphemeralDataProtectionProvider();
            var config=new ConfigurationBuilder().Build();var env=new HostingEnvironment {EnvironmentName=Environments.Development};
            OtpService Service(CoreDbContext context,Guid id)=>new(context,new Actor(id),clock,protection,env,config);
            var service=Service(db,user.Id);
            var request=new RequestOtpDto(Guid.NewGuid(),"Email","queue@example.invalid");
            var created=await service.RequestAsync(request,default);Assert.Null(created.ErrorCode);
            var id=created.Data!.Id;
            Assert.Equal(id,(await service.RequestAsync(request,default)).Data?.Id);
            Assert.Equal("conflict",(await service.RequestAsync(request with {Destination="other@example.invalid"},default)).ErrorCode);
            Assert.Equal("rate_limited",(await service.RequestAsync(request with {RequestKey=Guid.NewGuid()},default)).ErrorCode);
            Assert.Equal("not_found",(await service.PreviewAsync(id,default)).ErrorCode);
            var queue=new NotificationQueueService(db,clock,protection,config,env);
            await queue.ProcessNextAsync(default);db.ChangeTracker.Clear();
            var code=(await service.PreviewAsync(id,default)).Data!.Code;
            var production=new OtpService(db,new Actor(user.Id),clock,protection,new HostingEnvironment {EnvironmentName=Environments.Production},config);
            Assert.Equal("not_found",(await production.PreviewAsync(id,default)).ErrorCode);
            Assert.Equal("invalid_code",(await production.VerifyAsync(id,new(code),default)).ErrorCode);
            Assert.Equal("not_configured",(await production.RequestAsync(request with {RequestKey=Guid.NewGuid()},default)).ErrorCode);
            var verified=await service.VerifyAsync(id,new(code),default);
            Assert.True(verified.Data?.Simulated);Assert.False(verified.Data?.Verified);
            Assert.False((await db.Users.SingleAsync(x=>x.Id==user.Id)).EmailConfirmed);
            Assert.Equal("invalid_code",(await service.VerifyAsync(id,new(code),default)).ErrorCode);
            clock.Now=clock.Now.AddMinutes(2);
            // Concurrent distinct requests for the same recipient must not both enqueue.
            async Task<string?> Enqueue(){await using var context=new CoreDbContext(options);return (await Service(context,user.Id).RequestAsync(request with {RequestKey=Guid.NewGuid()},default)).ErrorCode;}
            var results=await Task.WhenAll(Enqueue(),Enqueue());Assert.Single(results,x=>x is null);Assert.Single(results,x=>x=="rate_limited");
            db.ChangeTracker.Clear();var second=await db.OtpDeliveries.SingleAsync(x=>x.ConsumedAt==null);
            second.State="Processing";second.SendAttempts=1;second.LeaseUntil=clock.Now.AddSeconds(-1);await db.SaveChangesAsync();
            await queue.ProcessNextAsync(default);db.ChangeTracker.Clear();
            var recovered=await db.OtpDeliveries.SingleAsync(x=>x.Id==second.Id);Assert.Equal("Sent",recovered.State);Assert.Equal(2,recovered.SendAttempts);
            var correct=(await service.PreviewAsync(second.Id,default)).Data!.Code;
            var wrong=correct=="000000"?"000001":"000000";
            for(var i=0;i<5;i++){Assert.Equal("invalid_code",(await service.VerifyAsync(second.Id,new(wrong),default)).ErrorCode);}
            Assert.Equal("invalid_code",(await service.VerifyAsync(second.Id,new(correct),default)).ErrorCode);
            clock.Now=clock.Now.AddMinutes(2);
            var expired=await service.RequestAsync(request with {RequestKey=Guid.NewGuid()},default);Assert.Null(expired.ErrorCode);
            clock.Now=clock.Now.AddMinutes(6);await queue.ProcessNextAsync(default);db.ChangeTracker.Clear();
            var expiredRow=await db.OtpDeliveries.SingleAsync(x=>x.Id==expired.Data!.Id);Assert.Equal("Expired",expiredRow.State);Assert.Empty(expiredRow.ProtectedCode);
        }finally{
            await using var drop=new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();
        }
    }
}
