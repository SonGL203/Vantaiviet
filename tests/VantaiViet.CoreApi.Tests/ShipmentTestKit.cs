using System.Net;
using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Entities;
using Xunit;

namespace VantaiViet.CoreApi.Tests;

/// <summary>What the shipper follow-up tests share: building shipments, drivers with a vehicle, and driving a booking through its trip.</summary>
public sealed class ShipmentTestKit(ApiHostFixture api)
{
    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0xFF, 0xD9];

    public static void AssertOk(ApiResult result)
    {
        Assert.True(result.Status == HttpStatusCode.OK, $"Expected 200 but got {(int)result.Status}: {result.Body}");
    }

    public static void AssertRejected(ApiResult result, HttpStatusCode status, string errorCode)
    {
        Assert.True(result.Status == status && result.ErrorCode == errorCode,
            $"Expected {(int)status} {errorCode} but got {(int)result.Status} {result.ErrorCode}: {result.Body}");
    }

    /// <summary>A complete create request; each override replaces one field.</summary>
    public static Dictionary<string, object?> Body(params (string Key, object? Value)[] overrides)
    {
        var body = new Dictionary<string, object?>
        {
            ["title"] = "20 pallet nước giải khát",
            ["pickupAddress"] = "Kho A, Thủ Đức, TP.HCM",
            ["deliveryAddress"] = "Kho B, Dĩ An, Bình Dương",
            ["weightKg"] = 1200.5,
            ["pickupAt"] = DateTimeOffset.UtcNow.AddDays(3),
            ["quantity"] = 20,
            ["quantityUnit"] = "Pallet",
            ["vehicleType"] = "Container",
            ["proposedPrice"] = "15000000",
            ["deliverByAt"] = DateTimeOffset.UtcNow.AddDays(5),
            ["notes"] = "Giao trong giờ hành chính"
        };
        foreach (var (key, value) in overrides)
        {
            body[key] = value;
        }

        return body;
    }

    public async Task<(Guid Id, long Version)> CreateDraftAsync(TestSession owner, Dictionary<string, object?>? body = null)
    {
        var created = await api.PostAsync("/api/shipments", body ?? Body(), owner.AccessToken);
        AssertOk(created);
        return (created.Data.GetProperty("id").GetGuid(), created.Data.GetProperty("version").GetInt64());
    }

    public async Task<(Guid Id, long Version)> CreatePublishedAsync(TestSession owner, Dictionary<string, object?>? body = null)
    {
        var (id, version) = await CreateDraftAsync(owner, body);
        var published = await api.PostAsync($"/api/shipments/{id}/publish", new { version }, owner.AccessToken);
        AssertOk(published);
        return (id, published.Data.GetProperty("version").GetInt64());
    }

    public async Task<long> VersionOfAsync(TestSession owner, Guid id)
    {
        var detail = await api.GetAsync($"/api/shipments/{id}", owner.AccessToken);
        AssertOk(detail);
        return detail.Data.GetProperty("shipment").GetProperty("version").GetInt64();
    }

    public async Task<(TestSession Driver, Guid VehicleId)> ApprovedDriverAsync(string displayName = "Tài xế Test", decimal payloadKg = 5000, string? bodyType = null)
    {
        var driver = await api.RegisterAsync("Driver");
        var vehicleId = Guid.NewGuid();
        await api.QueryDbAsync(async db =>
        {
            await db.Users.Where(x => x.Id == driver.UserId).ExecuteUpdateAsync(x => x.SetProperty(u => u.AccountStatus, "Active").SetProperty(u => u.DisplayName, displayName));
            db.DriverProfiles.Add(new DriverProfile { UserId = driver.UserId, LicenseClass = "C", LicenseExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), Status = "Approved" });
            db.Vehicles.Add(new Vehicle
            {
                Id = vehicleId,
                DriverUserId = driver.UserId,
                LicensePlate = "T-" + Guid.NewGuid().ToString("N")[..10],
                VehicleType = "Xe tải nhỏ",
                MaxPayloadKg = payloadKg,
                BodyType = bodyType,
                Status = "Approved"
            });
            await db.SaveChangesAsync();
            return true;
        });
        return (driver, vehicleId);
    }

    public async Task<Guid> RequestAsync(Guid shipmentId, TestSession driver, Guid vehicleId)
    {
        var request = await api.PostAsync($"/api/shipments/{shipmentId}/requests", new { vehicleId }, driver.AccessToken);
        AssertOk(request);
        return request.Data.GetProperty("id").GetGuid();
    }

    /// <summary>The owner accepts a driver's request; returns the trip that was opened.</summary>
    public async Task<Guid> AcceptAsync(TestSession owner, Guid requestId)
    {
        var accepted = await api.PostAsync($"/api/transport-requests/{requestId}/accept", null, owner.AccessToken);
        AssertOk(accepted);
        return accepted.Data.GetProperty("tripId").GetGuid();
    }

    public async Task<long> TripVersionAsync(Guid tripId, TestSession who)
    {
        var trip = await api.GetAsync($"/api/trips/{tripId}", who.AccessToken);
        AssertOk(trip);
        return trip.Data.GetProperty("version").GetInt64();
    }

    /// <summary>The driver starts, picks up and delivers, each with the proof photo the API asks for.</summary>
    public async Task DeliverAsync(Guid tripId, TestSession driver)
    {
        AssertOk(await api.PostAsync($"/api/trips/{tripId}/start", new { version = await TripVersionAsync(tripId, driver) }, driver.AccessToken));
        foreach (var step in new[] { "pickup", "deliver" })
        {
            var proof = await api.PostAsync($"/api/trips/{tripId}/proofs", new { contentType = "image/jpeg", content = Jpeg }, driver.AccessToken);
            AssertOk(proof);
            var done = await api.PostAsync($"/api/trips/{tripId}/{step}",
                new { version = await TripVersionAsync(tripId, driver), proofId = proof.Data.GetProperty("id").GetGuid() }, driver.AccessToken);
            AssertOk(done);
        }
    }

    public async Task<TestSession> BrokerAsync()
    {
        var broker = await api.RegisterAsync("Broker");
        await api.QueryDbAsync(async db =>
        {
            await db.Users.Where(x => x.Id == broker.UserId).ExecuteUpdateAsync(x => x.SetProperty(u => u.AccountStatus, "Active"));
            return true;
        });
        return broker;
    }
}
