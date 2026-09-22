using System.Net;
using System.Text.Json;
using VantaiViet.CoreApi.Entities;
using Xunit;
using static VantaiViet.CoreApi.Tests.ShipmentTestKit;

namespace VantaiViet.CoreApi.Tests;

/// <summary>
/// Broker features 11-20 through the real HTTP pipeline: richer vehicle search (minimum payload, return-trip priority),
/// a broker's direct price offer on a driver's open posting, the driver's one counter-offer, accepting either side's
/// price to book the driver exactly like a normal request would, and the broker's saved drivers, groups and invite code.
/// </summary>
public sealed class BrokerAdvancedApiTests(ApiHostFixture api) : IClassFixture<ApiHostFixture>
{
    private readonly ShipmentTestKit _kit = new(api);

    private async Task<(TestSession Broker, Guid ShipmentId, long Version)> PublishedBrokerShipmentAsync(string title = "Lô hàng cò nguồn")
    {
        var broker = await _kit.BrokerAsync();
        var (id, version) = await _kit.CreatePublishedAsync(broker,
            Body(("title", title), ("isExternalOrder", true), ("externalCustomerName", "Khách của cò"), ("externalCustomerPhone", "+84900000900")));
        return (broker, id, version);
    }

    private async Task<Guid> PostAvailabilityAsync(TestSession driver, Guid vehicleId, string from = "Hải Phòng", string to = "Hà Nội", bool returnTrip = false)
    {
        var posted = await api.PostAsync("/api/driver/vehicle-availability", new
        {
            vehicleId,
            fromAddress = from,
            toAddress = to,
            availableFrom = DateTimeOffset.UtcNow.AddHours(1),
            expiresAt = DateTimeOffset.UtcNow.AddDays(2),
            isReturnTrip = returnTrip
        }, driver.AccessToken);
        AssertOk(posted);
        return posted.Data.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Searching_can_require_a_minimum_payload_and_prioritizes_return_trip_postings_first()
    {
        var (driverA, vehicleA) = await _kit.ApprovedDriverAsync("Anh Ba", payloadKg: 2000);
        var (driverB, vehicleB) = await _kit.ApprovedDriverAsync("Anh Tư", payloadKg: 9000);
        var broker = await _kit.BrokerAsync();
        await PostAvailabilityAsync(driverA, vehicleA, from: "Đà Nẵng", to: "Huế");
        var returnId = await PostAvailabilityAsync(driverB, vehicleB, from: "Đà Nẵng", to: "Quy Nhơn", returnTrip: true);

        var byPayload = await api.GetAsync("/api/broker/vehicle-availability?fromArea=Đà Nẵng&minPayloadKg=5000", broker.AccessToken);
        AssertOk(byPayload);
        var payloadResults = byPayload.Data.EnumerateArray().ToList();
        Assert.Single(payloadResults);
        Assert.Equal(returnId, payloadResults[0].GetProperty("id").GetGuid());
        Assert.True(payloadResults[0].GetProperty("isReturnTrip").GetBoolean());

        var everything = await api.GetAsync("/api/broker/vehicle-availability?fromArea=Đà Nẵng", broker.AccessToken);
        AssertOk(everything);
        Assert.Equal(returnId, everything.Data.EnumerateArray().First().GetProperty("id").GetGuid());

        var returnOnly = await api.GetAsync("/api/broker/vehicle-availability?fromArea=Đà Nẵng&returnTripOnly=true", broker.AccessToken);
        AssertOk(returnOnly);
        Assert.Single(returnOnly.Data.EnumerateArray());
    }

    [Fact]
    public async Task A_broker_offers_a_price_and_the_driver_accepts_it_and_is_booked()
    {
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Năm", payloadKg: 5000);
        var (broker, shipmentId, _) = await PublishedBrokerShipmentAsync();
        var availabilityId = await PostAvailabilityAsync(driver, vehicleId);

        var proposed = await api.PostAsync($"/api/shipments/{shipmentId}/proposals", new { vehicleAvailabilityId = availabilityId, proposedPrice = "8000000" }, broker.AccessToken);
        AssertOk(proposed);
        Assert.Equal("Pending", proposed.Data.GetProperty("status").GetString());
        Assert.Equal("8000000", proposed.Data.GetProperty("proposedPrice").GetString());
        var proposalId = proposed.Data.GetProperty("id").GetGuid();

        var mine = await api.GetAsync("/api/driver/vehicle-proposals", driver.AccessToken);
        AssertOk(mine);
        Assert.Single(mine.Data.EnumerateArray());

        var accepted = await api.PostAsync($"/api/driver/vehicle-proposals/{proposalId}/accept", new { version = 1 }, driver.AccessToken);
        AssertOk(accepted);
        Assert.Equal("Accepted", accepted.Data.GetProperty("status").GetString());

        var shipment = await api.GetAsync($"/api/shipments/{shipmentId}", broker.AccessToken);
        AssertOk(shipment);
        Assert.Equal("Booked", shipment.Data.GetProperty("shipment").GetProperty("status").GetString());
        Assert.Equal("8000000", shipment.Data.GetProperty("shipment").GetProperty("proposedPrice").GetString());

        var availability = await api.GetAsync("/api/driver/vehicle-availability/mine", driver.AccessToken);
        AssertOk(availability);
        Assert.Equal("Matched", availability.Data.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == availabilityId).GetProperty("status").GetString());

        var takers = await api.GetAsync($"/api/shipments/{shipmentId}/takers", broker.AccessToken);
        AssertOk(takers);
        Assert.Equal(driver.UserId, takers.Data.GetProperty("takers")[0].GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task The_driver_counters_once_and_the_broker_accepting_it_books_at_the_counter_price()
    {
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Sáu", payloadKg: 5000);
        var (broker, shipmentId, _) = await PublishedBrokerShipmentAsync("Lô cần thương lượng");
        var availabilityId = await PostAvailabilityAsync(driver, vehicleId);

        var proposed = await api.PostAsync($"/api/shipments/{shipmentId}/proposals", new { vehicleAvailabilityId = availabilityId, proposedPrice = "7000000" }, broker.AccessToken);
        AssertOk(proposed);
        var proposalId = proposed.Data.GetProperty("id").GetGuid();

        var countered = await api.PostAsync($"/api/driver/vehicle-proposals/{proposalId}/counter", new { counterPrice = "8500000", version = 1 }, driver.AccessToken);
        AssertOk(countered);
        Assert.Equal("CounterOffered", countered.Data.GetProperty("status").GetString());

        // A second counter from the same Pending offer is refused: only one round is allowed.
        AssertRejected(
            await api.PostAsync($"/api/driver/vehicle-proposals/{proposalId}/counter", new { counterPrice = "9000000", version = 2 }, driver.AccessToken),
            HttpStatusCode.Conflict, "vehicle_proposal.conflict");
        // The driver themselves cannot "accept" their own outstanding counter; only the broker's own accept-counter finalizes it.
        AssertRejected(
            await api.PostAsync($"/api/driver/vehicle-proposals/{proposalId}/accept", new { version = 2 }, driver.AccessToken),
            HttpStatusCode.Conflict, "vehicle_proposal.conflict");

        var acceptedCounter = await api.PostAsync($"/api/broker/vehicle-proposals/{proposalId}/accept-counter", new { version = 2 }, broker.AccessToken);
        AssertOk(acceptedCounter);
        Assert.Equal("Accepted", acceptedCounter.Data.GetProperty("status").GetString());

        var shipment = await api.GetAsync($"/api/shipments/{shipmentId}", broker.AccessToken);
        AssertOk(shipment);
        Assert.Equal("8500000", shipment.Data.GetProperty("shipment").GetProperty("proposedPrice").GetString());
        Assert.Equal("Booked", shipment.Data.GetProperty("shipment").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Rejecting_and_withdrawing_close_a_proposal_without_booking_anyone()
    {
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Chị Bảy", payloadKg: 5000);
        var (broker, shipmentId, _) = await PublishedBrokerShipmentAsync();
        var availabilityId = await PostAvailabilityAsync(driver, vehicleId);

        var proposed = await api.PostAsync($"/api/shipments/{shipmentId}/proposals", new { vehicleAvailabilityId = availabilityId, proposedPrice = "6000000" }, broker.AccessToken);
        AssertOk(proposed);
        var rejected = await api.PostAsync($"/api/driver/vehicle-proposals/{proposed.Data.GetProperty("id").GetGuid()}/reject", new { version = 1 }, driver.AccessToken);
        AssertOk(rejected);
        Assert.Equal("Rejected", rejected.Data.GetProperty("status").GetString());

        // The posting is untouched (still Open), so a second, fresh offer can be sent.
        var again = await api.PostAsync($"/api/shipments/{shipmentId}/proposals", new { vehicleAvailabilityId = availabilityId, proposedPrice = "6500000" }, broker.AccessToken);
        AssertOk(again);
        var withdrawn = await api.PostAsync($"/api/broker/vehicle-proposals/{again.Data.GetProperty("id").GetGuid()}/withdraw", new { version = 1 }, broker.AccessToken);
        AssertOk(withdrawn);

        var shipment = await api.GetAsync($"/api/shipments/{shipmentId}", broker.AccessToken);
        AssertOk(shipment);
        Assert.Equal("Published", shipment.Data.GetProperty("shipment").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Proposing_twice_on_the_same_posting_or_on_one_that_is_not_open_is_refused()
    {
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Tám", payloadKg: 5000);
        var (broker, shipmentId, _) = await PublishedBrokerShipmentAsync();
        var availabilityId = await PostAvailabilityAsync(driver, vehicleId);

        AssertOk(await api.PostAsync($"/api/shipments/{shipmentId}/proposals", new { vehicleAvailabilityId = availabilityId, proposedPrice = "5000000" }, broker.AccessToken));
        var second = await _kit.CreatePublishedAsync(broker, Body(("title", "Lô thứ hai"), ("isExternalOrder", true), ("externalCustomerName", "Khách 2"), ("externalCustomerPhone", "+84900000901")));
        AssertRejected(
            await api.PostAsync($"/api/shipments/{second.Id}/proposals", new { vehicleAvailabilityId = availabilityId, proposedPrice = "5500000" }, broker.AccessToken),
            HttpStatusCode.Conflict, "vehicle_proposal.conflict");

        var withdrawnId = Guid.NewGuid();
        AssertRejected(
            await api.PostAsync($"/api/shipments/{shipmentId}/proposals", new { vehicleAvailabilityId = withdrawnId, proposedPrice = "5000000" }, broker.AccessToken),
            HttpStatusCode.BadRequest, "vehicle_proposal.availability_not_open");
    }

    [Fact]
    public async Task Accepting_one_proposal_passes_over_any_other_live_proposal_on_the_same_posting()
    {
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Chín", payloadKg: 5000);
        var brokerOne = await _kit.BrokerAsync();
        var brokerTwo = await _kit.BrokerAsync();
        var availabilityId = await PostAvailabilityAsync(driver, vehicleId);

        var shipmentOne = await _kit.CreatePublishedAsync(brokerOne, Body(("title", "Đơn của cò 1"), ("isExternalOrder", true), ("externalCustomerName", "A"), ("externalCustomerPhone", "+84900000801")));
        var offerOne = await api.PostAsync($"/api/shipments/{shipmentOne.Id}/proposals", new { vehicleAvailabilityId = availabilityId, proposedPrice = "5000000" }, brokerOne.AccessToken);
        AssertOk(offerOne);
        AssertOk(await api.PostAsync($"/api/driver/vehicle-proposals/{offerOne.Data.GetProperty("id").GetGuid()}/reject", new { version = 1 }, driver.AccessToken));

        var offerTwo = await api.PostAsync($"/api/shipments/{shipmentOne.Id}/proposals", new { vehicleAvailabilityId = availabilityId, proposedPrice = "5200000" }, brokerOne.AccessToken);
        var shipmentThree = await _kit.CreatePublishedAsync(brokerTwo, Body(("title", "Đơn của cò 2"), ("isExternalOrder", true), ("externalCustomerName", "B"), ("externalCustomerPhone", "+84900000802")));
        // Sending a second live offer on the same still-Open posting from a different broker is refused too.
        AssertRejected(
            await api.PostAsync($"/api/shipments/{shipmentThree.Id}/proposals", new { vehicleAvailabilityId = availabilityId, proposedPrice = "6000000" }, brokerTwo.AccessToken),
            HttpStatusCode.Conflict, "vehicle_proposal.conflict");

        var accepted = await api.PostAsync($"/api/driver/vehicle-proposals/{offerTwo.Data.GetProperty("id").GetGuid()}/accept", new { version = 1 }, driver.AccessToken);
        AssertOk(accepted);

        var shipmentThreeDetail = await api.GetAsync($"/api/shipments/{shipmentThree.Id}", brokerTwo.AccessToken);
        AssertOk(shipmentThreeDetail);
        Assert.Equal("Published", shipmentThreeDetail.Data.GetProperty("shipment").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Only_the_shipments_manager_may_propose_and_only_the_named_driver_may_answer()
    {
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Mười", payloadKg: 5000);
        var (otherDriver, _) = await _kit.ApprovedDriverAsync("Anh Mười Một", payloadKg: 5000);
        var (broker, shipmentId, _) = await PublishedBrokerShipmentAsync();
        var outsider = await _kit.BrokerAsync();
        var availabilityId = await PostAvailabilityAsync(driver, vehicleId);

        AssertRejected(
            await api.PostAsync($"/api/shipments/{shipmentId}/proposals", new { vehicleAvailabilityId = availabilityId, proposedPrice = "5000000" }, outsider.AccessToken),
            HttpStatusCode.NotFound, "vehicle_proposal.not_found");

        var proposed = await api.PostAsync($"/api/shipments/{shipmentId}/proposals", new { vehicleAvailabilityId = availabilityId, proposedPrice = "5000000" }, broker.AccessToken);
        AssertOk(proposed);
        AssertRejected(
            await api.PostAsync($"/api/driver/vehicle-proposals/{proposed.Data.GetProperty("id").GetGuid()}/accept", new { version = 1 }, otherDriver.AccessToken),
            HttpStatusCode.NotFound, "vehicle_proposal.not_found");
    }

    [Fact]
    public async Task A_broker_assigned_to_someone_elses_shipment_can_also_send_a_proposal()
    {
        var shipper = await api.RegisterAsync();
        var broker = await _kit.BrokerAsync();
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Mười Hai", payloadKg: 5000);
        var (id, version) = await _kit.CreatePublishedAsync(shipper, Body(("title", "Đơn của chủ hàng, cò xử lý")));
        var assigned = await api.PostAsync($"/api/shipments/{id}/broker", new { brokerUserId = broker.UserId, version }, shipper.AccessToken);
        AssertOk(assigned);
        var accepted = await api.PostAsync($"/api/shipments/{id}/broker/accept", new { version = assigned.Data.GetProperty("version").GetInt64() }, broker.AccessToken);
        AssertOk(accepted);

        var availabilityId = await PostAvailabilityAsync(driver, vehicleId);
        var proposed = await api.PostAsync($"/api/shipments/{id}/proposals", new { vehicleAvailabilityId = availabilityId, proposedPrice = "4000000" }, broker.AccessToken);
        AssertOk(proposed);
    }

    [Fact]
    public async Task A_broker_saves_a_driver_favorites_them_and_organizes_them_into_a_group()
    {
        var (driver, _) = await _kit.ApprovedDriverAsync("Chị Mười Ba");
        var broker = await _kit.BrokerAsync();

        var group = await api.PostAsync("/api/broker/driver-groups", new { name = "Container Hải Phòng" }, broker.AccessToken);
        AssertOk(group);
        var groupId = group.Data.GetProperty("id").GetGuid();

        var saved = await api.PostAsync("/api/broker/drivers", new { driverUserId = driver.UserId, groupId, isFavorite = false }, broker.AccessToken);
        AssertOk(saved);
        Assert.Equal("Chị Mười Ba", saved.Data.GetProperty("driverDisplayName").GetString());
        var savedId = saved.Data.GetProperty("id").GetGuid();

        AssertRejected(
            await api.PostAsync("/api/broker/drivers", new { driverUserId = driver.UserId }, broker.AccessToken),
            HttpStatusCode.Conflict, "saved_driver.already_saved");

        var updated = await api.PutAsync($"/api/broker/drivers/{savedId}", new { groupId, isFavorite = true, note = "Chạy tuyến Bắc-Nam ổn định", version = 1 }, broker.AccessToken);
        AssertOk(updated);
        Assert.True(updated.Data.GetProperty("isFavorite").GetBoolean());

        var favoritesOnly = await api.GetAsync("/api/broker/drivers?favoritesOnly=true", broker.AccessToken);
        AssertOk(favoritesOnly);
        Assert.Single(favoritesOnly.Data.EnumerateArray());

        var groups = await api.GetAsync("/api/broker/driver-groups", broker.AccessToken);
        AssertOk(groups);
        Assert.Equal(1, groups.Data.EnumerateArray().Single(g => g.GetProperty("id").GetGuid() == groupId).GetProperty("driverCount").GetInt32());

        AssertOk(await api.SendAsync(HttpMethod.Delete, $"/api/broker/driver-groups/{groupId}", null, broker.AccessToken));
        var afterGroupDeleted = await api.GetAsync("/api/broker/drivers", broker.AccessToken);
        AssertOk(afterGroupDeleted);
        Assert.Equal(JsonValueKind.Null, afterGroupDeleted.Data.EnumerateArray().Single().GetProperty("groupId").ValueKind);

        AssertOk(await api.SendAsync(HttpMethod.Delete, $"/api/broker/drivers/{savedId}", null, broker.AccessToken));
        var afterRemoved = await api.GetAsync("/api/broker/drivers", broker.AccessToken);
        AssertOk(afterRemoved);
        Assert.Empty(afterRemoved.Data.EnumerateArray());
    }

    [Fact]
    public async Task A_driver_who_registers_with_a_brokers_invite_code_is_saved_as_a_referral()
    {
        var broker = await _kit.BrokerAsync();
        var invite = await api.GetAsync("/api/broker/invite", broker.AccessToken);
        AssertOk(invite);
        var code = invite.Data.GetProperty("code").GetString();

        var registered = await api.PostAsync("/api/auth/register", new
        {
            phoneNumber = ApiHostFixture.NewPhone(),
            displayName = "Tài xế Được Mời",
            password = "Original123!",
            accountType = "Driver",
            referralCode = code
        });
        AssertOk(registered);

        var referrals = await api.GetAsync("/api/broker/referrals", broker.AccessToken);
        AssertOk(referrals);
        Assert.Contains(referrals.Data.EnumerateArray(), x => x.GetProperty("displayName").GetString() == "Tài xế Được Mời");

        var saved = await api.GetAsync("/api/broker/drivers", broker.AccessToken);
        AssertOk(saved);
        Assert.Contains(saved.Data.EnumerateArray(), x => x.GetProperty("driverDisplayName").GetString() == "Tài xế Được Mời");
    }

    [Fact]
    public async Task An_unknown_or_missing_invite_code_never_blocks_registration()
    {
        var registered = await api.PostAsync("/api/auth/register", new
        {
            phoneNumber = ApiHostFixture.NewPhone(),
            displayName = "Tài xế Không Mời",
            password = "Original123!",
            accountType = "Driver",
            referralCode = "not-a-guid"
        });
        AssertOk(registered);
    }
}
