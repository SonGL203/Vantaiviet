using System.Net;
using VantaiViet.CoreApi.Entities;
using Xunit;
using static VantaiViet.CoreApi.Tests.ShipmentTestKit;

namespace VantaiViet.CoreApi.Tests;

/// <summary>
/// Broker features 1-10 through the real HTTP pipeline on a migrated PostgreSQL database: registering as a broker (reuses
/// the shared phone+password auth), the broker's own profile, posting/editing/deleting/reposting/copying an externally
/// sourced order (reuses the shipper's own shipment endpoints, gated by role rather than duplicated), and a driver's empty
/// truck being searchable by route.
/// </summary>
public sealed class BrokerFeaturesApiTests(ApiHostFixture api) : IClassFixture<ApiHostFixture>
{
    private readonly ShipmentTestKit _kit = new(api);

    [Fact]
    public async Task A_broker_builds_their_profile_and_can_only_edit_with_the_version_they_saw()
    {
        var broker = await _kit.BrokerAsync();
        AssertRejected(await api.GetAsync("/api/broker/profile", broker.AccessToken), HttpStatusCode.NotFound, "broker_profile.not_found");
        AssertRejected(await api.PutAsync("/api/broker/profile", new { operatingArea = "H", yearsExperience = 3 }, broker.AccessToken),
            HttpStatusCode.BadRequest, "request.validation_failed");

        var created = await api.PutAsync("/api/broker/profile", new { operatingArea = "Hải Phòng, Hà Nội", yearsExperience = 5 }, broker.AccessToken);
        AssertOk(created);
        Assert.Equal("Hải Phòng, Hà Nội", created.Data.GetProperty("operatingArea").GetString());
        Assert.Equal(1, created.Data.GetProperty("version").GetInt64());

        var fetched = await api.GetAsync("/api/broker/profile", broker.AccessToken);
        AssertOk(fetched);
        Assert.Equal(5, fetched.Data.GetProperty("yearsExperience").GetInt32());

        AssertRejected(await api.PutAsync("/api/broker/profile", new { operatingArea = "Đà Nẵng", yearsExperience = 6, version = 999 }, broker.AccessToken),
            HttpStatusCode.Conflict, "broker_profile.conflict");
        var edited = await api.PutAsync("/api/broker/profile", new { operatingArea = "Đà Nẵng", yearsExperience = 6, version = 1 }, broker.AccessToken);
        AssertOk(edited);
        Assert.Equal(2, edited.Data.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Only_a_broker_reaches_the_broker_only_endpoints()
    {
        var shipper = await api.RegisterAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync("/api/broker/profile", shipper.AccessToken)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync("/api/broker/vehicle-availability", shipper.AccessToken)).Status);
    }

    [Fact]
    public async Task A_broker_posts_edits_deletes_reposts_and_copies_an_externally_sourced_order()
    {
        var broker = await _kit.BrokerAsync();
        var body = Body(("isExternalOrder", true), ("externalCustomerName", "Chị Năm vựa trái cây"), ("externalCustomerPhone", "+84900000321"));
        var created = await api.PostAsync("/api/shipments", body, broker.AccessToken);
        AssertOk(created);
        Assert.True(created.Data.GetProperty("isExternalOrder").GetBoolean());
        var id = created.Data.GetProperty("id").GetGuid();
        var version = created.Data.GetProperty("version").GetInt64();

        body["title"] = "20 pallet nước giải khát (đã sửa)";
        var edited = await api.PutAsync($"/api/shipments/{id}", new Dictionary<string, object?>(body) { ["version"] = version }, broker.AccessToken);
        AssertOk(edited);
        version = edited.Data.GetProperty("version").GetInt64();

        var published = await api.PostAsync($"/api/shipments/{id}/publish", new { version }, broker.AccessToken);
        AssertOk(published);
        var cancelled = await api.PostAsync($"/api/shipments/{id}/cancel", new { version = published.Data.GetProperty("version").GetInt64() }, broker.AccessToken);
        AssertOk(cancelled);
        Assert.Equal("Cancelled", cancelled.Data.GetProperty("status").GetString());

        var reposted = await api.PostAsync($"/api/shipments/{id}/repost", new { pickupAt = DateTimeOffset.UtcNow.AddDays(4) }, broker.AccessToken);
        AssertOk(reposted);
        Assert.Equal("Published", reposted.Data.GetProperty("status").GetString());
        Assert.NotEqual(cancelled.Data.GetProperty("code").GetString(), reposted.Data.GetProperty("code").GetString());

        var copied = await api.PostAsync($"/api/shipments/{reposted.Data.GetProperty("id").GetGuid()}/copy", new { pickupAt = DateTimeOffset.UtcNow.AddDays(6) }, broker.AccessToken);
        AssertOk(copied);
        Assert.Equal("Draft", copied.Data.GetProperty("status").GetString());

        // A fresh draft can be deleted outright; it never went live.
        var draft = await _kit.CreateDraftAsync(broker, Body(("isExternalOrder", true), ("externalCustomerName", "Anh Bảy"), ("externalCustomerPhone", "+84900000322")));
        AssertOk(await api.SendAsync(HttpMethod.Delete, $"/api/shipments/{draft.Id}?version={draft.Version}", null, broker.AccessToken));
        AssertRejected(await api.GetAsync($"/api/shipments/{draft.Id}", broker.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
    }

    [Fact]
    public async Task A_shipper_cannot_source_an_external_order_only_a_broker_can()
    {
        var shipper = await api.RegisterAsync();
        AssertRejected(await api.PostAsync("/api/shipments", Body(("isExternalOrder", true), ("externalCustomerName", "X"), ("externalCustomerPhone", "+84900000001")), shipper.AccessToken),
            HttpStatusCode.Forbidden, "shipment.forbidden");
    }

    [Fact]
    public async Task A_driver_posts_their_empty_truck_and_a_broker_finds_it_by_route_and_vehicle_type()
    {
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Sáu Xe Tải", payloadKg: 8000);
        var broker = await _kit.BrokerAsync();
        var posted = await api.PostAsync("/api/driver/vehicle-availability", new
        {
            vehicleId,
            fromAddress = "KCN Đình Vũ, Hải Phòng",
            toAddress = "Kho Bắc Từ Liêm, Hà Nội",
            availableFrom = DateTimeOffset.UtcNow.AddHours(1),
            expiresAt = DateTimeOffset.UtcNow.AddDays(2)
        }, driver.AccessToken);
        AssertOk(posted);
        Assert.Equal("Anh Sáu Xe Tải", posted.Data.GetProperty("driverDisplayName").GetString());
        Assert.Equal("Xe tải nhỏ", posted.Data.GetProperty("vehicleType").GetString());
        Assert.Equal("Open", posted.Data.GetProperty("status").GetString());

        var mine = await api.GetAsync("/api/driver/vehicle-availability/mine", driver.AccessToken);
        AssertOk(mine);
        var postedId = posted.Data.GetProperty("id").GetGuid();
        Assert.Contains(mine.Data.EnumerateArray(), x => x.GetProperty("id").GetGuid() == postedId);

        var byPickup = await api.GetAsync("/api/broker/vehicle-availability?fromArea=hải phòng", broker.AccessToken);
        AssertOk(byPickup);
        Assert.Single(byPickup.Data.EnumerateArray());

        var byMiss = await api.GetAsync("/api/broker/vehicle-availability?toArea=Đà Nẵng", broker.AccessToken);
        AssertOk(byMiss);
        Assert.Empty(byMiss.Data.EnumerateArray());

        var byType = await api.GetAsync("/api/broker/vehicle-availability?vehicleType=" + Uri.EscapeDataString("Xe tải nhỏ"), broker.AccessToken);
        AssertOk(byType);
        Assert.Single(byType.Data.EnumerateArray());
        var byOtherType = await api.GetAsync("/api/broker/vehicle-availability?vehicleType=" + Uri.EscapeDataString("Container"), broker.AccessToken);
        AssertOk(byOtherType);
        Assert.Empty(byOtherType.Data.EnumerateArray());
    }

    [Fact]
    public async Task Posting_is_refused_for_an_ineligible_driver_or_an_unreachable_time_window()
    {
        var driver = await api.RegisterAsync("Driver");
        AssertRejected(
            await api.PostAsync("/api/driver/vehicle-availability", new { vehicleId = Guid.NewGuid(), fromAddress = "A", toAddress = "B", availableFrom = DateTimeOffset.UtcNow, expiresAt = DateTimeOffset.UtcNow.AddDays(1) }, driver.AccessToken),
            HttpStatusCode.Forbidden, "vehicle_availability.driver_not_eligible");

        var (eligible, vehicleId) = await _kit.ApprovedDriverAsync();
        AssertRejected(
            await api.PostAsync("/api/driver/vehicle-availability", new { vehicleId, fromAddress = "A", toAddress = "B", availableFrom = DateTimeOffset.UtcNow.AddDays(2), expiresAt = DateTimeOffset.UtcNow.AddDays(1) }, eligible.AccessToken),
            HttpStatusCode.BadRequest, "vehicle_availability.invalid_window");
        AssertRejected(
            await api.PostAsync("/api/driver/vehicle-availability", new { vehicleId, fromAddress = "A", toAddress = "B", availableFrom = DateTimeOffset.UtcNow, expiresAt = DateTimeOffset.UtcNow.AddDays(40) }, eligible.AccessToken),
            HttpStatusCode.BadRequest, "vehicle_availability.invalid_window");
        AssertRejected(
            await api.PostAsync("/api/driver/vehicle-availability", new { vehicleId, fromAddress = "A", toAddress = "B", availableFrom = DateTimeOffset.UtcNow.AddDays(-2), expiresAt = DateTimeOffset.UtcNow.AddDays(-1) }, eligible.AccessToken),
            HttpStatusCode.BadRequest, "vehicle_availability.invalid_window");
    }

    [Fact]
    public async Task A_driver_withdraws_their_own_posting_and_it_drops_out_of_the_search()
    {
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync();
        var (otherDriver, otherVehicleId) = await _kit.ApprovedDriverAsync("Chị Hai");
        var broker = await _kit.BrokerAsync();
        var posted = await api.PostAsync("/api/driver/vehicle-availability",
            new { vehicleId, fromAddress = "Cần Thơ", toAddress = "TP.HCM", availableFrom = DateTimeOffset.UtcNow, expiresAt = DateTimeOffset.UtcNow.AddDays(1) }, driver.AccessToken);
        AssertOk(posted);
        var id = posted.Data.GetProperty("id").GetGuid();
        var version = posted.Data.GetProperty("version").GetInt64();

        Assert.Single((await api.GetAsync("/api/broker/vehicle-availability?fromArea=Cần Thơ", broker.AccessToken)).Data.EnumerateArray());

        // Someone else's posting cannot be withdrawn, and a stale version is refused.
        AssertRejected(await api.SendAsync(HttpMethod.Delete, $"/api/driver/vehicle-availability/{id}?version={version}", null, otherDriver.AccessToken), HttpStatusCode.NotFound, "vehicle_availability.not_found");
        AssertRejected(await api.SendAsync(HttpMethod.Delete, $"/api/driver/vehicle-availability/{id}?version=999", null, driver.AccessToken), HttpStatusCode.Conflict, "vehicle_availability.conflict");

        AssertOk(await api.SendAsync(HttpMethod.Delete, $"/api/driver/vehicle-availability/{id}?version={version}", null, driver.AccessToken));
        Assert.Empty((await api.GetAsync("/api/broker/vehicle-availability?fromArea=Cần Thơ", broker.AccessToken)).Data.EnumerateArray());
        AssertRejected(await api.SendAsync(HttpMethod.Delete, $"/api/driver/vehicle-availability/{id}?version={version + 1}", null, driver.AccessToken), HttpStatusCode.Conflict, "vehicle_availability.conflict");
    }

    [Fact]
    public async Task A_posting_that_has_simply_expired_drops_out_of_the_search_even_without_being_withdrawn()
    {
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync();
        var broker = await _kit.BrokerAsync();
        var id = Guid.NewGuid();
        await api.QueryDbAsync(async db =>
        {
            db.VehicleAvailabilities.Add(new VehicleAvailability
            {
                Id = id,
                DriverUserId = driver.UserId,
                VehicleId = vehicleId,
                FromAddress = "Vũng Tàu",
                ToAddress = "Biên Hòa",
                AvailableFrom = DateTimeOffset.UtcNow.AddDays(-3),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-3),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-3)
            });
            await db.SaveChangesAsync();
            return true;
        });

        Assert.Empty((await api.GetAsync("/api/broker/vehicle-availability?fromArea=Vũng Tàu", broker.AccessToken)).Data.EnumerateArray());
    }
}
