using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services;
using Xunit;

namespace VantaiViet.CoreApi.Tests;

public sealed class KycReviewListDatabaseTests
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
    public async Task ListReturnsQueueRowsWithAccountAndDocumentCountAsync()
    {
        await using var provider = CreateServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var registrationId = Guid.NewGuid();
        var kycId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public."RegistrationApplications" ("Id", "PhoneNumber", "PhoneVerifiedAt", "ExpiresAt")
            VALUES ({registrationId}, '+84900000077', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP + INTERVAL '1 hour');
            INSERT INTO public."KycApplications" ("Id", "RegistrationApplicationId", "Status", "SubmittedAt")
            VALUES ({kycId}, {registrationId}, 'Submitted', CURRENT_TIMESTAMP);
            INSERT INTO public."KycIdentityDetails"
                ("ApplicationId", "FullName", "DateOfBirth", "IdentityNumberEncrypted",
                 "EncryptionKeyId", "IdentityNumberHmac", "HmacKeyId")
            VALUES ({kycId}, 'Kyc List Person', DATE '1990-01-01', decode('01', 'hex'),
                    'test', decode(repeat('00',32),'hex'), 'test');
            INSERT INTO public."KycDocuments" ("ApplicationId", "DocumentType", "StorageKey", "ContentType", "SizeBytes")
            SELECT {kycId}, kind, {kycId.ToString()} || '/' || kind, 'image/jpeg', 1
            FROM unnest(ARRAY['IdentityFront', 'Selfie']) AS kind;
            """);
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User
        {
            RegistrationApplicationId = registrationId,
            DisplayName = "Kyc List Account",
            UserName = "+84900000077",
            PhoneNumber = "+84900000077",
            AccountStatus = "PendingKyc"
        };
        Assert.True((await manager.CreateAsync(user, "Test-Only-Password-123!")).Succeeded);
        var service = new KycReviewService(db, TimeProvider.System);

        var submitted = await service.ListAsync("Submitted", 1, 100, CancellationToken.None);
        var row = Assert.Single(submitted, x => x.Id == kycId);
        Assert.Equal(user.Id, row.UserId);
        Assert.Equal("Kyc List Account", row.DisplayName);
        Assert.Equal("+84900000077", row.PhoneNumber);
        Assert.Equal("Kyc List Person", row.FullName);
        Assert.Equal(2, row.DocumentCount);
        Assert.NotNull(row.SubmittedAt);

        Assert.DoesNotContain(await service.ListAsync("Approved", 1, 100, CancellationToken.None), x => x.Id == kycId);
        Assert.Contains(await service.ListAsync(null, 1, 100, CancellationToken.None), x => x.Id == kycId);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ListAsync("Bogus", 1, 20, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ListAsync(null, 0, 20, CancellationToken.None));
        await transaction.RollbackAsync();
    }
}
