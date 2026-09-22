using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.Entities;
using Xunit;

namespace VantaiViet.CoreApi.Tests;

public sealed class ShipperMigrationTests
{
    private const string PreviousMigration = "20260919010415_AddAuthenticationRecovery";
    private const string BeforeShipmentDetails = "20260921062041_AddShipperOnboarding";

    [PostgreSqlFact]
    public async Task ShipperOnboardingMigrationRollsBackDroppingBusinessLicencesAndAppliesAgainAsync()
    {
        var template = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CORE_DATABASE_TEST_CONNECTION"));
        var name = "migration_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(template.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin))
        {
            await create.ExecuteNonQueryAsync();
        }

        template.Database = name;
        template.Pooling = false;
        try
        {
            await using var db = new CoreDbContext(new DbContextOptionsBuilder<CoreDbContext>().UseNpgsql(template.ConnectionString).Options);
            var migrator = db.GetService<IMigrator>();
            await db.Database.MigrateAsync();
            var registrationId = Guid.NewGuid();
            var applicationId = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO public."RegistrationApplications" ("Id", "PhoneNumber", "ExpiresAt")
                VALUES ({registrationId}, '+84900009001', CURRENT_TIMESTAMP + INTERVAL '1 hour');
                INSERT INTO public."KycApplications" ("Id", "RegistrationApplicationId", "SubjectType") VALUES ({applicationId}, {registrationId}, 'Business');
                INSERT INTO public."KycDocuments" ("ApplicationId", "DocumentType", "StorageKey", "ContentType", "SizeBytes", "Content")
                VALUES ({applicationId}, 'BusinessLicense', 'kyc/rollback/license', 'image/jpeg', 3, decode('ffd8ff', 'hex')),
                       ({applicationId}, 'IdentityFront', 'kyc/rollback/front', 'image/jpeg', 3, decode('ffd8ff', 'hex'));
                """);

            await migrator.MigrateAsync(PreviousMigration);
            Assert.Equal(1, await CountAsync(db, "SELECT count(*) AS \"Value\" FROM public.\"KycDocuments\""));
            Assert.Equal(0, await CountAsync(db, "SELECT count(*) AS \"Value\" FROM information_schema.columns WHERE table_name = 'Shipments' AND column_name = 'CargoType'"));
            Assert.Equal(0, await CountAsync(db, "SELECT count(*) AS \"Value\" FROM information_schema.tables WHERE table_name = 'ShipperBusinessProfiles'"));
            var restored = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("""
                INSERT INTO public."KycDocuments" ("ApplicationId", "DocumentType", "StorageKey", "ContentType", "SizeBytes")
                SELECT "Id", 'BusinessLicense', 'kyc/late', 'image/jpeg', 1 FROM public."KycApplications" LIMIT 1
                """));
            Assert.Equal(PostgresErrorCodes.CheckViolation, restored.SqlState);

            await migrator.MigrateAsync();
            Assert.Equal(1, await CountAsync(db, "SELECT count(*) AS \"Value\" FROM information_schema.columns WHERE table_name = 'Shipments' AND column_name = 'CargoType'"));
            Assert.Equal(1, await CountAsync(db, "SELECT count(*) AS \"Value\" FROM information_schema.tables WHERE table_name = 'ShipperBusinessProfiles'"));
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {name} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [PostgreSqlFact]
    public async Task ShipmentDetailsMigrationKeepsExistingShipmentsAndRollsBackAndAppliesAgainAsync()
    {
        var template = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CORE_DATABASE_TEST_CONNECTION"));
        var name = "migration_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(template.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin))
        {
            await create.ExecuteNonQueryAsync();
        }

        template.Database = name;
        template.Pooling = false;
        try
        {
            await using var db = new CoreDbContext(new DbContextOptionsBuilder<CoreDbContext>().UseNpgsql(template.ConnectionString).Options);
            var migrator = db.GetService<IMigrator>();
            await db.Database.MigrateAsync();
            var owner = await SeedShipperAsync(db);
            var shipmentId = Guid.NewGuid();
            db.Shipments.Add(new Shipment
            {
                Id = shipmentId,
                OwnerUserId = owner,
                Title = "Đơn có chi tiết",
                PickupAddress = "A",
                DeliveryAddress = "B",
                WeightKg = 10,
                PickupAt = DateTimeOffset.UtcNow.AddDays(1),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Quantity = 7,
                QuantityUnit = QuantityUnits.Pallet,
                VehicleType = RequiredVehicleTypes.Container,
                ProposedPrice = 2_500_000,
                Notes = "Ghi chú"
            });
            db.ShipmentPhotos.Add(new ShipmentPhoto { Id = Guid.NewGuid(), ShipmentId = shipmentId, ContentType = "image/jpeg", SizeBytes = 3, Content = [0xFF, 0xD8, 0xFF], CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();

            // Rolling back drops only what this migration added; the shipment itself survives.
            await migrator.MigrateAsync(BeforeShipmentDetails);
            Assert.Equal(1, await CountAsync(db, "SELECT count(*) AS \"Value\" FROM public.\"Shipments\""));
            Assert.Equal(0, await CountAsync(db, "SELECT count(*) AS \"Value\" FROM information_schema.columns WHERE table_name = 'Shipments' AND column_name IN ('Quantity','QuantityUnit','VehicleType','ProposedPrice','Currency','DeliverByAt','Notes')"));
            Assert.Equal(0, await CountAsync(db, "SELECT count(*) AS \"Value\" FROM information_schema.tables WHERE table_name = 'ShipmentPhotos'"));

            // Applying again gives the surviving shipment the defaults an older client would have got.
            await migrator.MigrateAsync();
            var row = await db.Shipments.AsNoTracking().SingleAsync(x => x.Id == shipmentId);
            Assert.Equal((1, QuantityUnits.Package, RequiredVehicleTypes.Any, "VND"), (row.Quantity, row.QuantityUnit, row.VehicleType, row.Currency));
            Assert.Null(row.ProposedPrice);
            Assert.Null(row.DeliverByAt);
            Assert.Null(row.Notes);
            Assert.Equal(0, await db.ShipmentPhotos.CountAsync());
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {name} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<Guid> SeedShipperAsync(CoreDbContext db)
    {
        var registration = new RegistrationApplication
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "+84900009002",
            PhoneVerifiedAt = DateTimeOffset.UtcNow,
            DisplayName = "Test",
            Status = "Completed",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            CompletedAt = DateTimeOffset.UtcNow
        };
        db.RegistrationApplications.Add(registration);
        var user = new User
        {
            Id = Guid.NewGuid(),
            RegistrationApplicationId = registration.Id,
            DisplayName = "Test",
            UserName = registration.PhoneNumber,
            NormalizedUserName = registration.PhoneNumber,
            PhoneNumber = registration.PhoneNumber,
            PasswordHash = "test-only-hash",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<long> CountAsync(CoreDbContext db, string sql)
    {
        return await db.Database.SqlQueryRaw<long>(sql).SingleAsync();
    }
}
