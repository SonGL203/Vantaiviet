using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VantaiViet.CoreApi.Entities;
using Xunit;
using static VantaiViet.CoreApi.Tests.ShipmentTestKit;

namespace VantaiViet.CoreApi.Tests;

/// <summary>
/// Shipper features 21-30 through the real HTTP pipeline on a migrated PostgreSQL database: reposting and copying an order,
/// finding orders by code or route, filtering by status, seeing who asked for an order and their profile, accepting or
/// rejecting them, chatting, phone numbers to call, and tracking an order through its trip.
/// </summary>
public sealed class ShipperFollowUpApiTests(ApiHostFixture api) : IClassFixture<ApiHostFixture>
{
    private readonly ShipmentTestKit _kit = new(api);

    private static string CodeOf(JsonElement shipment)
    {
        return shipment.GetProperty("code").GetString()!;
    }

    private async Task<string[]> MineAsync(TestSession owner, string query)
    {
        var page = await api.GetAsync("/api/shipments/mine?pageSize=100" + query, owner.AccessToken);
        AssertOk(page);
        return page.Data.EnumerateArray().Select(x => x.GetProperty("title").GetString()!).ToArray();
    }

    private async Task<TestSession> ShipperAsync()
    {
        return await api.RegisterAsync();
    }

    [Fact]
    public async Task Every_shipment_gets_a_unique_short_code_that_reads_like_a_reference()
    {
        var owner = await ShipperAsync();
        var (first, _) = await _kit.CreateDraftAsync(owner);
        var (second, _) = await _kit.CreateDraftAsync(owner);

        var codes = new List<string>();
        foreach (var id in new[] { first, second })
        {
            var detail = await api.GetAsync($"/api/shipments/{id}", owner.AccessToken);
            AssertOk(detail);
            codes.Add(CodeOf(detail.Data.GetProperty("shipment")));
        }

        Assert.All(codes, code => Assert.Matches("^VT[0-9]{7}$", code));
        Assert.NotEqual(codes[0], codes[1]);
    }

    [Fact]
    public async Task Search_finds_shipments_by_code_title_or_either_end_of_the_route_and_only_the_owners_own()
    {
        var owner = await ShipperAsync();
        var stranger = await ShipperAsync();
        await _kit.CreateDraftAsync(owner, Body(("title", "Gạo ST25"), ("pickupAddress", "Cần Thơ"), ("deliveryAddress", "Hà Nội")));
        await _kit.CreateDraftAsync(owner, Body(("title", "Xi măng"), ("pickupAddress", "Hải Phòng"), ("deliveryAddress", "Đà Nẵng")));
        await _kit.CreateDraftAsync(stranger, Body(("title", "Gạo của người khác"), ("pickupAddress", "Cần Thơ")));

        var rice = (await api.GetAsync("/api/shipments/mine", owner.AccessToken)).Data.EnumerateArray()
            .Single(x => x.GetProperty("title").GetString() == "Gạo ST25");
        var code = CodeOf(rice);

        Assert.Equal(["Gạo ST25"], await MineAsync(owner, "&q=" + code.ToLowerInvariant()));
        Assert.Equal(["Gạo ST25"], await MineAsync(owner, "&q=" + code[2..].TrimStart('0')));
        Assert.Equal(["Xi măng"], await MineAsync(owner, "&q=" + Uri.EscapeDataString("hải phòng")));
        Assert.Equal(["Gạo ST25"], await MineAsync(owner, "&q=" + Uri.EscapeDataString("hà nội")));
        Assert.Equal(["Xi măng"], await MineAsync(owner, "&q=" + Uri.EscapeDataString("xi")));
        Assert.Empty(await MineAsync(owner, "&q=" + Uri.EscapeDataString("Cần Thơ Hà Nội")));
        Assert.Equal(2, (await MineAsync(owner, "&q=")).Length);
    }

    [Fact]
    public async Task Search_treats_percent_and_underscore_as_plain_characters()
    {
        var owner = await ShipperAsync();
        await _kit.CreateDraftAsync(owner, Body(("title", "Giảm 50% phí")));
        await _kit.CreateDraftAsync(owner, Body(("title", "Hàng thường")));

        Assert.Equal(["Giảm 50% phí"], await MineAsync(owner, "&q=" + Uri.EscapeDataString("50%")));
        // A bare percent sign would match everything if it were a wildcard; here it matches only the title that has one.
        Assert.Equal(["Giảm 50% phí"], await MineAsync(owner, "&q=" + Uri.EscapeDataString("%")));
        Assert.Empty(await MineAsync(owner, "&q=" + Uri.EscapeDataString("_")));
    }

    [Fact]
    public async Task Search_text_longer_than_a_hundred_characters_is_refused()
    {
        var owner = await ShipperAsync();
        var result = await api.GetAsync("/api/shipments/mine?q=" + new string('a', 101), owner.AccessToken);
        AssertRejected(result, HttpStatusCode.BadRequest, "shipment.invalid_query");
    }

    [Fact]
    public async Task Status_filter_narrows_the_list_and_unknown_values_are_refused()
    {
        var owner = await ShipperAsync();
        await _kit.CreateDraftAsync(owner, Body(("title", "Bản nháp")));
        await _kit.CreatePublishedAsync(owner, Body(("title", "Đang tìm")));
        var (cancelId, cancelVersion) = await _kit.CreatePublishedAsync(owner, Body(("title", "Đã hủy")));
        AssertOk(await api.PostAsync($"/api/shipments/{cancelId}/cancel", new { version = cancelVersion }, owner.AccessToken));

        Assert.Equal(["Đang tìm"], await MineAsync(owner, "&status=Published"));
        Assert.Equal(["Bản nháp", "Đã hủy"], (await MineAsync(owner, "&status=Draft,Cancelled")).Order().ToArray());
        Assert.Equal(["Đã hủy"], await MineAsync(owner, "&status=Cancelled&q=" + Uri.EscapeDataString("hủy")));
        Assert.Empty(await MineAsync(owner, "&status=Completed"));
        Assert.Equal(3, (await MineAsync(owner, "")).Length);
        AssertRejected(await api.GetAsync("/api/shipments/mine?status=Nope", owner.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_status");
    }

    [Fact]
    public async Task A_cancelled_shipment_is_reposted_as_a_new_published_one_with_its_photos_and_delivery_window()
    {
        var owner = await ShipperAsync();
        var pickup = DateTimeOffset.UtcNow.AddDays(2);
        var (id, _) = await _kit.CreatePublishedAsync(owner, Body(("pickupAt", pickup), ("deliverByAt", pickup.AddDays(2))));
        AssertOk(await api.PostAsync($"/api/shipments/{id}/photos", new { contentType = "image/jpeg", content = Jpeg }, owner.AccessToken));
        AssertOk(await api.PostAsync($"/api/shipments/{id}/cancel", new { version = await _kit.VersionOfAsync(owner, id) }, owner.AccessToken));

        var cancelled = await api.GetAsync($"/api/shipments/{id}", owner.AccessToken);
        Assert.True(cancelled.Data.GetProperty("actions").GetProperty("canRepost").GetBoolean());
        Assert.True(cancelled.Data.GetProperty("actions").GetProperty("canCopy").GetBoolean());

        var newPickup = DateTimeOffset.UtcNow.AddDays(7);
        var reposted = await api.PostAsync($"/api/shipments/{id}/repost", new { pickupAt = newPickup }, owner.AccessToken);
        AssertOk(reposted);
        var copy = reposted.Data;
        Assert.NotEqual(id, copy.GetProperty("id").GetGuid());
        Assert.Equal("Published", copy.GetProperty("status").GetString());
        Assert.NotEqual(CodeOf(cancelled.Data.GetProperty("shipment")), CodeOf(copy));
        Assert.Equal(1, copy.GetProperty("photoCount").GetInt32());
        Assert.Equal("15000000", copy.GetProperty("proposedPrice").GetString());
        Assert.Equal(20, copy.GetProperty("quantity").GetInt32());
        // The two-day gap between pickup and delivery deadline moves with the new pickup time.
        var span = copy.GetProperty("deliverByAt").GetDateTimeOffset() - copy.GetProperty("pickupAt").GetDateTimeOffset();
        Assert.True(Math.Abs((span - TimeSpan.FromDays(2)).TotalSeconds) < 1, $"Expected a two-day window but got {span}");
        Assert.True(Math.Abs((copy.GetProperty("pickupAt").GetDateTimeOffset() - newPickup).TotalSeconds) < 1);

        var copyId = copy.GetProperty("id").GetGuid();
        var photos = await api.GetAsync($"/api/shipments/{copyId}/photos", owner.AccessToken);
        Assert.Equal(1, photos.Data.GetArrayLength());
        Assert.Equal("Cancelled", await api.QueryDbAsync(db => db.Shipments.Where(x => x.Id == id).Select(x => x.Status).SingleAsync()));
    }

    [Fact]
    public async Task Only_a_cancelled_shipment_can_be_reposted_and_the_new_pickup_must_be_in_the_future()
    {
        var owner = await ShipperAsync();
        var stranger = await ShipperAsync();
        var (openId, openVersion) = await _kit.CreatePublishedAsync(owner);
        var future = DateTimeOffset.UtcNow.AddDays(9);

        AssertRejected(await api.PostAsync($"/api/shipments/{openId}/repost", new { pickupAt = future }, owner.AccessToken), HttpStatusCode.Conflict, "shipment.conflict");
        AssertOk(await api.PostAsync($"/api/shipments/{openId}/cancel", new { version = openVersion }, owner.AccessToken));
        AssertRejected(await api.PostAsync($"/api/shipments/{openId}/repost", new { pickupAt = DateTimeOffset.UtcNow.AddMinutes(-5) }, owner.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_details");
        AssertRejected(await api.PostAsync($"/api/shipments/{openId}/repost", new { pickupAt = future, deliverByAt = future.AddHours(-1) }, owner.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_deliver_by");
        AssertRejected(await api.PostAsync($"/api/shipments/{openId}/repost", new { pickupAt = future }, stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        AssertOk(await api.PostAsync($"/api/shipments/{openId}/repost", new { pickupAt = future, deliverByAt = future.AddDays(1) }, owner.AccessToken));
    }

    [Fact]
    public async Task Copying_any_shipment_makes_an_editable_draft_and_leaves_the_original_alone()
    {
        var owner = await ShipperAsync();
        var stranger = await ShipperAsync();
        var (id, _) = await _kit.CreatePublishedAsync(owner, Body(("title", "Hàng gốc")));
        var future = DateTimeOffset.UtcNow.AddDays(6);

        AssertRejected(await api.PostAsync($"/api/shipments/{id}/copy", new { pickupAt = future }, stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        var copied = await api.PostAsync($"/api/shipments/{id}/copy", new { pickupAt = future }, owner.AccessToken);
        AssertOk(copied);
        Assert.Equal("Draft", copied.Data.GetProperty("status").GetString());
        Assert.Equal("Hàng gốc", copied.Data.GetProperty("title").GetString());
        var copyId = copied.Data.GetProperty("id").GetGuid();
        Assert.NotEqual(id, copyId);

        var detail = await api.GetAsync($"/api/shipments/{copyId}", owner.AccessToken);
        Assert.True(detail.Data.GetProperty("actions").GetProperty("canEdit").GetBoolean());
        Assert.True(detail.Data.GetProperty("actions").GetProperty("canPublish").GetBoolean());
        Assert.Equal("Published", await api.QueryDbAsync(db => db.Shipments.Where(x => x.Id == id).Select(x => x.Status).SingleAsync()));
        Assert.Equal(2, (await MineAsync(owner, "")).Length);
    }

    [Fact]
    public async Task The_owner_sees_every_driver_who_asked_with_vehicle_and_phone_and_nobody_else_can()
    {
        var owner = await ShipperAsync();
        var stranger = await ShipperAsync();
        var (id, _) = await _kit.CreatePublishedAsync(owner);
        var (first, firstVehicle) = await _kit.ApprovedDriverAsync("Anh Ba");
        var (second, secondVehicle) = await _kit.ApprovedDriverAsync("Anh Tư");
        var firstRequest = await _kit.RequestAsync(id, first, firstVehicle);
        await _kit.RequestAsync(id, second, secondVehicle);

        var takers = await api.GetAsync($"/api/shipments/{id}/takers", owner.AccessToken);
        AssertOk(takers);
        var rows = takers.Data.GetProperty("takers").EnumerateArray().ToArray();
        Assert.Equal(2, rows.Length);
        var row = rows.Single(x => x.GetProperty("userId").GetGuid() == first.UserId);
        Assert.Equal("Driver", row.GetProperty("kind").GetString());
        Assert.Equal("Anh Ba", row.GetProperty("displayName").GetString());
        // A number is shared only once the request has been accepted; until then they can write to each other.
        Assert.Equal(JsonValueKind.Null, row.GetProperty("phone").ValueKind);
        Assert.Equal("Pending", row.GetProperty("status").GetString());
        Assert.Equal(firstRequest, row.GetProperty("requestId").GetGuid());
        Assert.Equal(firstVehicle, row.GetProperty("vehicle").GetProperty("id").GetGuid());
        Assert.Equal(5000m, row.GetProperty("vehicle").GetProperty("maxPayloadKg").GetDecimal());
        Assert.False(row.GetProperty("verified").GetBoolean());
        Assert.Equal(0, row.GetProperty("completedTrips").GetInt32());

        AssertRejected(await api.GetAsync($"/api/shipments/{id}/takers", stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        AssertRejected(await api.GetAsync($"/api/shipments/{id}/takers", first.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        AssertRejected(await api.GetAsync($"/api/shipments/{Guid.NewGuid()}/takers", owner.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
    }

    [Fact]
    public async Task The_list_and_the_detail_say_how_many_are_waiting_for_an_answer()
    {
        var owner = await ShipperAsync();
        var broker = await _kit.BrokerAsync();
        var (id, _) = await _kit.CreatePublishedAsync(owner, Body(("title", "Có người chờ")));
        var (quiet, _) = await _kit.CreatePublishedAsync(owner, Body(("title", "Chưa ai hỏi")));
        var (first, firstVehicle) = await _kit.ApprovedDriverAsync();
        var (second, secondVehicle) = await _kit.ApprovedDriverAsync();
        var firstRequest = await _kit.RequestAsync(id, first, firstVehicle);
        await _kit.RequestAsync(id, second, secondVehicle);

        async Task<Dictionary<string, int>> WaitingByTitleAsync()
        {
            var page = await api.GetAsync("/api/shipments/mine?pageSize=100", owner.AccessToken);
            return page.Data.EnumerateArray().ToDictionary(x => x.GetProperty("title").GetString()!, x => x.GetProperty("pendingRequests").GetInt32());
        }

        var counts = await WaitingByTitleAsync();
        Assert.Equal(2, counts["Có người chờ"]);
        Assert.Equal(0, counts["Chưa ai hỏi"]);
        Assert.Equal(2, (await api.GetAsync($"/api/shipments/{id}", owner.AccessToken)).Data.GetProperty("shipment").GetProperty("pendingRequests").GetInt32());

        // A broker who has been offered the job is waiting too; one who has answered no longer is.
        AssertOk(await api.PostAsync($"/api/shipments/{quiet}/broker", new { brokerUserId = broker.UserId, version = await _kit.VersionOfAsync(owner, quiet) }, owner.AccessToken));
        Assert.Equal(1, (await WaitingByTitleAsync())["Chưa ai hỏi"]);

        AssertOk(await api.PostAsync($"/api/transport-requests/{firstRequest}/reject", null, owner.AccessToken));
        Assert.Equal(1, (await WaitingByTitleAsync())["Có người chờ"]);
        await _kit.AcceptAsync(owner, await api.QueryDbAsync(db => db.TransportRequests.Where(x => x.ShipmentId == id && x.Status == "Pending").Select(x => x.Id).SingleAsync()));
        Assert.Equal(0, (await WaitingByTitleAsync())["Có người chờ"]);
    }

    [Fact]
    public async Task The_summary_counts_shipments_by_status_and_the_people_still_waiting_for_an_answer()
    {
        var owner = await ShipperAsync();
        var stranger = await ShipperAsync();
        var empty = await api.GetAsync("/api/shipments/mine/summary", owner.AccessToken);
        AssertOk(empty);
        Assert.Equal([0, 0, 0, 0, 0, 0], new[] { "draft", "published", "booked", "completed", "cancelled", "pendingRequests" }.Select(k => empty.Data.GetProperty(k).GetInt32()).ToArray());

        await _kit.CreateDraftAsync(owner);
        var (open, _) = await _kit.CreatePublishedAsync(owner);
        var (booked, _) = await _kit.CreatePublishedAsync(owner);
        var (cancelId, cancelVersion) = await _kit.CreatePublishedAsync(owner);
        AssertOk(await api.PostAsync($"/api/shipments/{cancelId}/cancel", new { version = cancelVersion }, owner.AccessToken));
        await _kit.CreatePublishedAsync(stranger);

        var (asker, askerVehicle) = await _kit.ApprovedDriverAsync();
        var (chosen, chosenVehicle) = await _kit.ApprovedDriverAsync();
        await _kit.RequestAsync(open, asker, askerVehicle);
        await _kit.AcceptAsync(owner, await _kit.RequestAsync(booked, chosen, chosenVehicle));

        var summary = (await api.GetAsync("/api/shipments/mine/summary", owner.AccessToken)).Data;
        Assert.Equal(1, summary.GetProperty("draft").GetInt32());
        Assert.Equal(1, summary.GetProperty("published").GetInt32());
        Assert.Equal(1, summary.GetProperty("booked").GetInt32());
        Assert.Equal(0, summary.GetProperty("completed").GetInt32());
        Assert.Equal(1, summary.GetProperty("cancelled").GetInt32());
        // Only the open order still has someone to answer.
        Assert.Equal(1, summary.GetProperty("pendingRequests").GetInt32());
    }

    [Fact]
    public async Task A_broker_the_order_was_handed_to_is_listed_beside_the_drivers_until_it_is_revoked()
    {
        var owner = await ShipperAsync();
        var broker = await _kit.BrokerAsync();
        var (id, version) = await _kit.CreatePublishedAsync(owner);
        AssertOk(await api.PostAsync($"/api/shipments/{id}/broker", new { brokerUserId = broker.UserId, version }, owner.AccessToken));

        var row = (await api.GetAsync($"/api/shipments/{id}/takers", owner.AccessToken)).Data.GetProperty("takers").EnumerateArray().Single();
        Assert.Equal("Broker", row.GetProperty("kind").GetString());
        Assert.Equal("Pending", row.GetProperty("status").GetString());
        Assert.Equal(broker.UserId, row.GetProperty("userId").GetGuid());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("vehicle").ValueKind);

        var current = await _kit.VersionOfAsync(owner, id);
        AssertOk(await api.PostAsync($"/api/shipments/{id}/broker/revoke", new { version = current }, owner.AccessToken));
        var revoked = (await api.GetAsync($"/api/shipments/{id}/takers", owner.AccessToken)).Data.GetProperty("takers").EnumerateArray().Single();
        Assert.Equal("Revoked", revoked.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_carrier_profile_shows_verification_licence_vehicles_and_trips_and_only_for_someone_who_asked()
    {
        var owner = await ShipperAsync();
        var stranger = await ShipperAsync();
        var (id, _) = await _kit.CreatePublishedAsync(owner);
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Năm");
        var (bystander, _) = await _kit.ApprovedDriverAsync("Người ngoài");
        await _kit.RequestAsync(id, driver, vehicleId);

        var profile = await api.GetAsync($"/api/shipments/{id}/takers/{driver.UserId}", owner.AccessToken);
        AssertOk(profile);
        Assert.Equal("Anh Năm", profile.Data.GetProperty("displayName").GetString());
        Assert.Equal("Driver", profile.Data.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, profile.Data.GetProperty("phone").ValueKind);
        Assert.Equal("C", profile.Data.GetProperty("driver").GetProperty("licenseClass").GetString());
        Assert.True(profile.Data.GetProperty("driver").GetProperty("licenseValid").GetBoolean());
        Assert.Equal(vehicleId, profile.Data.GetProperty("driver").GetProperty("vehicles")[0].GetProperty("id").GetGuid());
        Assert.False(profile.Data.GetProperty("verified").GetBoolean());
        Assert.True(profile.Data.TryGetProperty("memberSince", out _));

        AssertRejected(await api.GetAsync($"/api/shipments/{id}/takers/{bystander.UserId}", owner.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        AssertRejected(await api.GetAsync($"/api/shipments/{id}/takers/{driver.UserId}", stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        AssertRejected(await api.GetAsync($"/api/shipments/{id}/takers/{owner.UserId}", owner.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
    }

    [Fact]
    public async Task Accepting_one_request_books_the_shipment_and_rejects_the_rest_and_rejecting_only_closes_that_request()
    {
        var owner = await ShipperAsync();
        var stranger = await ShipperAsync();
        var (id, _) = await _kit.CreatePublishedAsync(owner);
        var (winner, winnerVehicle) = await _kit.ApprovedDriverAsync("Được chọn");
        var (loser, loserVehicle) = await _kit.ApprovedDriverAsync("Không được chọn");
        var (declined, declinedVehicle) = await _kit.ApprovedDriverAsync("Bị từ chối");
        var winnerRequest = await _kit.RequestAsync(id, winner, winnerVehicle);
        var loserRequest = await _kit.RequestAsync(id, loser, loserVehicle);
        var declinedRequest = await _kit.RequestAsync(id, declined, declinedVehicle);

        AssertRejected(await api.PostAsync($"/api/transport-requests/{winnerRequest}/accept", null, stranger.AccessToken), HttpStatusCode.NotFound, "booking.not_found");
        AssertOk(await api.PostAsync($"/api/transport-requests/{declinedRequest}/reject", null, owner.AccessToken));
        AssertOk(await api.PostAsync($"/api/transport-requests/{winnerRequest}/accept", null, owner.AccessToken));

        var rows = (await api.GetAsync($"/api/shipments/{id}/takers", owner.AccessToken)).Data.GetProperty("takers").EnumerateArray()
            .ToDictionary(x => x.GetProperty("userId").GetGuid(), x => x.GetProperty("status").GetString());
        Assert.Equal("Accepted", rows[winner.UserId]);
        Assert.Equal("Rejected", rows[loser.UserId]);
        Assert.Equal("Rejected", rows[declined.UserId]);
        // Whoever still needs an answer, or won, is listed before the closed ones.
        var first = (await api.GetAsync($"/api/shipments/{id}/takers", owner.AccessToken)).Data.GetProperty("takers")[0];
        Assert.Equal(winner.UserId, first.GetProperty("userId").GetGuid());
        Assert.Equal("Booked", await api.QueryDbAsync(db => db.Shipments.Where(x => x.Id == id).Select(x => x.Status).SingleAsync()));
        AssertRejected(await api.PostAsync($"/api/transport-requests/{loserRequest}/accept", null, owner.AccessToken), HttpStatusCode.Conflict, "booking.conflict");

        // Connected now: the chosen driver's number is shown (in the list, the profile and the chat), nobody else's.
        var connected = (await api.GetAsync($"/api/shipments/{id}/takers", owner.AccessToken)).Data.GetProperty("takers").EnumerateArray()
            .ToDictionary(x => x.GetProperty("userId").GetGuid(), x => x.GetProperty("phone"));
        Assert.Equal(winner.Phone, connected[winner.UserId].GetString());
        Assert.Equal(JsonValueKind.Null, connected[loser.UserId].ValueKind);
        Assert.Equal(winner.Phone, (await api.GetAsync($"/api/shipments/{id}/takers/{winner.UserId}", owner.AccessToken)).Data.GetProperty("phone").GetString());
        Assert.Equal(JsonValueKind.Null, (await api.GetAsync($"/api/shipments/{id}/takers/{loser.UserId}", owner.AccessToken)).Data.GetProperty("phone").ValueKind);
        var chatWithWinner = await api.GetAsync($"/api/shipments/{id}/messages?carrierUserId={winner.UserId}", owner.AccessToken);
        Assert.Equal(winner.Phone, chatWithWinner.Data.GetProperty("partner").GetProperty("phone").GetString());
    }

    [Fact]
    public async Task Owner_and_carrier_chat_and_a_poll_returns_only_what_is_new()
    {
        var owner = await ShipperAsync();
        var (id, _) = await _kit.CreatePublishedAsync(owner);
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Sáu");
        await _kit.RequestAsync(id, driver, vehicleId);
        var chat = $"/api/shipments/{id}/messages";

        var empty = await api.GetAsync($"{chat}?carrierUserId={driver.UserId}", owner.AccessToken);
        AssertOk(empty);
        Assert.Equal(0, empty.Data.GetProperty("messages").GetArrayLength());
        Assert.True(empty.Data.GetProperty("canSend").GetBoolean());
        Assert.Equal("Anh Sáu", empty.Data.GetProperty("partner").GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, empty.Data.GetProperty("partner").GetProperty("phone").ValueKind);

        var sent = await api.PostAsync(chat, new { body = "  Xe anh vào kho được không?  ", carrierUserId = driver.UserId }, owner.AccessToken);
        AssertOk(sent);
        Assert.Equal("Xe anh vào kho được không?", sent.Data.GetProperty("body").GetString());
        Assert.True(sent.Data.GetProperty("mine").GetBoolean());
        var firstId = sent.Data.GetProperty("id").GetInt64();
        AssertOk(await api.PostAsync(chat, new { body = "Được, xe 5 tấn vào tốt." }, driver.AccessToken));

        var forDriver = await api.GetAsync(chat, driver.AccessToken);
        AssertOk(forDriver);
        var messages = forDriver.Data.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(2, messages.Length);
        Assert.False(messages[0].GetProperty("mine").GetBoolean());
        Assert.True(messages[1].GetProperty("mine").GetBoolean());
        Assert.Equal(owner.UserId, forDriver.Data.GetProperty("partner").GetProperty("userId").GetGuid());
        // Until the job is theirs a driver is not given the owner's number.
        Assert.Equal(JsonValueKind.Null, forDriver.Data.GetProperty("partner").GetProperty("phone").ValueKind);

        var afterFirst = await api.GetAsync($"{chat}?carrierUserId={driver.UserId}&afterId={firstId}", owner.AccessToken);
        var fresh = afterFirst.Data.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Single(fresh);
        Assert.Equal("Được, xe 5 tấn vào tốt.", fresh[0].GetProperty("body").GetString());
        Assert.False(fresh[0].GetProperty("mine").GetBoolean());
    }

    [Fact]
    public async Task A_message_waits_for_an_earlier_writer_of_the_same_conversation_but_not_for_another_conversation()
    {
        var owner = await ShipperAsync();
        var (id, _) = await _kit.CreatePublishedAsync(owner);
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync();
        var (other, otherVehicle) = await _kit.ApprovedDriverAsync();
        await _kit.RequestAsync(id, driver, vehicleId);
        await _kit.RequestAsync(id, other, otherVehicle);
        var chat = $"/api/shipments/{id}/messages";

        // Another writer of this conversation is in the middle of its transaction and holds its turn.
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync();
        await using var holder = await connection.BeginTransactionAsync();
        await using (var take = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))", connection, holder))
        {
            take.Parameters.AddWithValue("key", $"chat:{id:N}:{driver.UserId:N}");
            await take.ExecuteNonQueryAsync();
        }

        var waiting = api.PostAsync(chat, new { body = "Tin nhắn phải chờ", carrierUserId = driver.UserId }, owner.AccessToken);
        // A conversation with someone else is a different queue and is not held up.
        AssertOk(await api.PostAsync(chat, new { body = "Tin nhắn khác cuộc trò chuyện", carrierUserId = other.UserId }, owner.AccessToken));
        await Task.Delay(1500);
        Assert.False(waiting.IsCompleted, "the message must wait for the writer that came first");

        await holder.CommitAsync();
        AssertOk(await waiting);
        Assert.Equal(1, await api.QueryDbAsync(db => db.ShipmentMessages.CountAsync(x => x.ShipmentId == id && x.CarrierUserId == driver.UserId)));
    }

    [Fact]
    public async Task Messages_written_at_the_same_moment_are_never_skipped_by_a_reader_that_follows_the_last_id()
    {
        var owner = await ShipperAsync();
        var (id, _) = await _kit.CreatePublishedAsync(owner);
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync();
        await _kit.RequestAsync(id, driver, vehicleId);
        var chat = $"/api/shipments/{id}/messages";
        const int perSide = 25;

        var senders = Task.WhenAll(Enumerable.Range(0, perSide).SelectMany(i => new[]
        {
            api.PostAsync(chat, new { body = "owner " + i, carrierUserId = driver.UserId }, owner.AccessToken),
            api.PostAsync(chat, new { body = "driver " + i }, driver.AccessToken)
        }));

        // The reader does what the app does: ask for everything after the highest id it has seen, again and again.
        var seen = new HashSet<long>();
        var reader = Task.Run(async () =>
        {
            long cursor = 0;
            while (true)
            {
                var writersDone = senders.IsCompleted;
                var page = await api.GetAsync($"{chat}?carrierUserId={driver.UserId}&afterId={cursor}", owner.AccessToken);
                AssertOk(page);
                foreach (var message in page.Data.GetProperty("messages").EnumerateArray())
                {
                    var messageId = message.GetProperty("id").GetInt64();
                    seen.Add(messageId);
                    cursor = Math.Max(cursor, messageId);
                }

                // One more read after the last writer finished, so nothing committed at the end is left out.
                if (writersDone)
                {
                    return;
                }
            }
        });

        Assert.All(await senders, AssertOk);
        await reader;
        Assert.Equal(perSide * 2, seen.Count);
        Assert.Equal(perSide * 2, await api.QueryDbAsync(db => db.ShipmentMessages.CountAsync(x => x.ShipmentId == id)));
    }

    [Fact]
    public async Task Chat_is_private_validated_and_closes_when_the_request_does_but_history_stays_readable()
    {
        var owner = await ShipperAsync();
        var stranger = await ShipperAsync();
        var (id, _) = await _kit.CreatePublishedAsync(owner);
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync();
        var (rival, rivalVehicle) = await _kit.ApprovedDriverAsync();
        var (bystander, _) = await _kit.ApprovedDriverAsync();
        var request = await _kit.RequestAsync(id, driver, vehicleId);
        await _kit.RequestAsync(id, rival, rivalVehicle);
        var chat = $"/api/shipments/{id}/messages";
        AssertOk(await api.PostAsync(chat, new { body = "Xin chào", carrierUserId = driver.UserId }, owner.AccessToken));

        AssertRejected(await api.GetAsync($"{chat}?carrierUserId={driver.UserId}", stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        AssertRejected(await api.GetAsync(chat, bystander.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        // A rival driver never sees another driver's conversation, and the owner cannot address a stranger.
        Assert.Equal(0, (await api.GetAsync(chat, rival.AccessToken)).Data.GetProperty("messages").GetArrayLength());
        AssertRejected(await api.GetAsync($"{chat}?carrierUserId={bystander.UserId}", owner.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        AssertRejected(await api.GetAsync(chat, owner.AccessToken), HttpStatusCode.BadRequest, "shipment.carrier_required");
        AssertRejected(await api.PostAsync(chat, new { body = "   ", carrierUserId = driver.UserId }, owner.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        AssertRejected(await api.PostAsync(chat, new { body = new string('a', 1001), carrierUserId = driver.UserId }, owner.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        AssertRejected(await api.PostAsync(chat, new { body = "Tin nhắn", carrierUserId = bystander.UserId }, owner.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");

        AssertOk(await api.PostAsync($"/api/transport-requests/{request}/reject", null, owner.AccessToken));
        AssertRejected(await api.PostAsync(chat, new { body = "Còn không?", carrierUserId = driver.UserId }, owner.AccessToken), HttpStatusCode.Conflict, "shipment.chat_closed");
        var history = await api.GetAsync($"{chat}?carrierUserId={driver.UserId}", owner.AccessToken);
        AssertOk(history);
        Assert.Equal(1, history.Data.GetProperty("messages").GetArrayLength());
        Assert.False(history.Data.GetProperty("canSend").GetBoolean());
    }

    [Fact]
    public async Task Once_the_job_is_the_drivers_they_learn_the_owners_number_and_can_keep_chatting()
    {
        var owner = await ShipperAsync();
        var (id, _) = await _kit.CreatePublishedAsync(owner);
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync();
        await _kit.AcceptAsync(owner, await _kit.RequestAsync(id, driver, vehicleId));

        var chat = await api.GetAsync($"/api/shipments/{id}/messages", driver.AccessToken);
        AssertOk(chat);
        Assert.Equal(owner.Phone, chat.Data.GetProperty("partner").GetProperty("phone").GetString());
        Assert.True(chat.Data.GetProperty("canSend").GetBoolean());
        var forOwner = await api.GetAsync($"/api/shipments/{id}/messages?carrierUserId={driver.UserId}", owner.AccessToken);
        Assert.Equal(driver.Phone, forOwner.Data.GetProperty("partner").GetProperty("phone").GetString());
        AssertOk(await api.PostAsync($"/api/shipments/{id}/messages", new { body = "Tôi tới kho lúc 8 giờ." }, driver.AccessToken));
    }

    [Fact]
    public async Task Tracking_follows_the_order_from_posted_through_booking_and_delivery_to_the_owners_confirmation()
    {
        var owner = await ShipperAsync();
        var stranger = await ShipperAsync();
        var (id, _) = await _kit.CreatePublishedAsync(owner);
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Bảy");

        var posted = await api.GetAsync($"/api/shipments/{id}/tracking", owner.AccessToken);
        AssertOk(posted);
        Assert.Equal("Published", posted.Data.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, posted.Data.GetProperty("trip").ValueKind);
        Assert.Matches("^VT[0-9]{7}$", posted.Data.GetProperty("code").GetString()!);
        AssertRejected(await api.GetAsync($"/api/shipments/{id}/tracking", stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");

        var tripId = await _kit.AcceptAsync(owner, await _kit.RequestAsync(id, driver, vehicleId));
        var booked = (await api.GetAsync($"/api/shipments/{id}/tracking", owner.AccessToken)).Data;
        Assert.Equal("Booked", booked.GetProperty("status").GetString());
        var trip = booked.GetProperty("trip");
        Assert.Equal(tripId, trip.GetProperty("tripId").GetGuid());
        Assert.Equal("Assigned", trip.GetProperty("status").GetString());
        Assert.Equal("Anh Bảy", trip.GetProperty("driver").GetProperty("displayName").GetString());
        Assert.Equal(driver.Phone, trip.GetProperty("driver").GetProperty("phone").GetString());
        Assert.StartsWith("T-", trip.GetProperty("vehicle").GetProperty("licensePlate").GetString());
        Assert.False(trip.GetProperty("canConfirmComplete").GetBoolean());
        Assert.Equal(JsonValueKind.Null, trip.GetProperty("latest").ValueKind);

        await _kit.DeliverAsync(tripId, driver);
        var delivered = (await api.GetAsync($"/api/shipments/{id}/tracking", owner.AccessToken)).Data.GetProperty("trip");
        Assert.Equal("Delivered", delivered.GetProperty("status").GetString());
        Assert.True(delivered.GetProperty("canConfirmComplete").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, delivered.GetProperty("startedAt").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, delivered.GetProperty("pickedUpAt").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, delivered.GetProperty("deliveredAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, delivered.GetProperty("completedAt").ValueKind);

        // Only the owner may confirm the delivery is done, and only with the version they last saw.
        var version = delivered.GetProperty("version").GetInt64();
        AssertRejected(await api.PostAsync($"/api/trips/{tripId}/complete", new { version }, driver.AccessToken), HttpStatusCode.Forbidden, "trip.forbidden");
        AssertRejected(await api.PostAsync($"/api/trips/{tripId}/complete", new { version = version - 1 }, owner.AccessToken), HttpStatusCode.Conflict, "trip.conflict");
        AssertOk(await api.PostAsync($"/api/trips/{tripId}/complete", new { version }, owner.AccessToken));

        var done = (await api.GetAsync($"/api/shipments/{id}/tracking", owner.AccessToken)).Data;
        Assert.Equal("Completed", done.GetProperty("status").GetString());
        Assert.Equal("Completed", done.GetProperty("trip").GetProperty("status").GetString());
        Assert.False(done.GetProperty("trip").GetProperty("canConfirmComplete").GetBoolean());
    }

    [Fact]
    public async Task Tracking_reports_the_drivers_last_position_while_the_trip_is_under_way()
    {
        var owner = await ShipperAsync();
        var (id, _) = await _kit.CreatePublishedAsync(owner);
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync();
        var tripId = await _kit.AcceptAsync(owner, await _kit.RequestAsync(id, driver, vehicleId));
        AssertOk(await api.PostAsync($"/api/trips/{tripId}/start", new { version = await _kit.TripVersionAsync(tripId, driver) }, driver.AccessToken));

        var pointId = Guid.NewGuid();
        var sent = await api.PostAsync($"/api/tracking/{tripId}/locations",
            new { points = new[] { new { pointId, latitude = 10.85, longitude = 106.77, accuracyMeters = 8, recordedAt = DateTimeOffset.UtcNow, simulated = true } } }, driver.AccessToken);
        AssertOk(sent);

        var trip = (await api.GetAsync($"/api/shipments/{id}/tracking", owner.AccessToken)).Data.GetProperty("trip");
        Assert.Equal("InProgress", trip.GetProperty("status").GetString());
        var latest = trip.GetProperty("latest");
        Assert.Equal(10.85, latest.GetProperty("latitude").GetDouble());
        Assert.Equal(106.77, latest.GetProperty("longitude").GetDouble());
        Assert.False(trip.GetProperty("stale").GetBoolean());
    }
}
