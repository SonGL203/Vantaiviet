using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services;
using VantaiViet.CoreApi.Services.Interfaces;
using Xunit;

namespace VantaiViet.CoreApi.Tests;

public sealed class ShipmentDatabaseTests
{
    private sealed record Actor(Guid UserId) : ICurrentActor;

    [PostgreSqlFact]
    public async Task OwnershipTransitionsAndStaleVersionAreEnforcedAsync()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("CORE_DATABASE_TEST_CONNECTION")).Options;
        await using var db = new CoreDbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var owner = await db.Users.Where(x => x.AccountStatus == "Active").Select(x => x.Id).FirstAsync();
        var role = await db.Roles.Where(x => x.NormalizedName == "SHIPPER").Select(x => x.Id).SingleAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public."UserRoles" ("UserId", "RoleId") VALUES ({owner}, {role})
            ON CONFLICT DO NOTHING
            """);
        var service = new ShipmentService(db, new Actor(owner), TimeProvider.System);
        var request = new CreateShipmentRequest("Test shipment", "Pickup", "Delivery", 100, DateTimeOffset.UtcNow.AddDays(2),
            Pickup: new(21.0285,105.8542), Delivery: new(20.9,105.9));
        Assert.Equal("invalid_coordinates", (await service.CreateAsync(request with {Delivery=null},default)).ErrorCode);
        Assert.Equal("invalid_coordinates", (await service.CreateAsync(request with {Pickup=new(double.NaN,105)},default)).ErrorCode);
        var created = await service.CreateAsync(request, default);
        Assert.Null(created.ErrorCode);
        var shipment = Assert.IsType<ShipmentResponse>(created.Data);
        Assert.Equal(request.Pickup,shipment.Pickup);
        Assert.Equal(request.Delivery,(await service.GetAsync(shipment.Id,default)).Data?.Shipment.Delivery);
        var edited=await service.UpdateAsync(shipment.Id,new UpdateShipmentRequest("Edited","Pickup","Delivery",100,request.PickupAt,shipment.Version),default);
        Assert.Null(edited.ErrorCode);
        shipment=Assert.IsType<ShipmentResponse>(edited.Data);
        Assert.Equal(request.Pickup,shipment.Pickup);
        Assert.Equal(request.Delivery,(await service.ListAsync(true,1,100,default)).Data?.Single(x=>x.Id==shipment.Id).Delivery);
        var stranger = new ShipmentService(db, new Actor(Guid.NewGuid()), TimeProvider.System);
        Assert.Equal("not_found", (await stranger.GetAsync(shipment.Id, default)).ErrorCode);
        Assert.Equal("not_found", (await stranger.TransitionAsync(shipment.Id, "cancel", shipment.Version, default)).ErrorCode);
        Assert.Equal("conflict", (await service.TransitionAsync(shipment.Id, "publish", shipment.Version + 1, default)).ErrorCode);
        var published = await service.TransitionAsync(shipment.Id, "publish", shipment.Version, default);
        Assert.Equal("Published", published.Data?.Status);
        Assert.Equal("conflict", (await service.TransitionAsync(shipment.Id, "delete", published.Data!.Version, default)).ErrorCode);
        Assert.True(await db.AuditEvents.AnyAsync(x => x.TargetId == shipment.Id && x.Action == "shipment.publish"));
        await transaction.RollbackAsync();
    }
}
