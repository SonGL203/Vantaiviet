using Microsoft.EntityFrameworkCore;
using Npgsql;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services;
using VantaiViet.CoreApi.Services.Interfaces;
using Xunit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using VantaiViet.CoreApi.Hubs;

namespace VantaiViet.CoreApi.Tests;

public sealed class BookingDatabaseTests
{
    private sealed record Actor(Guid UserId) : ICurrentActor;

    [Theory]
    [InlineData("SHIPPER", false)]
    [InlineData("BROKER", true)]
    public async Task ConcurrentAcceptanceCreatesExactlyOneBookingAndTripAsync(string ownerRole, bool external)
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CORE_DATABASE_TEST_CONNECTION"));
        var database = "booking_test_" + Guid.NewGuid().ToString("N");
        connection.Database = "postgres";
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE {database}", admin))
        {
            await command.ExecuteNonQueryAsync();
        }
        connection.Database = database;
        connection.Pooling = false;
        var options = new DbContextOptionsBuilder<CoreDbContext>().UseNpgsql(connection.ConnectionString).Options;
        try
        {
            await using var db = new CoreDbContext(options);
            await db.Database.MigrateAsync();
            var owner = await SeedUserAsync(db, "+84900000101", ownerRole);
            var firstDriver = await SeedUserAsync(db, "+84900000102", "DRIVER");
            var secondDriver = await SeedUserAsync(db, "+84900000103", "DRIVER");
            var v1 = SeedVehicle(db, firstDriver, "TEST-01");
            var v2 = SeedVehicle(db, secondDriver, "TEST-02");
            var shipment = new Shipment { Id=Guid.NewGuid(),OwnerUserId=owner,Title="Test",PickupAddress="A",DeliveryAddress="B",
                WeightKg=100,PickupAt=DateTimeOffset.UtcNow.AddDays(1),Status="Published",CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow,
                IsExternalOrder=external,ExternalCustomerName=external?"External test customer":null,ExternalCustomerPhone=external?"+84900000999":null };
            db.Shipments.Add(shipment);
            await db.SaveChangesAsync();
            var r1 = await new BookingService(db,new Actor(firstDriver),TimeProvider.System).RequestAsync(shipment.Id,new(v1.Id),default);
            Assert.Null(r1.ErrorCode);
            var r2 = await new BookingService(db,new Actor(secondDriver),TimeProvider.System).RequestAsync(shipment.Id,new(v2.Id),default);
            Assert.Null(r2.ErrorCode);
            await using var foreign = new CoreDbContext(options);
            Assert.Equal("not_found",(await new BookingService(foreign,new Actor(secondDriver),TimeProvider.System).AcceptAsync(r1.Data!.Id,default)).ErrorCode);
            Assert.Equal("not_found",(await new BookingService(foreign,new Actor(secondDriver),TimeProvider.System).ListRequestsAsync(shipment.Id,1,default)).ErrorCode);
            await db.Vehicles.Where(x=>x.Id==v1.Id).ExecuteUpdateAsync(x=>x.SetProperty(v=>v.Status,"Submitted"));
            await using (var invalid = new CoreDbContext(options))
            {
                Assert.Equal("driver_not_eligible",(await new BookingService(invalid,new Actor(owner),TimeProvider.System).AcceptAsync(r1.Data!.Id,default)).ErrorCode);
            }
            Assert.Equal(0,await db.Bookings.CountAsync());
            await db.Vehicles.Where(x=>x.Id==v1.Id).ExecuteUpdateAsync(x=>x.SetProperty(v=>v.Status,"Approved"));
            await using var a = new CoreDbContext(options);
            await using var b = new CoreDbContext(options);
            var outcomes = await Task.WhenAll(
                new BookingService(a,new Actor(owner),TimeProvider.System).AcceptAsync(r1.Data!.Id,default),
                new BookingService(b,new Actor(owner),TimeProvider.System).AcceptAsync(r2.Data!.Id,default));
            Assert.Single(outcomes,x=>x.ErrorCode is null);
            Assert.Single(outcomes,x=>x.ErrorCode=="conflict");
            Assert.Equal(1,await db.Bookings.CountAsync());
            Assert.Equal(1,await db.Trips.CountAsync());
            db.ChangeTracker.Clear();
            Assert.Equal("Booked",(await db.Shipments.SingleAsync()).Status);
            var accepted = await db.TransportRequests.SingleAsync(x=>x.Status=="Accepted");
            await using var repeat = new CoreDbContext(options);
            Assert.Null((await new BookingService(repeat,new Actor(owner),TimeProvider.System).AcceptAsync(accepted.Id,default)).ErrorCode);
            Assert.Equal(1,await db.Bookings.CountAsync());
            Assert.Equal("conflict",(await new ShipmentService(db,new Actor(owner),TimeProvider.System).TransitionAsync(shipment.Id,"cancel",(await db.Shipments.SingleAsync()).Version,default)).ErrorCode);
            await using (var closed = new CoreDbContext(options))
            {
                Assert.Equal("conflict",(await new BookingService(closed,new Actor(accepted.DriverUserId),TimeProvider.System).CloseRequestAsync(accepted.Id,true,default)).ErrorCode);
            }
            var next = new Shipment { Id=Guid.NewGuid(),OwnerUserId=owner,Title="Second shipment",PickupAddress="A",DeliveryAddress="B",
                WeightKg=100,PickupAt=DateTimeOffset.UtcNow.AddDays(2),Status="Published",CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow };
            db.Shipments.Add(next);
            await db.SaveChangesAsync();
            await using (var busy = new CoreDbContext(options))
            {
                var submitted = await new BookingService(busy,new Actor(accepted.DriverUserId),TimeProvider.System).RequestAsync(next.Id,new(accepted.VehicleId),default);
                Assert.Null(submitted.ErrorCode);
                var denied = await new BookingService(busy,new Actor(owner),TimeProvider.System).AcceptAsync(submitted.Data!.Id,default);
                Assert.Equal("resource_unavailable",denied.ErrorCode);
            }
            Assert.Equal(1,await db.Bookings.CountAsync());
            await VerifyRouteCacheAsync(options,owner,(await db.Trips.SingleAsync()).Id);
            await VerifyLifecycleAsync(options,owner,accepted,next.Id);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)",admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task VerifyLifecycleAsync(DbContextOptions<CoreDbContext> options, Guid owner, TransportRequest accepted, Guid nextShipmentId)
    {
        await using var db = new CoreDbContext(options);
        var trip = await db.Trips.SingleAsync();
        var driver = new TripService(db,new Actor(accepted.DriverUserId),TimeProvider.System);
        var ownerService = new TripService(db,new Actor(owner),TimeProvider.System);
        var stranger = new TripService(db,new Actor(Guid.NewGuid()),TimeProvider.System);
        Assert.Equal("not_found",(await stranger.GetAsync(trip.Id,default)).ErrorCode);
        Assert.Equal("forbidden",(await ownerService.StartAsync(trip.Id,new(trip.Version),default)).ErrorCode);
        Assert.Equal("invalid_transition",(await driver.DeliverAsync(trip.Id,new(trip.Version,Guid.NewGuid(),null),default)).ErrorCode);
        var started = await driver.StartAsync(trip.Id,new(trip.Version),default);
        Assert.Equal(TripStates.InProgress,started.Data?.Status);
        Assert.NotNull(started.Data?.StartedAt);
        await VerifyTrackingAsync(options,owner,accepted.DriverUserId,trip.Id);
        Assert.Equal("proof_required",(await driver.PickupAsync(trip.Id,new(trip.Version,Guid.NewGuid(),null),default)).ErrorCode);
        var photo = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a3ioAAAAASUVORK5CYII=");
        var proof = await driver.UploadProofAsync(trip.Id,new("image/png",photo),default);
        Assert.Null(proof.ErrorCode);
        Assert.Equal("not_found",(await stranger.DownloadProofAsync(trip.Id,proof.Data!.Id,default)).ErrorCode);
        Assert.Equal(photo,(await ownerService.DownloadProofAsync(trip.Id,proof.Data!.Id,default)).Data?.Content);
        var loaded = await driver.PickupAsync(trip.Id,new(proof.Data!.TripVersion,proof.Data.Id,"Received"),default);
        Assert.Equal(TripStates.Loaded,loaded.Data?.Status);
        Assert.Equal("invalid_transition",(await ownerService.CancelAsync(trip.Id,new(trip.Version,"Cancel after pickup"),default)).ErrorCode);
        var incident = await driver.ReportIncidentAsync(trip.Id,new(trip.Version,"Traffic delay"),default);
        Assert.Equal(TripStates.Loaded,incident.Data?.Status);
        var nextRequest = await db.TransportRequests.SingleAsync(x=>x.ShipmentId==nextShipmentId);
        await using (var busy = new CoreDbContext(options))
        {
            Assert.Equal("resource_unavailable",(await new BookingService(busy,new Actor(owner),TimeProvider.System).AcceptAsync(nextRequest.Id,default)).ErrorCode);
        }
        Assert.Equal("proof_already_used",(await driver.DeliverAsync(trip.Id,new(trip.Version,proof.Data.Id,null),default)).ErrorCode);
        var deliveryProof = await driver.UploadProofAsync(trip.Id,new("image/png",photo),default);
        var delivered = await driver.DeliverAsync(trip.Id,new(deliveryProof.Data!.TripVersion,deliveryProof.Data.Id,"Delivered"),default);
        Assert.Equal(TripStates.Delivered,delivered.Data?.Status);
        await VerifyTrackingStoppedAsync(options,accepted.DriverUserId,trip.Id);
        Assert.Equal("forbidden",(await driver.CompleteAsync(trip.Id,new(trip.Version),default)).ErrorCode);
        await using (var busy = new CoreDbContext(options))
        {
            Assert.Equal("resource_unavailable",(await new BookingService(busy,new Actor(owner),TimeProvider.System).AcceptAsync(nextRequest.Id,default)).ErrorCode);
        }
        await using var first = new CoreDbContext(options);
        await using var second = new CoreDbContext(options);
        var completions = await Task.WhenAll(
            new TripService(first,new Actor(owner),TimeProvider.System).CompleteAsync(trip.Id,new(trip.Version),default),
            new TripService(second,new Actor(owner),TimeProvider.System).CompleteAsync(trip.Id,new(trip.Version),default));
        Assert.Single(completions,x=>x.ErrorCode is null);
        Assert.Single(completions,x=>x.ErrorCode=="conflict");
        db.ChangeTracker.Clear();
        Assert.Equal("Completed",(await db.Bookings.SingleAsync()).Status);
        Assert.Equal("Completed",(await db.Shipments.SingleAsync(x=>x.Id==accepted.ShipmentId)).Status);
        Assert.Equal(1,await db.TripEvents.CountAsync(x=>x.TripId==trip.Id && x.Action=="Complete"));
        await using var available = new CoreDbContext(options);
        var nextBooking = await new BookingService(available,new Actor(owner),TimeProvider.System).AcceptAsync(nextRequest.Id,default);
        Assert.Null(nextBooking.ErrorCode);
        var nextTrip = await available.Trips.SingleAsync(x=>x.Id==nextBooking.Data!.TripId);
        var cancel = await new TripService(available,new Actor(accepted.DriverUserId),TimeProvider.System).CancelAsync(nextTrip.Id,new(nextTrip.Version,"Vehicle unavailable"),default);
        Assert.Equal("Cancelled",cancel.Data?.Status);
        Assert.Equal("Cancelled",(await available.Bookings.SingleAsync(x=>x.Id==nextTrip.BookingId)).Status);
        Assert.Equal("Cancelled",(await available.Shipments.SingleAsync(x=>x.Id==nextShipmentId)).Status);
        Assert.False(await available.Trips.AnyAsync(x=>x.DriverUserId==accepted.DriverUserId && x.Status!="Completed" && x.Status!="Cancelled"));
    }

    private sealed class CountingRouteProvider : IRouteProvider
    {
        private int _calls;
        public int Calls => _calls;
        public async Task<RouteResponse> GetAsync(CoordinateDto pickup,CoordinateDto delivery,CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            await Task.Delay(200,ct);
            return new("ReferenceCarRoute",[[pickup.Longitude,pickup.Latitude],[delivery.Longitude,delivery.Latitude]],1000,60);
        }
    }

    private static async Task VerifyRouteCacheAsync(DbContextOptions<CoreDbContext> options,Guid owner,Guid tripId)
    {
        var provider = new CountingRouteProvider();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var pickup = new CoordinateDto(21,105);
        var delivery = new CoordinateDto(22,106);
        async Task<RouteResponse> RequestAsync(Guid user,CoordinateDto destination)
        {
            await using var context = new CoreDbContext(options);
            return await new TripRouteService(context,new Actor(user),provider,config,TimeProvider.System)
                .GetAsync(tripId,pickup,destination,default);
        }
        var concurrent = await Task.WhenAll(Enumerable.Range(0,5).Select(_ => RequestAsync(owner,delivery)));
        Assert.All(concurrent,r => Assert.Equal("ReferenceCarRoute",r.Status));
        Assert.Equal(1,provider.Calls);
        Assert.Equal("ReferenceCarRoute",(await RequestAsync(owner,delivery)).Status);
        Assert.Equal(1,provider.Calls);
        Assert.Equal("Unavailable",(await RequestAsync(Guid.NewGuid(),delivery)).Status);
        Assert.Equal(1,provider.Calls);
        await RequestAsync(owner,new(23,107));
        Assert.Equal(2,provider.Calls);
        await using var db = new CoreDbContext(options);
        await db.Set<TripRoute>().ExecuteUpdateAsync(x => x.SetProperty(r => r.ExpiresAt,DateTimeOffset.UtcNow.AddMinutes(-1)));
        await RequestAsync(owner,delivery);
        Assert.Equal(3,provider.Calls);
    }

    private sealed class TestRouteProvider : ITripRouteService
    {
        public Task<RouteResponse> GetAsync(Guid tripId, CoordinateDto pickup, CoordinateDto delivery, CancellationToken ct) =>
            Task.FromResult(new RouteResponse("NotConfigured", [], null, null));
    }

    private static TrackingService Tracking(CoreDbContext db, Guid actorId, IServiceProvider provider) =>
        new(db,new Actor(actorId),TimeProvider.System,provider.GetRequiredService<IHubContext<TrackingHub>>(),
            new TestRouteProvider(),new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName=Environments.Development },
            NullLogger<TrackingService>.Instance);

    private static async Task VerifyTrackingAsync(DbContextOptions<CoreDbContext> options, Guid owner, Guid driver, Guid tripId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignalR();
        await using var provider = services.BuildServiceProvider();
        await using var db = new CoreDbContext(options);
        var trip = await db.Trips.SingleAsync(x=>x.Id==tripId);
        var tracking = Tracking(db,driver,provider);
        var point = new GpsPointRequest(Guid.NewGuid(),21.0285,105.8542,5,DateTimeOffset.UtcNow,true);
        Assert.Equal("invalid_points",(await tracking.SendAsync(tripId,new([point with { Latitude=91 }]),default)).ErrorCode);
        Assert.Equal("invalid_points",(await tracking.SendAsync(tripId,new([point with { RecordedAt=DateTimeOffset.UtcNow.AddHours(1) }]),default)).ErrorCode);
        Assert.Equal("invalid_points",(await tracking.SendAsync(tripId,new([point with { RecordedAt=trip.StartedAt!.Value.AddMinutes(-1) }]),default)).ErrorCode);
        var result = await tracking.SendAsync(tripId,new([point]),default);
        Assert.Equal(1,result.Data?.Inserted);
        var repeated = await tracking.SendAsync(tripId,new([point]),default);
        Assert.Equal(1,repeated.Data?.Duplicates);
        Assert.Equal("point_conflict",(await tracking.SendAsync(tripId,new([point with { Latitude=20 }]),default)).ErrorCode);
        var newer = point with { PointId=Guid.NewGuid(),RecordedAt=point.RecordedAt.AddSeconds(2),Latitude=21.03 };
        Assert.Null((await tracking.SendAsync(tripId,new([newer]),default)).ErrorCode);
        var delayed = point with { PointId=Guid.NewGuid(),RecordedAt=point.RecordedAt.AddSeconds(1),Latitude=21.02 };
        Assert.Null((await tracking.SendAsync(tripId,new([delayed]),default)).ErrorCode);
        Assert.Equal(newer.PointId,(await tracking.SnapshotAsync(tripId,default)).Data?.Latest?.PointId);
        Assert.Equal(3,(await tracking.HistoryAsync(tripId,0,default)).Data?.Points.Count);
        Assert.Equal("not_found",(await Tracking(db,owner,provider).SendAsync(tripId,new([point]),default)).ErrorCode);
        var viewer = await SeedUserAsync(db,"+84900000104","BROKER");
        var view = Tracking(db,viewer,provider);
        Assert.False(await view.CanViewAsync(tripId,viewer,default));
        Assert.Equal("not_found",(await view.HistoryAsync(tripId,0,default)).ErrorCode);
        var manager = Tracking(db,owner,provider);
        Assert.Null((await manager.SetParticipantAsync(tripId,viewer,true,default)).ErrorCode);
        Assert.True(await view.CanViewAsync(tripId,viewer,default));
        Assert.NotNull((await view.SnapshotAsync(tripId,default)).Data?.Latest);
        Assert.Equal("not_found",(await view.SendAsync(tripId,new([point]),default)).ErrorCode);
        Assert.Null((await manager.SetParticipantAsync(tripId,viewer,false,default)).ErrorCode);
        Assert.Equal("not_found",(await view.SnapshotAsync(tripId,default)).ErrorCode);
        Assert.Equal("not_found",(await view.RouteAsync(tripId,default)).ErrorCode);
    }

    private static async Task VerifyTrackingStoppedAsync(DbContextOptions<CoreDbContext> options,Guid driver,Guid tripId)
    {
        var services = new ServiceCollection();
        services.AddLogging();services.AddSignalR();
        await using var provider = services.BuildServiceProvider();
        await using var db = new CoreDbContext(options);
        var point = new GpsPointRequest(Guid.NewGuid(),21,105,5,DateTimeOffset.UtcNow,true);
        Assert.Equal("tracking_stopped",(await Tracking(db,driver,provider).SendAsync(tripId,new([point]),default)).ErrorCode);
    }

    private static async Task<Guid> SeedUserAsync(CoreDbContext db,string phone,string role)
    {
        var registration = new RegistrationApplication { Id=Guid.NewGuid(),PhoneNumber=phone,PhoneVerifiedAt=DateTimeOffset.UtcNow,DisplayName="Test",
            Status="Completed",ExpiresAt=DateTimeOffset.UtcNow.AddDays(1),CompletedAt=DateTimeOffset.UtcNow };
        db.RegistrationApplications.Add(registration);
        var user = new User { Id=Guid.NewGuid(),RegistrationApplicationId=registration.Id,DisplayName="Test",UserName=phone,NormalizedUserName=phone,
            PhoneNumber=phone,PasswordHash="test-only-hash",SecurityStamp=Guid.NewGuid().ToString(),ConcurrencyStamp=Guid.NewGuid().ToString() };
        db.Users.Add(user);
        var roleId=await db.Roles.Where(x=>x.NormalizedName==role).Select(x=>x.Id).SingleAsync();
        db.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid> {UserId=user.Id,RoleId=roleId});
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static Vehicle SeedVehicle(CoreDbContext db,Guid driver,string plate)
    {
        db.DriverProfiles.Add(new DriverProfile {UserId=driver,LicenseClass="C",LicenseExpiresOn=DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),Status="Approved"});
        var vehicle=new Vehicle {Id=Guid.NewGuid(),DriverUserId=driver,LicensePlate=plate,VehicleType="Truck",MaxPayloadKg=1000,Status="Approved"};
        db.Vehicles.Add(vehicle);
        return vehicle;
    }
}
