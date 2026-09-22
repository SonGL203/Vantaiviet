using System.Net;
using VantaiViet.CoreApi.Entities;
using Xunit;
using static VantaiViet.CoreApi.Tests.ShipmentTestKit;

namespace VantaiViet.CoreApi.Tests;

/// <summary>
/// Broker features 21-25 through the real HTTP pipeline: commission tracking on the broker's own external orders,
/// the broker's own reputation shown on their profile, reporting a driver, the broker's most-used routes, and the
/// notifications raised by a price proposal's lifecycle and by a saved driver posting a truck.
/// </summary>
public sealed class BrokerManagementApiTests(ApiHostFixture api) : IClassFixture<ApiHostFixture>
{
    private readonly ShipmentTestKit _kit = new(api);

    private async Task<(TestSession Broker, Guid ShipmentId, long Version)> PublishedBrokerShipmentAsync(decimal? commissionRate = 10, string title = "Lô hàng cò nguồn")
    {
        var broker = await _kit.BrokerAsync();
        var (id, version) = await _kit.CreatePublishedAsync(broker,
            Body(("title", title), ("isExternalOrder", true), ("externalCustomerName", "Khách của cò"), ("externalCustomerPhone", "+84900000900"),
                ("proposedPrice", "10000000"), ("brokerCommissionRate", commissionRate)));
        return (broker, id, version);
    }

    [Fact]
    public async Task A_commission_rate_is_rejected_outside_0_to_100_and_carried_through_repost_and_copy()
    {
        var broker = await _kit.BrokerAsync();
        // Out of [0,100] is refused by the request's own [Range] attribute before it reaches the service.
        AssertRejected(
            await api.PostAsync("/api/shipments", Body(("isExternalOrder", true), ("externalCustomerName", "A"), ("externalCustomerPhone", "+84900000811"), ("brokerCommissionRate", 150)), broker.AccessToken),
            HttpStatusCode.BadRequest, "request.validation_failed");

        var (owner, shipmentId, version) = await PublishedBrokerShipmentAsync(commissionRate: 12.5m);
        var detail = await api.GetAsync($"/api/shipments/{shipmentId}", owner.AccessToken);
        AssertOk(detail);
        Assert.Equal(12.5, detail.Data.GetProperty("shipment").GetProperty("brokerCommissionRate").GetDouble());

        var cancelled = await api.PostAsync($"/api/shipments/{shipmentId}/cancel", new { version }, owner.AccessToken);
        AssertOk(cancelled);
        var reposted = await api.PostAsync($"/api/shipments/{shipmentId}/repost", new { pickupAt = DateTimeOffset.UtcNow.AddDays(4) }, owner.AccessToken);
        AssertOk(reposted);
        Assert.Equal(12.5, reposted.Data.GetProperty("brokerCommissionRate").GetDouble());

        var copied = await api.PostAsync($"/api/shipments/{shipmentId}/copy", new { pickupAt = DateTimeOffset.UtcNow.AddDays(5) }, owner.AccessToken);
        AssertOk(copied);
        Assert.Equal(12.5, copied.Data.GetProperty("brokerCommissionRate").GetDouble());
    }

    [Fact]
    public async Task Booking_through_a_direct_request_accrues_commission_the_summary_and_list_both_report()
    {
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Một Bốn", payloadKg: 5000);
        var (broker, shipmentId, _) = await PublishedBrokerShipmentAsync(commissionRate: 10);
        var requestId = await _kit.RequestAsync(shipmentId, driver, vehicleId);
        var tripId = await _kit.AcceptAsync(broker, requestId);

        var listWhileBooked = await api.GetAsync("/api/broker/revenue", broker.AccessToken);
        AssertOk(listWhileBooked);
        var deal = listWhileBooked.Data.EnumerateArray().Single(x => x.GetProperty("shipmentId").GetGuid() == shipmentId);
        Assert.Equal("10000000", deal.GetProperty("finalPrice").GetString());
        Assert.Equal("1000000", deal.GetProperty("commissionAmount").GetString());
        Assert.Equal("Booked", deal.GetProperty("status").GetString());

        await _kit.DeliverAsync(tripId, driver);
        AssertOk(await api.PostAsync($"/api/trips/{tripId}/complete", new { version = await _kit.TripVersionAsync(tripId, broker) }, broker.AccessToken));

        var summary = await api.GetAsync("/api/broker/revenue/summary", broker.AccessToken);
        AssertOk(summary);
        Assert.Equal(1, summary.Data.GetProperty("dealCount").GetInt32());
        Assert.Equal("10000000", summary.Data.GetProperty("totalRevenue").GetString());
        Assert.Equal("1000000", summary.Data.GetProperty("totalCommission").GetString());
    }

    [Fact]
    public async Task Booking_through_an_accepted_price_proposal_uses_the_finalized_price_for_commission()
    {
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Một Năm", payloadKg: 5000);
        var (broker, shipmentId, _) = await PublishedBrokerShipmentAsync(commissionRate: 20);
        var posted = await api.PostAsync("/api/driver/vehicle-availability", new
        {
            vehicleId,
            fromAddress = "Hải Phòng",
            toAddress = "Hà Nội",
            availableFrom = DateTimeOffset.UtcNow.AddHours(1),
            expiresAt = DateTimeOffset.UtcNow.AddDays(2)
        }, driver.AccessToken);
        AssertOk(posted);
        var availabilityId = posted.Data.GetProperty("id").GetGuid();

        var proposed = await api.PostAsync($"/api/shipments/{shipmentId}/proposals", new { vehicleAvailabilityId = availabilityId, proposedPrice = "7000000" }, broker.AccessToken);
        AssertOk(proposed);
        AssertOk(await api.PostAsync($"/api/driver/vehicle-proposals/{proposed.Data.GetProperty("id").GetGuid()}/accept", new { version = 1 }, driver.AccessToken));

        var deal = await api.GetAsync("/api/broker/revenue", broker.AccessToken);
        AssertOk(deal);
        var item = deal.Data.EnumerateArray().Single(x => x.GetProperty("shipmentId").GetGuid() == shipmentId);
        Assert.Equal("7000000", item.GetProperty("finalPrice").GetString());
        Assert.Equal("1400000", item.GetProperty("commissionAmount").GetString());
    }

    [Fact]
    public async Task No_commission_rate_or_still_a_draft_keeps_a_shipment_off_the_revenue_list()
    {
        var broker = await _kit.BrokerAsync();
        await PublishedBrokerShipmentAsync(commissionRate: null, title: "Chưa đặt hoa hồng");
        await _kit.CreateDraftAsync(broker, Body(("isExternalOrder", true), ("externalCustomerName", "B"), ("externalCustomerPhone", "+84900000812"), ("brokerCommissionRate", 15)));

        var list = await api.GetAsync("/api/broker/revenue", broker.AccessToken);
        AssertOk(list);
        Assert.Empty(list.Data.EnumerateArray());
    }

    [Fact]
    public async Task A_brokers_own_reputation_reflects_reviews_a_shipper_left_for_them()
    {
        var shipper = await api.RegisterAsync();
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Một Sáu", payloadKg: 5000);
        var broker = await _kit.BrokerAsync();
        AssertOk(await api.PutAsync("/api/broker/profile", new { operatingArea = "TP.HCM", yearsExperience = 4 }, broker.AccessToken));
        var noReviewsYet = await api.GetAsync("/api/broker/profile", broker.AccessToken);
        AssertOk(noReviewsYet);
        Assert.Equal(0, noReviewsYet.Data.GetProperty("rating").GetProperty("count").GetInt32());

        var (id, version) = await _kit.CreatePublishedAsync(shipper);
        var assigned = await api.PostAsync($"/api/shipments/{id}/broker", new { brokerUserId = broker.UserId, version }, shipper.AccessToken);
        AssertOk(assigned);
        AssertOk(await api.PostAsync($"/api/shipments/{id}/broker/accept", new { version = assigned.Data.GetProperty("version").GetInt64() }, broker.AccessToken));

        var requestId = await _kit.RequestAsync(id, driver, vehicleId);
        var tripId = await _kit.AcceptAsync(shipper, requestId);
        await _kit.DeliverAsync(tripId, driver);
        AssertOk(await api.PostAsync($"/api/trips/{tripId}/complete", new { version = await _kit.TripVersionAsync(tripId, shipper) }, shipper.AccessToken));

        AssertOk(await api.PostAsync($"/api/shipments/{id}/reviews", new { targetUserId = broker.UserId, stars = 4, comment = "Ổn" }, shipper.AccessToken));

        var reviewed = await api.GetAsync("/api/broker/profile", broker.AccessToken);
        AssertOk(reviewed);
        var rating = reviewed.Data.GetProperty("rating");
        Assert.Equal(1, rating.GetProperty("count").GetInt32());
        Assert.Equal(4, rating.GetProperty("average").GetDouble());
    }

    [Fact]
    public async Task Reporting_a_driver_validates_the_category_and_the_driver_and_lists_the_brokers_own_reports()
    {
        var (driver, _) = await _kit.ApprovedDriverAsync("Chị Một Bảy");
        var broker = await _kit.BrokerAsync();

        AssertRejected(
            await api.PostAsync($"/api/broker/drivers/{driver.UserId}/reports", new { category = "NotACategory", description = "Không đến lấy hàng" }, broker.AccessToken),
            HttpStatusCode.BadRequest, "driver_report.invalid_category");
        AssertRejected(
            await api.PostAsync($"/api/broker/drivers/{Guid.NewGuid()}/reports", new { category = "NoShow", description = "Không đến lấy hàng" }, broker.AccessToken),
            HttpStatusCode.BadRequest, "driver_report.driver_not_found");

        var reported = await api.PostAsync($"/api/broker/drivers/{driver.UserId}/reports", new { category = "NoShow", description = "Không đến lấy hàng đúng giờ hẹn" }, broker.AccessToken);
        AssertOk(reported);
        Assert.Equal("Chị Một Bảy", reported.Data.GetProperty("driverDisplayName").GetString());

        var mine = await api.GetAsync("/api/broker/drivers/reports/mine", broker.AccessToken);
        AssertOk(mine);
        Assert.Single(mine.Data.EnumerateArray());
    }

    [Fact]
    public async Task Frequent_routes_group_the_brokers_own_shipments_by_exact_pickup_and_delivery_address()
    {
        var broker = await _kit.BrokerAsync();
        await _kit.CreateDraftAsync(broker, Body(("pickupAddress", "Kho A, Thủ Đức"), ("deliveryAddress", "Kho B, Dĩ An"), ("title", "1")));
        await _kit.CreateDraftAsync(broker, Body(("pickupAddress", "Kho A, Thủ Đức"), ("deliveryAddress", "Kho B, Dĩ An"), ("title", "2")));
        await _kit.CreateDraftAsync(broker, Body(("pickupAddress", "Kho C"), ("deliveryAddress", "Kho D"), ("title", "3")));

        var routes = await api.GetAsync("/api/shipments/mine/frequent-routes", broker.AccessToken);
        AssertOk(routes);
        // Kho C -> Kho D was only posted once, so it is not "thường dùng" yet.
        Assert.Single(routes.Data.EnumerateArray());
        var top = routes.Data.EnumerateArray().First();
        Assert.Equal("Kho A, Thủ Đức", top.GetProperty("pickupAddress").GetString());
        Assert.Equal(2, top.GetProperty("count").GetInt32());

        AssertRejected(await api.GetAsync("/api/shipments/mine/frequent-routes?take=0", broker.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_pagination");
    }

    [Fact]
    public async Task A_proposals_lifecycle_notifies_the_other_side_at_each_step()
    {
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Một Tám", payloadKg: 5000);
        var (broker, shipmentId, _) = await PublishedBrokerShipmentAsync();
        var posted = await api.PostAsync("/api/driver/vehicle-availability", new
        {
            vehicleId,
            fromAddress = "Cần Thơ",
            toAddress = "TP.HCM",
            availableFrom = DateTimeOffset.UtcNow.AddHours(1),
            expiresAt = DateTimeOffset.UtcNow.AddDays(2)
        }, driver.AccessToken);
        AssertOk(posted);
        var availabilityId = posted.Data.GetProperty("id").GetGuid();

        var proposed = await api.PostAsync($"/api/shipments/{shipmentId}/proposals", new { vehicleAvailabilityId = availabilityId, proposedPrice = "5000000" }, broker.AccessToken);
        AssertOk(proposed);
        var driverNotifications = await api.GetAsync("/api/notifications", driver.AccessToken);
        AssertOk(driverNotifications);
        Assert.Contains(driverNotifications.Data.GetProperty("items").EnumerateArray(), x => x.GetProperty("type").GetString() == "vehicle_proposal.received");

        var countered = await api.PostAsync($"/api/driver/vehicle-proposals/{proposed.Data.GetProperty("id").GetGuid()}/counter", new { counterPrice = "5500000", version = 1 }, driver.AccessToken);
        AssertOk(countered);
        var brokerNotifications = await api.GetAsync("/api/notifications", broker.AccessToken);
        AssertOk(brokerNotifications);
        Assert.Contains(brokerNotifications.Data.GetProperty("items").EnumerateArray(), x => x.GetProperty("type").GetString() == "vehicle_proposal.countered");

        var accepted = await api.PostAsync($"/api/broker/vehicle-proposals/{proposed.Data.GetProperty("id").GetGuid()}/accept-counter", new { version = 2 }, broker.AccessToken);
        AssertOk(accepted);
        var driverAfterAccept = await api.GetAsync("/api/notifications", driver.AccessToken);
        AssertOk(driverAfterAccept);
        Assert.Contains(driverAfterAccept.Data.GetProperty("items").EnumerateArray(), x => x.GetProperty("type").GetString() == "vehicle_proposal.accepted");
    }

    [Fact]
    public async Task A_saved_drivers_new_posting_notifies_only_the_broker_who_saved_them()
    {
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Chị Một Chín", payloadKg: 5000);
        var interested = await _kit.BrokerAsync();
        var stranger = await _kit.BrokerAsync();
        AssertOk(await api.PostAsync("/api/broker/drivers", new { driverUserId = driver.UserId }, interested.AccessToken));

        AssertOk(await api.PostAsync("/api/driver/vehicle-availability", new
        {
            vehicleId,
            fromAddress = "Nha Trang",
            toAddress = "Đà Lạt",
            availableFrom = DateTimeOffset.UtcNow.AddHours(1),
            expiresAt = DateTimeOffset.UtcNow.AddDays(2)
        }, driver.AccessToken));

        var interestedNotifications = await api.GetAsync("/api/notifications", interested.AccessToken);
        AssertOk(interestedNotifications);
        Assert.Contains(interestedNotifications.Data.GetProperty("items").EnumerateArray(), x => x.GetProperty("type").GetString() == "saved_driver.available");

        var strangerNotifications = await api.GetAsync("/api/notifications", stranger.AccessToken);
        AssertOk(strangerNotifications);
        Assert.DoesNotContain(strangerNotifications.Data.GetProperty("items").EnumerateArray(), x => x.GetProperty("type").GetString() == "saved_driver.available");
    }
}
