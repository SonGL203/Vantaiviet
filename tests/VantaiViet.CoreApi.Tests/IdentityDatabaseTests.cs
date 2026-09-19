using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.Entities;
using Xunit;

namespace VantaiViet.CoreApi.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CORE_DATABASE_TEST_CONNECTION")))
        {
            Skip = "Set CORE_DATABASE_TEST_CONNECTION to an isolated migrated PostgreSQL database.";
        }
    }
}

public sealed class IdentityDatabaseTests
{
    private static ServiceProvider CreateServices()
    {
        var connection = Environment.GetEnvironmentVariable("CORE_DATABASE_TEST_CONNECTION")
            ?? throw new InvalidOperationException("An isolated PostgreSQL test database is required.");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<CoreDbContext>(options => options.UseNpgsql(connection));
        services.AddIdentityCore<User>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<CoreDbContext>();
        return services.BuildServiceProvider();
    }

    [PostgreSqlFact]
    public async Task SimplifiedRegistrationAllowsPendingKycWithoutPhoneVerificationAsync()
    {
        await using var provider = CreateServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var registrationId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public."RegistrationApplications" ("Id", "PhoneNumber", "ExpiresAt")
            VALUES ({registrationId}, '+84900000001', CURRENT_TIMESTAMP + INTERVAL '1 hour')
            """);
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User
        {
            RegistrationApplicationId = registrationId,
            DisplayName = "Integration test",
            UserName = "+84900000001",
            PhoneNumber = "+84900000001",
            PhoneNumberConfirmed = false,
            AccountStatus = "PendingKyc"
        };
        // SimplifyRegistration deliberately removed the old KYC/phone gate on insertion.
        Assert.True((await manager.CreateAsync(user, "Test-Only-Password-123!")).Succeeded);
        var saved=await db.Users.AsNoTracking().SingleAsync(x=>x.Id==user.Id);
        Assert.Equal("PendingKyc",saved.AccountStatus);
        Assert.False(saved.PhoneNumberConfirmed);
        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    public async Task ApprovedRegistrationCreatesIdentityUserAndCompletesApplicationAtomicallyAsync()
    {
        await using var provider = CreateServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var registrationId = Guid.NewGuid();
        var kycId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public."RegistrationApplications"
                ("Id", "PhoneNumber", "PhoneVerifiedAt", "ExpiresAt")
            VALUES ({registrationId}, '+84900000002', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP + INTERVAL '1 hour');
            INSERT INTO public."KycApplications"
                ("Id", "RegistrationApplicationId", "Status", "SubmittedAt", "ReviewedAt", "ReviewerReference")
            VALUES ({kycId}, {registrationId}, 'Approved', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'integration-test');
            INSERT INTO public."KycIdentityDetails"
                ("ApplicationId", "FullName", "DateOfBirth", "IdentityNumberEncrypted",
                 "EncryptionKeyId", "IdentityNumberHmac", "HmacKeyId")
            VALUES ({kycId}, 'Integration test', DATE '1990-01-01', decode('01', 'hex'),
                    'test', decode(repeat('00',32),'hex'), 'test');
            INSERT INTO public."KycDocuments"
                ("ApplicationId", "DocumentType", "StorageKey", "ContentType", "SizeBytes")
            SELECT {kycId}, kind, {kycId.ToString()} || '/' || kind, 'image/jpeg', 1
            FROM unnest(ARRAY['IdentityFront', 'IdentityBack', 'Selfie']) AS kind;
            """);
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User
        {
            RegistrationApplicationId = registrationId,
            DisplayName = "Integration test",
            UserName = "+84900000002",
            PhoneNumber = "+84900000002",
            PhoneNumberConfirmed = true
        };
        Assert.True((await manager.CreateAsync(user, "Test-Only-Password-123!")).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, "User")).Succeeded);
        Assert.True(await manager.CheckPasswordAsync(user, "Test-Only-Password-123!"));
        var registration = await db.RegistrationApplications.SingleAsync(x => x.Id == registrationId);
        Assert.Equal("Completed", registration.Status);
        Assert.NotNull(registration.CompletedAt);
        Assert.True(await manager.IsInRoleAsync(user, "User"));
        Assert.False(db.Database.HasPendingModelChanges());
        await transaction.RollbackAsync();
    }
}
