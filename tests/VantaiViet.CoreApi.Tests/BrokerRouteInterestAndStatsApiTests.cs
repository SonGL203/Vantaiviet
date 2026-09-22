using System.Net;
using Xunit;
using static VantaiViet.CoreApi.Tests.ShipmentTestKit;

namespace VantaiViet.CoreApi.Tests;

/// <summary>
/// Broker batch 5 through the real HTTP pipeline: "Có hàng phù hợp" (a watched route notifies the broker when a
/// matching shipment is published), revenue grouped by day/month, "tỷ lệ tìm được xe" on the shipment summary, and
/// filtering vehicle search by "loại thùng" (body type).
/// </summary>
public sealed class BrokerRouteInterestAndStatsApiTests(ApiHostFixture api) : IClassFixture<ApiHostFixture>
{
    private readonly ShipmentTestKit _kit = new(api);

    [Fact]
    public async Task A_route_interest_is_validated_limited_and_kept_unique_per_broker()
    {
        var broker = await _kit.BrokerAsync();
        // A whitespace-only field is refused by the request's own [Required] attribute before it reaches the service.
        AssertRejected(await api.PostAsync("/api/broker/route-interests", new { fromArea = "  ", toArea = "Hà Nội" }, broker.AccessToken),
            HttpStatusCode.BadRequest, "request.validation_failed");

        var created = await api.PostAsync("/api/broker/route-interests", new { fromArea = "Hải Phòng", toArea = "Hà Nội" }, broker.AccessToken);
        AssertOk(created);
        Assert.Equal("Hải Phòng", created.Data.GetProperty("fromArea").GetString());

        AssertRejected(await api.PostAsync("/api/broker/route-interests", new { fromArea = "hải phòng", toArea = "hà nội" }, broker.AccessToken),
            HttpStatusCode.Conflict, "route_interest.duplicate_route");

        var list = await api.GetAsync("/api/broker/route-interests", broker.AccessToken);
        AssertOk(list);
        Assert.Single(list.Data.EnumerateArray());

        var id = created.Data.GetProperty("id").GetGuid();
        var afterDelete = await api.SendAsync(HttpMethod.Delete, $"/api/broker/route-interests/{id}", null, broker.AccessToken);
        AssertOk(afterDelete);
        Assert.Empty(afterDelete.Data.EnumerateArray());

        AssertRejected(await api.SendAsync(HttpMethod.Delete, $"/api/broker/route-interests/{id}", null, broker.AccessToken), HttpStatusCode.NotFound, "route_interest.not_found");
    }

    [Fact]
    public async Task Publishing_a_matching_shipment_notifies_only_the_broker_watching_that_route_and_never_the_owner_themselves()
    {
        var watcher = await _kit.BrokerAsync();
        AssertOk(await api.PostAsync("/api/broker/route-interests", new { fromArea = "Hải Phòng", toArea = "Hà Nội" }, watcher.AccessToken));
        // The publisher watches the very same route they are about to publish on; they must never notify themselves.
        var publisher = await _kit.BrokerAsync();
        AssertOk(await api.PostAsync("/api/broker/route-interests", new { fromArea = "Hải Phòng", toArea = "Hà Nội" }, publisher.AccessToken));
        var stranger = await _kit.BrokerAsync();
        AssertOk(await api.PostAsync("/api/broker/route-interests", new { fromArea = "Cần Thơ", toArea = "Cà Mau" }, stranger.AccessToken));

        await _kit.CreatePublishedAsync(publisher, Body(
            ("title", "Lô hàng tuyến Hải Phòng"), ("isExternalOrder", true), ("externalCustomerName", "Khách"), ("externalCustomerPhone", "+84900000920"),
            ("pickupAddress", "Kho X, Hải Phòng"), ("deliveryAddress", "Kho Y, Hà Nội")));

        var watcherNotifications = await api.GetAsync("/api/notifications", watcher.AccessToken);
        AssertOk(watcherNotifications);
        Assert.Contains(watcherNotifications.Data.GetProperty("items").EnumerateArray(), x => x.GetProperty("type").GetString() == "cargo.available");

        var publisherNotifications = await api.GetAsync("/api/notifications", publisher.AccessToken);
        AssertOk(publisherNotifications);
        Assert.DoesNotContain(publisherNotifications.Data.GetProperty("items").EnumerateArray(), x => x.GetProperty("type").GetString() == "cargo.available");

        var strangerNotifications = await api.GetAsync("/api/notifications", stranger.AccessToken);
        AssertOk(strangerNotifications);
        Assert.DoesNotContain(strangerNotifications.Data.GetProperty("items").EnumerateArray(), x => x.GetProperty("type").GetString() == "cargo.available");
    }

    [Fact]
    public async Task Revenue_by_period_groups_todays_deals_together_and_rejects_a_bad_granularity()
    {
        var (driverA, vehicleA) = await _kit.ApprovedDriverAsync("Anh Hai Mốt", payloadKg: 5000);
        var (driverB, vehicleB) = await _kit.ApprovedDriverAsync("Anh Hai Hai", payloadKg: 5000);
        var broker = await _kit.BrokerAsync();
        var (idA, _) = await _kit.CreatePublishedAsync(broker, Body(
            ("title", "Deal A"), ("isExternalOrder", true), ("externalCustomerName", "K"), ("externalCustomerPhone", "+84900000921"),
            ("proposedPrice", "4000000"), ("brokerCommissionRate", 10)));
        var (idB, _) = await _kit.CreatePublishedAsync(broker, Body(
            ("title", "Deal B"), ("isExternalOrder", true), ("externalCustomerName", "K"), ("externalCustomerPhone", "+84900000922"),
            ("proposedPrice", "6000000"), ("brokerCommissionRate", 10)));
        await _kit.AcceptAsync(broker, await _kit.RequestAsync(idA, driverA, vehicleA));
        await _kit.AcceptAsync(broker, await _kit.RequestAsync(idB, driverB, vehicleB));

        var byDay = await api.GetAsync("/api/broker/revenue/by-period?granularity=day", broker.AccessToken);
        AssertOk(byDay);
        var today = byDay.Data.EnumerateArray().Single();
        Assert.Equal(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"), today.GetProperty("period").GetString());
        Assert.Equal(2, today.GetProperty("dealCount").GetInt32());
        Assert.Equal("10000000", today.GetProperty("totalRevenue").GetString());
        Assert.Equal("1000000", today.GetProperty("totalCommission").GetString());

        var byMonth = await api.GetAsync("/api/broker/revenue/by-period?granularity=month", broker.AccessToken);
        AssertOk(byMonth);
        Assert.Equal(DateTimeOffset.UtcNow.ToString("yyyy-MM"), byMonth.Data.EnumerateArray().Single().GetProperty("period").GetString());

        AssertRejected(await api.GetAsync("/api/broker/revenue/by-period?granularity=year", broker.AccessToken), HttpStatusCode.BadRequest, "broker_revenue.invalid_pagination");
    }

    [Fact]
    public async Task Found_vehicle_rate_counts_booked_and_completed_orders_against_everything_ever_sent_out()
    {
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Hai Ba", payloadKg: 5000);
        var broker = await _kit.BrokerAsync();

        // A draft was never sent out, so it must not count on either side of the rate.
        await _kit.CreateDraftAsync(broker, Body(("title", "Còn nháp")));
        var beforeAnyPublish = await api.GetAsync("/api/shipments/mine/summary", broker.AccessToken);
        AssertOk(beforeAnyPublish);
        Assert.True(beforeAnyPublish.Data.GetProperty("foundVehicleRate").ValueKind == System.Text.Json.JsonValueKind.Null);

        var (booked, _) = await _kit.CreatePublishedAsync(broker, Body(("title", "Đã có xe")));
        await _kit.AcceptAsync(broker, await _kit.RequestAsync(booked, driver, vehicleId));
        var (stillWaiting, stillWaitingVersion) = await _kit.CreatePublishedAsync(broker, Body(("title", "Còn chờ xe")));
        var (cancelled, cancelledVersion) = await _kit.CreatePublishedAsync(broker, Body(("title", "Đã hủy")));
        AssertOk(await api.PostAsync($"/api/shipments/{cancelled}/cancel", new { version = cancelledVersion }, broker.AccessToken));

        var summary = await api.GetAsync("/api/shipments/mine/summary", broker.AccessToken);
        AssertOk(summary);
        // 1 of 3 ever-published orders (Booked, Published, Cancelled) found a vehicle: 33.3%.
        Assert.Equal(33.3, summary.Data.GetProperty("foundVehicleRate").GetDouble());
        _ = stillWaiting;
        _ = stillWaitingVersion;
    }

    [Fact]
    public async Task Searching_vehicles_can_be_narrowed_to_one_body_type()
    {
        var (kinDriver, kinVehicle) = await _kit.ApprovedDriverAsync("Anh Hai Bốn", bodyType: "Kin");
        var (batDriver, batVehicle) = await _kit.ApprovedDriverAsync("Anh Hai Năm", bodyType: "Bat");
        var broker = await _kit.BrokerAsync();
        async Task PostAsync(TestSession driver, Guid vehicleId) => AssertOk(await api.PostAsync("/api/driver/vehicle-availability", new
        {
            vehicleId,
            fromAddress = "Vinh",
            toAddress = "Huế",
            availableFrom = DateTimeOffset.UtcNow.AddHours(1),
            expiresAt = DateTimeOffset.UtcNow.AddDays(2)
        }, driver.AccessToken));
        await PostAsync(kinDriver, kinVehicle);
        await PostAsync(batDriver, batVehicle);

        var kinOnly = await api.GetAsync("/api/broker/vehicle-availability?fromArea=Vinh&bodyType=Kin", broker.AccessToken);
        AssertOk(kinOnly);
        var results = kinOnly.Data.EnumerateArray().ToList();
        Assert.Single(results);
        Assert.Equal("Kin", results[0].GetProperty("bodyType").GetString());
        Assert.Equal("Anh Hai Bốn", results[0].GetProperty("driverDisplayName").GetString());

        var everything = await api.GetAsync("/api/broker/vehicle-availability?fromArea=Vinh", broker.AccessToken);
        AssertOk(everything);
        Assert.Equal(2, everything.Data.EnumerateArray().Count());
    }
}
