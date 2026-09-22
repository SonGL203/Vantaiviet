using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Hosting;
using VantaiViet.CoreApi.Services.Interfaces;
using Xunit;

namespace VantaiViet.CoreApi.Tests;

/// <summary>One wire-level response: status plus the parsed JSON body, so tests assert the exact field names clients rely on.</summary>
public sealed record ApiResult(HttpStatusCode Status, JsonElement Body)
{
    public JsonElement Data => Body.GetProperty("data");

    public string? ErrorCode => Body.ValueKind == JsonValueKind.Object && Body.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
}

public sealed record TestSession(Guid UserId, string Phone, string Password, string AccessToken, string RefreshToken);

/// <summary>Scripted stand-in for the public geocoder; records every request so tests can check what was asked.</summary>
public sealed class FakeUpstreamHandler : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (Requests)
        {
            Requests.Add(request.RequestUri!);
        }

        return Task.FromResult(Respond(request));
    }
}

/// <summary>
/// A migrated, isolated PostgreSQL database with the real API pipeline (auth, rate limiting, ProblemDetails) listening on a random port.
/// </summary>
public sealed class ApiHostFixture : IAsyncLifetime
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    private static int _phoneCounter = Environment.TickCount % 1_000_000;

    private WebApplication? _app;
    private NpgsqlConnection? _admin;
    private string _databaseName = string.Empty;

    public const string DefaultPassword = "Original123!";

    public FakeUpstreamHandler Geocoder { get; } = new();

    public HttpClient Http { get; private set; } = new();

    public IServiceProvider Services => _app!.Services;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var template = Environment.GetEnvironmentVariable("CORE_DATABASE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(template))
        {
            return;
        }

        var builder = new NpgsqlConnectionStringBuilder(template);
        _databaseName = "shipper_test_" + Guid.NewGuid().ToString("N");
        _admin = new NpgsqlConnection(builder.ConnectionString);
        await _admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {_databaseName}", _admin))
        {
            await create.ExecuteNonQueryAsync();
        }

        builder.Database = _databaseName;
        builder.Pooling = false;
        ConnectionString = builder.ConnectionString;

        var web = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(CoreDbContext).Assembly.FullName,
            EnvironmentName = "Development"
        });
        web.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:CoreDatabase"] = ConnectionString,
            ["Authentication:JwtSigningKey"] = new string('k', 64),
            ["Authentication:Issuer"] = "test",
            ["Authentication:Audience"] = "test",
            ["Kyc:IdentityHmacKey"] = new string('h', 48),
            ["Maps:GeocodingBaseUrl"] = "https://geocoder.test/",
            ["Notifications:WorkerEnabled"] = "false",
            // A real (non-simulated) email channel so a verification code can certify an address; nothing is ever sent.
            ["Notifications:Email:Simulate"] = "false",
            ["Notifications:Smtp:Host"] = "smtp.invalid",
            ["Notifications:Smtp:From"] = "noreply@example.invalid",
            // Flow tests register and sign in many accounts from one address; the limit itself is covered elsewhere.
            ["RateLimiting:AuthPermitLimit"] = "1000"
        });
        web.WebHost.UseUrls("http://127.0.0.1:0");
        web.Services.AddApiFoundation(web.Configuration);
        // Keys must not leak into (or depend on) the developer's real key ring.
        web.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        web.Services.AddHttpClient<IGeocodingService, Services.GeocodingService>().ConfigurePrimaryHttpMessageHandler(() => Geocoder);
        _app = web.Build();
        _app.UseApiFoundation();

        await using (var scope = _app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges(), "The model and the migrations must not drift apart.");
        }

        await _app.StartAsync();
        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        Http = new HttpClient { BaseAddress = new Uri(Assert.Single(Assert.IsAssignableFrom<IServerAddressesFeature>(addresses).Addresses)) };
    }

    public async Task DisposeAsync()
    {
        Http.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        if (_admin is not null)
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE)", _admin);
            await drop.ExecuteNonQueryAsync();
            await _admin.DisposeAsync();
        }
    }

    public async Task<ApiResult> SendAsync(HttpMethod method, string path, object? body = null, string? token = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: WebJson);
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var json = string.IsNullOrWhiteSpace(text) ? JsonDocument.Parse("{}").RootElement.Clone() : JsonDocument.Parse(text).RootElement.Clone();
        return new ApiResult(response.StatusCode, json);
    }

    public Task<ApiResult> GetAsync(string path, string? token = null)
    {
        return SendAsync(HttpMethod.Get, path, null, token);
    }

    public Task<ApiResult> PostAsync(string path, object? body, string? token = null)
    {
        return SendAsync(HttpMethod.Post, path, body ?? new { }, token);
    }

    public Task<ApiResult> PutAsync(string path, object body, string? token = null)
    {
        return SendAsync(HttpMethod.Put, path, body, token);
    }

    public static string NewPhone()
    {
        return "+849" + (Interlocked.Increment(ref _phoneCounter) % 100_000_000).ToString("D8");
    }

    public async Task<TestSession> RegisterAsync(string accountType = "Shipper", string? phone = null, string password = DefaultPassword)
    {
        phone ??= NewPhone();
        var result = await PostAsync("/api/auth/register", new { phoneNumber = phone, displayName = "Người Test", password, accountType });
        Assert.Equal(HttpStatusCode.OK, result.Status);
        return ToSession(result, phone, password);
    }

    public async Task<TestSession> LoginAsync(string phone, string password = DefaultPassword)
    {
        var result = await PostAsync("/api/auth/login", new { phoneNumber = phone, password });
        Assert.Equal(HttpStatusCode.OK, result.Status);
        return ToSession(result, phone, password);
    }

    /// <summary>Grants a role straight in the database (the API only hands out Shipper, Broker and Driver) and signs in again so the token carries it.</summary>
    public async Task<TestSession> WithRoleAsync(TestSession session, string role)
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await users.FindByIdAsync(session.UserId.ToString());
        Assert.True((await users.AddToRoleAsync(user!, role)).Succeeded);
        return await LoginAsync(session.Phone, session.Password);
    }

    public async Task<T> QueryDbAsync<T>(Func<CoreDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<CoreDbContext>());
    }

    private static TestSession ToSession(ApiResult result, string phone, string password)
    {
        var data = result.Data;
        return new TestSession(Guid.Parse(data.GetProperty("userId").GetString()!), phone, password,
            data.GetProperty("accessToken").GetString()!, data.GetProperty("refreshToken").GetString()!);
    }
}
