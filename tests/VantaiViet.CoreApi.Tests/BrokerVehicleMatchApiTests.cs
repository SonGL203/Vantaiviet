using System.Net;
using Xunit;
using static VantaiViet.CoreApi.Tests.ShipmentTestKit;

namespace VantaiViet.CoreApi.Tests;

/// <summary>
/// Broker batch 6: "Có xe phù hợp" widened from "a driver I saved posted a truck" to "a driver posted a truck that
/// matches one of my own open shipments", even when I never saved that driver; and "Chuyến thay đổi" reaching a
/// broker dispatching someone else's shipment, not only the shipment's actual owner.
/// </summary>
public sealed class BrokerVehicleMatchApiTests(ApiHostFixture api) : IClassFixture<ApiHostFixture>
{
    private readonly ShipmentTestKit _kit = new(api);

    private static async Task<Guid> PostVehicleAsync(ApiHostFixture api, TestSession driver, Guid vehicleId, string from, string to)
    {
        var posted = await api.PostAsync("/api/driver/vehicle-availability", new
        {
            vehicleId,
            fromAddress = from,
            toAddress = to,
            availableFrom = DateTimeOffset.UtcNow.AddHours(1),
            expiresAt = DateTimeOffset.UtcNow.AddDays(2)
        }, driver.AccessToken);
        AssertOk(posted);
        return posted.Data.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Posting_a_truck_notifies_a_broker_with_a_matching_open_shipment_even_without_a_saved_driver()
    {
        var broker = await _kit.BrokerAsync();
        await _kit.CreatePublishedAsync(broker, Body(
            ("title", "Cần xe tuyến Hải Phòng"), ("isExternalOrder", true), ("externalCustomerName", "Khách"), ("externalCustomerPhone", "+84900000930"),
            ("pickupAddress", "Kho X, Hải Phòng"), ("deliveryAddress", "Kho Y, Hà Nội"), ("vehicleType", "Any")));

        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Sáu");
        await PostVehicleAsync(api, driver, vehicleId, "Hải Phòng", "Hà Nội");

        var notifications = await api.GetAsync("/api/notifications", broker.AccessToken);
        AssertOk(notifications);
        Assert.Contains(notifications.Data.GetProperty("items").EnumerateArray(), x => x.GetProperty("type").GetString() == "vehicle.available");
    }

    [Fact]
    public async Task A_truck_on_a_different_route_does_not_notify_and_neither_does_a_shipper_owned_shipment()
    {
        var broker = await _kit.BrokerAsync();
        await _kit.CreatePublishedAsync(broker, Body(
            ("title", "Cần xe tuyến Cần Thơ"), ("isExternalOrder", true), ("externalCustomerName", "Khách"), ("externalCustomerPhone", "+84900000931"),
            ("pickupAddress", "Kho X, Cần Thơ"), ("deliveryAddress", "Kho Y, Cà Mau"), ("vehicleType", "Any")));

        var shipper = await api.RegisterAsync();
        await _kit.CreatePublishedAsync(shipper, Body(
            ("title", "Đơn của chủ hàng thường"), ("pickupAddress", "Kho X, Hải Phòng"), ("deliveryAddress", "Kho Y, Hà Nội"), ("vehicleType", "Any")));

        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Bảy");
        await PostVehicleAsync(api, driver, vehicleId, "Hải Phòng", "Hà Nội");

        var brokerNotifications = await api.GetAsync("/api/notifications", broker.AccessToken);
        AssertOk(brokerNotifications);
        Assert.DoesNotContain(brokerNotifications.Data.GetProperty("items").EnumerateArray(), x => x.GetProperty("type").GetString() == "vehicle.available");

        var shipperNotifications = await api.GetAsync("/api/notifications", shipper.AccessToken);
        AssertOk(shipperNotifications);
        Assert.DoesNotContain(shipperNotifications.Data.GetProperty("items").EnumerateArray(), x => x.GetProperty("type").GetString() == "vehicle.available");
    }

    [Fact]
    public async Task A_required_vehicle_type_narrows_the_match_but_any_type_matches_every_truck()
    {
        var pickyBroker = await _kit.BrokerAsync();
        await _kit.CreatePublishedAsync(pickyBroker, Body(
            ("title", "Chỉ nhận xe tải nhẹ"), ("isExternalOrder", true), ("externalCustomerName", "Khách"), ("externalCustomerPhone", "+84900000932"),
            ("pickupAddress", "Kho X, Đà Nẵng"), ("deliveryAddress", "Kho Y, Huế"), ("vehicleType", "SmallTruck")));
        var flexibleBroker = await _kit.BrokerAsync();
        await _kit.CreatePublishedAsync(flexibleBroker, Body(
            ("title", "Xe gì cũng được"), ("isExternalOrder", true), ("externalCustomerName", "Khách"), ("externalCustomerPhone", "+84900000933"),
            ("pickupAddress", "Kho X, Đà Nẵng"), ("deliveryAddress", "Kho Y, Huế"), ("vehicleType", "Any")));

        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Tám");
        await PostVehicleAsync(api, driver, vehicleId, "Đà Nẵng", "Huế");

        var pickyNotifications = await api.GetAsync("/api/notifications", pickyBroker.AccessToken);
        AssertOk(pickyNotifications);
        Assert.DoesNotContain(pickyNotifications.Data.GetProperty("items").EnumerateArray(), x => x.GetProperty("type").GetString() == "vehicle.available");

        var flexibleNotifications = await api.GetAsync("/api/notifications", flexibleBroker.AccessToken);
        AssertOk(flexibleNotifications);
        Assert.Contains(flexibleNotifications.Data.GetProperty("items").EnumerateArray(), x => x.GetProperty("type").GetString() == "vehicle.available");
    }

    /// <summary>"Chuyến thay đổi" used to only reach whichever of owner/driver was not the actor, so a broker merely
    /// dispatching someone else's shipment (assigned + accepted, never the owner) never heard about trip progress.</summary>
    [Fact]
    public async Task Trip_progress_also_notifies_a_broker_dispatching_someone_elses_shipment()
    {
        var shipper = await api.RegisterAsync();
        var broker = await _kit.BrokerAsync();
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Chín");
        var (id, version) = await _kit.CreatePublishedAsync(shipper, Body(("title", "Đơn của chủ hàng, cò xử lý")));

        var assigned = await api.PostAsync($"/api/shipments/{id}/broker", new { brokerUserId = broker.UserId, version }, shipper.AccessToken);
        AssertOk(assigned);
        AssertOk(await api.PostAsync($"/api/shipments/{id}/broker/accept", new { version = assigned.Data.GetProperty("version").GetInt64() }, broker.AccessToken));

        var requestId = await _kit.RequestAsync(id, driver, vehicleId);
        var tripId = await _kit.AcceptAsync(broker, requestId);

        AssertOk(await api.PostAsync($"/api/trips/{tripId}/start", new { version = await _kit.TripVersionAsync(tripId, driver) }, driver.AccessToken));

        var brokerNotifications = await api.GetAsync("/api/notifications", broker.AccessToken);
        AssertOk(brokerNotifications);
        Assert.Contains(brokerNotifications.Data.GetProperty("items").EnumerateArray(), x => x.GetProperty("type").GetString() == "trip.start");

        var shipperNotifications = await api.GetAsync("/api/notifications", shipper.AccessToken);
        AssertOk(shipperNotifications);
        Assert.Contains(shipperNotifications.Data.GetProperty("items").EnumerateArray(), x => x.GetProperty("type").GetString() == "trip.start");
    }

    /// <summary>"Báo cáo tài xế" used to be write-only, visible only to the reporter; batch 6 adds the admin queue
    /// that was disclosed as missing.</summary>
    [Fact]
    public async Task An_admin_lists_open_driver_reports_and_resolves_one_while_a_broker_cannot_reach_the_queue_at_all()
    {
        var (driver, _) = await _kit.ApprovedDriverAsync("Chị Mười Chín");
        var broker = await _kit.BrokerAsync();
        var admin = await api.WithRoleAsync(await api.RegisterAsync(), "Admin");

        var reported = await api.PostAsync($"/api/broker/drivers/{driver.UserId}/reports",
            new { category = "NoShow", description = "Không đến lấy hàng đúng giờ hẹn" }, broker.AccessToken);
        AssertOk(reported);
        var reportId = reported.Data.GetProperty("id").GetGuid();
        Assert.Equal("Open", reported.Data.GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync("/api/admin/driver-reports", broker.AccessToken)).Status);

        var openQueue = await api.GetAsync("/api/admin/driver-reports?status=Open", admin.AccessToken);
        AssertOk(openQueue);
        var listed = openQueue.Data.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == reportId);
        Assert.Equal("Chị Mười Chín", listed.GetProperty("driverDisplayName").GetString());

        var reviewResult = await api.SendAsync(HttpMethod.Put, $"/api/admin/driver-reports/{reportId}/review",
            new { status = "Reviewed", reviewNote = "Đã xác minh, nhắc nhở tài xế" }, admin.AccessToken);
        AssertOk(reviewResult);
        Assert.Equal("Reviewed", reviewResult.Data.GetProperty("status").GetString());
        Assert.Equal("Đã xác minh, nhắc nhở tài xế", reviewResult.Data.GetProperty("reviewNote").GetString());

        var stillOpen = await api.GetAsync("/api/admin/driver-reports?status=Open", admin.AccessToken);
        AssertOk(stillOpen);
        Assert.DoesNotContain(stillOpen.Data.EnumerateArray(), x => x.GetProperty("id").GetGuid() == reportId);
    }
}
