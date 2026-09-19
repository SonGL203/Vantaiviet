using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Hosting;
using VantaiViet.CoreApi.Services;
using VantaiViet.CoreApi.Services.Interfaces;
using Xunit;

namespace VantaiViet.CoreApi.Tests;

public sealed class AuthSessionDatabaseTests
{
    private sealed class Actor : ICurrentActor
    {
        public Guid UserId { get; set; }
        public Guid SessionId { get; set; }
    }
    private static Guid SessionId(AuthResponse response) => Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken).Claims.Single(x=>x.Type=="sid").Value);
    private static string Stamp(AuthResponse response) => new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken).Claims.Single(x=>x.Type=="security_stamp").Value;

    [PostgreSqlFact]
    public async Task SessionsAndRecoveryRejectReplayAndRevokeOldCredentialsAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CORE_DATABASE_TEST_CONNECTION"));
        var name = "auth_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {name}",admin)) { await create.ExecuteNonQueryAsync(); }
        builder.Database=name;builder.Pooling=false;
        try
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["Authentication:JwtSigningKey"]=new string('x',64),["Authentication:Issuer"]="test",["Authentication:Audience"]="test",
                ["Notifications:WorkerEnabled"]="true",["Notifications:Email:Simulate"]="false",
                ["Notifications:Smtp:Host"]="smtp.invalid",["Notifications:Smtp:From"]="test@example.invalid"
            }).Build();
            var protection = new EphemeralDataProtectionProvider();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton<IDataProtectionProvider>(protection);
            services.AddSingleton(TimeProvider.System);
            services.AddDbContext<CoreDbContext>(x=>x.UseNpgsql(builder.ConnectionString));
            services.AddIdentityCore<User>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<CoreDbContext>().AddDefaultTokenProviders();
            services.AddScoped<Actor>();services.AddScoped<ICurrentActor>(x=>x.GetRequiredService<Actor>());
            services.AddScoped<AuthService>();services.AddScoped<PasswordRecoveryService>();
            await using var root = services.BuildServiceProvider();
            async Task<T> RunAsync<T>(Func<AuthService,CoreDbContext,PasswordRecoveryService,Task<T>> run,AuthResponse? actor=null)
            {
                await using var scope = root.CreateAsyncScope();
                var current = scope.ServiceProvider.GetRequiredService<Actor>();
                if (actor is not null) { current.UserId=actor.UserId;current.SessionId=SessionId(actor); }
                return await run(scope.ServiceProvider.GetRequiredService<AuthService>(),scope.ServiceProvider.GetRequiredService<CoreDbContext>(),scope.ServiceProvider.GetRequiredService<PasswordRecoveryService>());
            }
            await RunAsync(async (_,db,_)=> { await db.Database.MigrateAsync();return true; });
            var first = await RunAsync((auth,_,_)=>auth.RegisterAsync(new("+84900000601","Session test","Original123!"),default));
            var other = await RunAsync((auth,_,_)=>auth.RegisterAsync(new("+84900000602","Other user","Original123!"),default));
            Assert.False(await RunAsync((_,db,_)=>db.Users.Where(x=>x.Id==first.UserId).Select(x=>x.PhoneNumberConfirmed).SingleAsync()));
            Assert.Null(await RunAsync((_,db,_)=>db.RegistrationApplications.Where(x=>x.PhoneNumber=="+84900000601").Select(x=>x.PhoneVerifiedAt).SingleAsync()));
            Assert.True(await RunAsync((auth,_,_)=>auth.IsSessionActiveAsync(first.UserId,SessionId(first),Stamp(first),default)));
            Assert.Equal("not_found",(await RunAsync((auth,_,_)=>auth.RevokeSessionAsync(SessionId(first),default),other)).ErrorCode);
            var rotations = await Task.WhenAll(Enumerable.Range(0,2).Select(_=>RunAsync((auth,_,_)=>auth.RefreshAsync(new(first.RefreshToken),default))));
            Assert.Single(rotations,x=>x.ErrorCode is null);
            Assert.Single(rotations,x=>x.ErrorCode=="invalid_credentials");
            var rotated = Assert.IsType<AuthResponse>(rotations.Single(x=>x.ErrorCode is null).Data);
            Assert.False(await RunAsync((auth,_,_)=>auth.IsSessionActiveAsync(rotated.UserId,SessionId(rotated),Stamp(rotated),default)));
            Assert.Equal("invalid_credentials",(await RunAsync((auth,_,_)=>auth.RefreshAsync(new(rotated.RefreshToken),default))).ErrorCode);
            var login = await RunAsync((auth,_,_)=>auth.LoginAsync(new("+84900000601","Original123!"),default));
            Assert.Null((await RunAsync((auth,_,_)=>auth.LogoutAsync(default),login)).ErrorCode);
            Assert.False(await RunAsync((auth,_,_)=>auth.IsSessionActiveAsync(login.UserId,SessionId(login),Stamp(login),default)));
            login = await RunAsync((auth,_,_)=>auth.LoginAsync(new("+84900000601","Original123!"),default));
            Assert.Equal("invalid_credentials",(await RunAsync((auth,_,_)=>auth.ChangePasswordAsync(new("Wrong123!","Changed123!"),default),login)).ErrorCode);
            Assert.Null((await RunAsync((auth,_,_)=>auth.ChangePasswordAsync(new("Original123!","Changed123!"),default),login)).ErrorCode);
            Assert.False(await RunAsync((auth,_,_)=>auth.IsSessionActiveAsync(login.UserId,SessionId(login),Stamp(login),default)));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>RunAsync((auth,_,_)=>auth.LoginAsync(new("+84900000601","Original123!"),default)));
            login = await RunAsync((auth,_,_)=>auth.LoginAsync(new("+84900000601","Changed123!"),default));
            await RunAsync((_,db,_)=>db.Users.Where(x=>x.Id==first.UserId).ExecuteUpdateAsync(x=>x.SetProperty(u=>u.Email,"reset@example.invalid")
                .SetProperty(u=>u.NormalizedEmail,"RESET@EXAMPLE.INVALID").SetProperty(u=>u.EmailConfirmed,true)));
            var unknown = await RunAsync((_,_,recovery)=>recovery.RequestAsync(new("unknown@example.invalid"),default));
            Assert.Null(unknown.ErrorCode);
            Assert.False(await RunAsync((_,db,_)=>db.OtpDeliveries.AnyAsync(x=>x.Id==unknown.Data!.ChallengeId)));
            var challenge = (await RunAsync((_,_,recovery)=>recovery.RequestAsync(new("reset@example.invalid"),default))).Data!;
            var duplicate = (await RunAsync((_,_,recovery)=>recovery.RequestAsync(new("reset@example.invalid"),default))).Data!;
            Assert.NotEqual(challenge.ChallengeId,duplicate.ChallengeId);
            Assert.Equal(1,await RunAsync((_,db,_)=>db.OtpDeliveries.CountAsync()));
            var code = await RunAsync(async (_,db,_)=>
            {
                var row = await db.OtpDeliveries.SingleAsync();row.State="Sent";await db.SaveChangesAsync();
                return protection.CreateProtector("VantaiViet.OtpDelivery.v1").Unprotect(row.ProtectedCode);
            });
            var wrong = code=="000000"?"000001":"000000";
            for (var i=0;i<5;i++)
            { Assert.Equal("invalid_code",(await RunAsync((_,_,recovery)=>recovery.ResetAsync(new(challenge.ChallengeId,wrong,"Recovered123!"),default))).ErrorCode); }
            Assert.Equal("invalid_code",(await RunAsync((_,_,recovery)=>recovery.ResetAsync(new(challenge.ChallengeId,code,"Recovered123!"),default))).ErrorCode);
            await RunAsync((_,db,_)=>db.OtpDeliveries.ExecuteUpdateAsync(x=>x.SetProperty(r=>r.CreatedAt,DateTimeOffset.UtcNow.AddMinutes(-2))));
            var second = (await RunAsync((_,_,recovery)=>recovery.RequestAsync(new("reset@example.invalid"),default))).Data!;
            await RunAsync((_,db,_)=>db.Users.Where(x=>x.Id==first.UserId).ExecuteUpdateAsync(x=>x.SetProperty(u=>u.LockoutEnd,DateTimeOffset.UtcNow.AddMinutes(5)).SetProperty(u=>u.AccessFailedCount,5)));
            code = await RunAsync(async (_,db,_)=>
            {
                var row = await db.OtpDeliveries.SingleAsync(x=>x.Id==second.ChallengeId);row.State="Sent";await db.SaveChangesAsync();
                return protection.CreateProtector("VantaiViet.OtpDelivery.v1").Unprotect(row.ProtectedCode);
            });
            var resetResults = await Task.WhenAll(Enumerable.Range(0,2).Select(_=>RunAsync((_,_,recovery)=>recovery.ResetAsync(new(second.ChallengeId,code,"Recovered123!"),default))));
            Assert.Single(resetResults,x=>x.ErrorCode is null);
            Assert.Single(resetResults,x=>x.ErrorCode=="invalid_code");
            Assert.False(await RunAsync((auth,_,_)=>auth.IsSessionActiveAsync(login.UserId,SessionId(login),Stamp(login),default)));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>RunAsync((auth,_,_)=>auth.LoginAsync(new("+84900000601","Changed123!"),default)));
            Assert.NotNull(await RunAsync((auth,_,_)=>auth.LoginAsync(new("+84900000601","Recovered123!"),default)));
            await VerifyHttpAsync(builder.ConnectionString,config);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)",admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task VerifyHttpAsync(string connection,IConfiguration config)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName=typeof(AuthService).Assembly.FullName,EnvironmentName="Development"
        });
        builder.Configuration.AddConfiguration(config).AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["ConnectionStrings:CoreDatabase"]=connection,["Notifications:WorkerEnabled"]="false"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddApiFoundation(builder.Configuration);
        await using var app = builder.Build();
        app.UseApiFoundation();
        await app.StartAsync();
        try
        {
            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
            using var http = new HttpClient { BaseAddress=new Uri(Assert.Single(Assert.IsAssignableFrom<IServerAddressesFeature>(addresses).Addresses)) };
            var response = await http.PostAsJsonAsync("/api/auth/login",new LoginRequest("+84900000601","Recovered123!"));
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            var auth = (await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data;
            http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",auth.AccessToken);
            Assert.Equal(HttpStatusCode.OK,(await http.GetAsync("/api/auth/sessions")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await http.PostAsJsonAsync($"/api/shipments/{Guid.NewGuid()}/broker",new AssignBrokerRequest(Guid.NewGuid(),1))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await http.PostAsync($"/api/transport-requests/{Guid.NewGuid()}/accept",null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync($"/api/shipments/{Guid.NewGuid()}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await http.PostAsync("/api/auth/logout",null)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,(await http.GetAsync("/api/auth/sessions")).StatusCode);
            http.DefaultRequestHeaders.Authorization=null;
            Assert.Equal(HttpStatusCode.Unauthorized,(await http.PostAsJsonAsync("/api/auth/refresh",new RefreshSessionRequest(auth.RefreshToken))).StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,(await http.PostAsJsonAsync("/api/auth/forgot-password",new ForgotPasswordRequest("reset@example.invalid"))).StatusCode);
            HttpStatusCode status=HttpStatusCode.OK;
            for (var i=0;i<35 && status!=HttpStatusCode.TooManyRequests;i++)
            { status=(await http.PostAsJsonAsync("/api/auth/refresh",new RefreshSessionRequest(new string('x',64)))).StatusCode; }
            Assert.Equal(HttpStatusCode.TooManyRequests,status);
        }
        finally { await app.StopAsync(); }
    }
}
