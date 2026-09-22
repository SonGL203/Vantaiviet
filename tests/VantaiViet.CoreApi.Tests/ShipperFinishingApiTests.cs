using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static VantaiViet.CoreApi.Tests.ShipmentTestKit;

namespace VantaiViet.CoreApi.Tests;

/// <summary>
/// Shipper features 31-36 through the real HTTP pipeline on a migrated PostgreSQL database: confirming completion, rating the
/// carrier, reporting an incident, notifications, the order's history, and saved addresses.
/// </summary>
public sealed class ShipperFinishingApiTests(ApiHostFixture api) : IClassFixture<ApiHostFixture>
{
    private readonly ShipmentTestKit _kit = new(api);

    private Task<TestSession> ShipperAsync()
    {
        return api.RegisterAsync();
    }

    /// <summary>Publishes a shipment, books the driver and (when asked) drives the trip to Delivered.</summary>
    private async Task<(Guid ShipmentId, Guid TripId)> BookedAsync(TestSession owner, TestSession driver, Guid vehicleId, bool delivered = false)
    {
        var (id, _) = await _kit.CreatePublishedAsync(owner);
        var tripId = await _kit.AcceptAsync(owner, await _kit.RequestAsync(id, driver, vehicleId));
        if (delivered)
        {
            await _kit.DeliverAsync(tripId, driver);
        }

        return (id, tripId);
    }

    private async Task CompleteAsync(TestSession owner, Guid tripId)
    {
        AssertOk(await api.PostAsync($"/api/trips/{tripId}/complete", new { version = await _kit.TripVersionAsync(tripId, owner) }, owner.AccessToken));
    }

    private async Task<JsonElement> NotificationsAsync(TestSession who, string query = "")
    {
        var page = await api.GetAsync("/api/notifications" + query, who.AccessToken);
        AssertOk(page);
        return page.Data;
    }

    private static string[] TypesOf(JsonElement page)
    {
        return page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("type").GetString()!).ToArray();
    }

    // ---------------------------------------------------------------- 31 confirm completion

    [Fact]
    public async Task Only_the_owner_confirms_and_only_after_delivery_and_then_the_shipment_is_completed()
    {
        var owner = await ShipperAsync();
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync();
        var (id, tripId) = await BookedAsync(owner, driver, vehicleId);

        // Not delivered yet, so there is nothing to confirm.
        AssertRejected(await api.PostAsync($"/api/trips/{tripId}/complete", new { version = await _kit.TripVersionAsync(tripId, owner) }, owner.AccessToken),
            HttpStatusCode.Conflict, "trip.invalid_transition");
        await _kit.DeliverAsync(tripId, driver);
        AssertRejected(await api.PostAsync($"/api/trips/{tripId}/complete", new { version = await _kit.TripVersionAsync(tripId, driver) }, driver.AccessToken),
            HttpStatusCode.Forbidden, "trip.forbidden");

        await CompleteAsync(owner, tripId);
        Assert.Equal("Completed", await api.QueryDbAsync(db => db.Shipments.Where(x => x.Id == id).Select(x => x.Status).SingleAsync()));
        // A second confirmation does nothing twice.
        AssertRejected(await api.PostAsync($"/api/trips/{tripId}/complete", new { version = await _kit.TripVersionAsync(tripId, owner) }, owner.AccessToken),
            HttpStatusCode.Conflict, "trip.conflict");
    }

    // ---------------------------------------------------------------- 32 rating

    [Fact]
    public async Task A_carrier_can_be_rated_only_after_completion_by_the_owner_and_only_once()
    {
        var owner = await ShipperAsync();
        var stranger = await ShipperAsync();
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Chín");
        var (id, tripId) = await BookedAsync(owner, driver, vehicleId, delivered: true);
        var reviews = $"/api/shipments/{id}/reviews";

        var open = await api.GetAsync(reviews, owner.AccessToken);
        AssertOk(open);
        Assert.False(open.Data.GetProperty("canReview").GetBoolean());
        Assert.Equal(driver.UserId, open.Data.GetProperty("targets")[0].GetProperty("userId").GetGuid());
        AssertRejected(await api.PostAsync(reviews, new { targetUserId = driver.UserId, stars = 5 }, owner.AccessToken), HttpStatusCode.Conflict, "shipment.conflict");

        await CompleteAsync(owner, tripId);
        Assert.True((await api.GetAsync(reviews, owner.AccessToken)).Data.GetProperty("canReview").GetBoolean());
        AssertRejected(await api.PostAsync(reviews, new { targetUserId = driver.UserId, stars = 0 }, owner.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        AssertRejected(await api.PostAsync(reviews, new { targetUserId = driver.UserId, stars = 6 }, owner.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        AssertRejected(await api.PostAsync(reviews, new { targetUserId = driver.UserId, stars = 4, comment = new string('a', 501) }, owner.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        AssertRejected(await api.PostAsync(reviews, new { targetUserId = Guid.NewGuid(), stars = 5 }, owner.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_target");
        AssertRejected(await api.PostAsync(reviews, new { targetUserId = driver.UserId, stars = 5 }, stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        AssertRejected(await api.PostAsync(reviews, new { targetUserId = driver.UserId, stars = 5 }, driver.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");

        var rated = await api.PostAsync(reviews, new { targetUserId = driver.UserId, stars = 4, comment = "  Giao đúng giờ, hàng nguyên vẹn.  " }, owner.AccessToken);
        AssertOk(rated);
        Assert.Equal(4, rated.Data.GetProperty("stars").GetInt32());
        Assert.Equal("Giao đúng giờ, hàng nguyên vẹn.", rated.Data.GetProperty("comment").GetString());
        AssertRejected(await api.PostAsync(reviews, new { targetUserId = driver.UserId, stars = 5 }, owner.AccessToken), HttpStatusCode.Conflict, "shipment.already_reviewed");

        var after = (await api.GetAsync(reviews, owner.AccessToken)).Data.GetProperty("targets")[0];
        Assert.Equal(4, after.GetProperty("review").GetProperty("stars").GetInt32());
    }

    [Fact]
    public async Task The_broker_who_held_the_job_can_be_rated_too()
    {
        var owner = await ShipperAsync();
        var broker = await _kit.BrokerAsync();
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync();
        var (id, version) = await _kit.CreatePublishedAsync(owner);
        AssertOk(await api.PostAsync($"/api/shipments/{id}/broker", new { brokerUserId = broker.UserId, version }, owner.AccessToken));
        AssertOk(await api.PostAsync($"/api/shipments/{id}/broker/accept", new { version = await _kit.VersionOfAsync(owner, id) }, broker.AccessToken));
        var tripId = await _kit.AcceptAsync(owner, await _kit.RequestAsync(id, driver, vehicleId));
        await _kit.DeliverAsync(tripId, driver);
        await CompleteAsync(owner, tripId);

        var targets = (await api.GetAsync($"/api/shipments/{id}/reviews", owner.AccessToken)).Data.GetProperty("targets").EnumerateArray().ToArray();
        Assert.Equal(["Driver", "Broker"], targets.Select(x => x.GetProperty("kind").GetString()!).ToArray());
        AssertOk(await api.PostAsync($"/api/shipments/{id}/reviews", new { targetUserId = broker.UserId, stars = 5 }, owner.AccessToken));
        AssertOk(await api.PostAsync($"/api/shipments/{id}/reviews", new { targetUserId = driver.UserId, stars = 3 }, owner.AccessToken));
    }

    [Fact]
    public async Task Ratings_add_up_on_the_carriers_profile_and_in_the_list_of_who_asked()
    {
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Mười");
        foreach (var stars in new[] { 5, 4 })
        {
            var past = await ShipperAsync();
            var (id, tripId) = await BookedAsync(past, driver, vehicleId, delivered: true);
            await CompleteAsync(past, tripId);
            AssertOk(await api.PostAsync($"/api/shipments/{id}/reviews", new { targetUserId = driver.UserId, stars, comment = "Ổn " + stars }, past.AccessToken));
        }

        var next = await ShipperAsync();
        var (openId, _) = await _kit.CreatePublishedAsync(next);
        await _kit.RequestAsync(openId, driver, vehicleId);

        var taker = (await api.GetAsync($"/api/shipments/{openId}/takers", next.AccessToken)).Data.GetProperty("takers")[0];
        Assert.Equal(4.5, taker.GetProperty("rating").GetProperty("average").GetDouble());
        Assert.Equal(2, taker.GetProperty("rating").GetProperty("count").GetInt32());
        Assert.Equal(2, taker.GetProperty("completedTrips").GetInt32());

        var profile = (await api.GetAsync($"/api/shipments/{openId}/takers/{driver.UserId}", next.AccessToken)).Data;
        Assert.Equal(4.5, profile.GetProperty("rating").GetProperty("average").GetDouble());
        var recent = profile.GetProperty("recentReviews").EnumerateArray().ToArray();
        Assert.Equal([4, 5], recent.Select(x => x.GetProperty("stars").GetInt32()).ToArray());
        Assert.Equal("Ổn 4", recent[0].GetProperty("comment").GetString());
    }

    [Fact]
    public async Task Someone_never_rated_has_no_average()
    {
        var owner = await ShipperAsync();
        var (id, _) = await _kit.CreatePublishedAsync(owner);
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync();
        await _kit.RequestAsync(id, driver, vehicleId);
        var taker = (await api.GetAsync($"/api/shipments/{id}/takers", owner.AccessToken)).Data.GetProperty("takers")[0];
        Assert.Equal(JsonValueKind.Null, taker.GetProperty("rating").ValueKind);
    }

    // ---------------------------------------------------------------- 33 incidents

    [Fact]
    public async Task An_incident_is_reported_on_a_booked_shipment_kept_listed_and_shown_to_the_driver()
    {
        var owner = await ShipperAsync();
        var stranger = await ShipperAsync();
        var (openId, _) = await _kit.CreatePublishedAsync(owner);
        var incidents = $"/api/shipments/{openId}/incidents";
        AssertRejected(await api.PostAsync(incidents, new { category = "Late", description = "Chưa thấy xe đến" }, owner.AccessToken), HttpStatusCode.Conflict, "shipment.conflict");

        var (driver, vehicleId) = await _kit.ApprovedDriverAsync();
        var (id, tripId) = await BookedAsync(owner, driver, vehicleId, delivered: true);
        incidents = $"/api/shipments/{id}/incidents";
        AssertRejected(await api.PostAsync(incidents, new { category = "Nope", description = "Hàng bị vỡ hai thùng" }, owner.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_details");
        AssertRejected(await api.PostAsync(incidents, new { category = "DamagedCargo", description = "ab" }, owner.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        AssertRejected(await api.PostAsync(incidents, new { category = "DamagedCargo", description = "Hàng bị vỡ hai thùng" }, stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        AssertRejected(await api.PostAsync(incidents, new { category = "DamagedCargo", description = "Hàng bị vỡ hai thùng" }, driver.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");

        var reported = await api.PostAsync(incidents, new { category = "DamagedCargo", description = "  Hàng bị vỡ hai thùng  " }, owner.AccessToken);
        AssertOk(reported);
        Assert.Equal("DamagedCargo", reported.Data.GetProperty("category").GetString());
        Assert.Equal("Hàng bị vỡ hai thùng", reported.Data.GetProperty("description").GetString());
        Assert.Equal("Open", reported.Data.GetProperty("status").GetString());

        var listed = (await api.GetAsync(incidents, owner.AccessToken)).Data.EnumerateArray().ToArray();
        Assert.Single(listed);
        AssertRejected(await api.GetAsync(incidents, stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");

        // The driver reads it in the trip log and is notified.
        var events = (await api.GetAsync($"/api/trips/{tripId}/events", driver.AccessToken)).Data.EnumerateArray().ToArray();
        Assert.Contains(events, e => e.GetProperty("action").GetString() == "Incident" && e.GetProperty("note").GetString()!.Contains("Hàng bị vỡ hai thùng"));
        Assert.Contains("incident.reported", TypesOf(await NotificationsAsync(driver)));
    }

    [Fact]
    public async Task A_shipment_can_only_carry_twenty_reports()
    {
        var owner = await ShipperAsync();
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync();
        var (id, _) = await BookedAsync(owner, driver, vehicleId);
        for (var i = 0; i < 20; i++)
        {
            AssertOk(await api.PostAsync($"/api/shipments/{id}/incidents", new { category = "Other", description = "Sự cố số " + i }, owner.AccessToken));
        }

        AssertRejected(await api.PostAsync($"/api/shipments/{id}/incidents", new { category = "Other", description = "Sự cố thứ hai mươi mốt" }, owner.AccessToken), HttpStatusCode.Conflict, "shipment.limit_reached");
    }

    // ---------------------------------------------------------------- 34 notifications

    [Fact]
    public async Task The_owner_hears_that_a_driver_asked_and_can_read_the_count_the_list_and_mark_it_read()
    {
        var owner = await ShipperAsync();
        var other = await ShipperAsync();
        var (id, _) = await _kit.CreatePublishedAsync(owner);
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Hai");
        await _kit.RequestAsync(id, driver, vehicleId);

        var page = await NotificationsAsync(owner);
        var item = page.GetProperty("items")[0];
        Assert.Equal("request.received", item.GetProperty("type").GetString());
        Assert.Equal(id, item.GetProperty("shipmentId").GetGuid());
        Assert.Equal("Anh Hai", item.GetProperty("actorName").GetString());
        Assert.Matches("^VT[0-9]{7}$", item.GetProperty("subject").GetString()!);
        Assert.False(item.GetProperty("read").GetBoolean());
        Assert.Equal(1, page.GetProperty("unreadCount").GetInt32());
        Assert.Equal(1, (await api.GetAsync("/api/notifications/unread-count", owner.AccessToken)).Data.GetProperty("count").GetInt32());
        Assert.Empty(TypesOf(await NotificationsAsync(other)));
        Assert.Empty(TypesOf(await NotificationsAsync(driver)));

        var notificationId = item.GetProperty("id").GetInt64();
        // Someone else marking it changes nothing.
        AssertOk(await api.PostAsync("/api/notifications/read", new { ids = new[] { notificationId } }, other.AccessToken));
        Assert.Equal(1, (await api.GetAsync("/api/notifications/unread-count", owner.AccessToken)).Data.GetProperty("count").GetInt32());
        var marked = await api.PostAsync("/api/notifications/read", new { ids = new[] { notificationId } }, owner.AccessToken);
        AssertOk(marked);
        Assert.Equal(0, marked.Data.GetProperty("count").GetInt32());
        Assert.True((await NotificationsAsync(owner)).GetProperty("items")[0].GetProperty("read").GetBoolean());
        AssertRejected(await api.PostAsync("/api/notifications/read", new { ids = Array.Empty<long>() }, owner.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
    }

    [Fact]
    public async Task Accepting_one_driver_tells_them_and_tells_the_others_they_were_not_chosen()
    {
        var owner = await ShipperAsync();
        var (id, _) = await _kit.CreatePublishedAsync(owner);
        var (winner, winnerVehicle) = await _kit.ApprovedDriverAsync();
        var (loser, loserVehicle) = await _kit.ApprovedDriverAsync();
        var (declined, declinedVehicle) = await _kit.ApprovedDriverAsync();
        var winnerRequest = await _kit.RequestAsync(id, winner, winnerVehicle);
        await _kit.RequestAsync(id, loser, loserVehicle);
        var declinedRequest = await _kit.RequestAsync(id, declined, declinedVehicle);

        AssertOk(await api.PostAsync($"/api/transport-requests/{declinedRequest}/reject", null, owner.AccessToken));
        await _kit.AcceptAsync(owner, winnerRequest);

        Assert.Equal(["request.accepted"], TypesOf(await NotificationsAsync(winner)));
        Assert.Equal(["request.rejected"], TypesOf(await NotificationsAsync(loser)));
        Assert.Equal(["request.rejected"], TypesOf(await NotificationsAsync(declined)));
    }

    [Fact]
    public async Task Every_stage_of_the_trip_reaches_the_other_side()
    {
        var owner = await ShipperAsync();
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Tám");
        var (_, tripId) = await BookedAsync(owner, driver, vehicleId, delivered: true);

        var ownerTypes = TypesOf(await NotificationsAsync(owner));
        Assert.Equal(["trip.deliver", "trip.pickup", "trip.start", "request.received"], ownerTypes);
        Assert.Equal("Anh Tám", (await NotificationsAsync(owner)).GetProperty("items")[0].GetProperty("actorName").GetString());

        await CompleteAsync(owner, tripId);
        Assert.Equal("trip.complete", TypesOf(await NotificationsAsync(driver))[0]);
    }

    [Fact]
    public async Task Many_chat_messages_make_one_unread_notification_until_it_is_read()
    {
        var owner = await ShipperAsync();
        var (id, _) = await _kit.CreatePublishedAsync(owner);
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync();
        await _kit.RequestAsync(id, driver, vehicleId);
        var chat = $"/api/shipments/{id}/messages";
        foreach (var text in new[] { "Một", "Hai", "Ba" })
        {
            AssertOk(await api.PostAsync(chat, new { body = text, carrierUserId = driver.UserId }, owner.AccessToken));
        }

        var page = await NotificationsAsync(driver);
        Assert.Equal(["chat.message"], TypesOf(page));
        Assert.Equal(1, page.GetProperty("unreadCount").GetInt32());

        AssertOk(await api.PostAsync("/api/notifications/read-all", null, driver.AccessToken));
        Assert.Equal(0, (await api.GetAsync("/api/notifications/unread-count", driver.AccessToken)).Data.GetProperty("count").GetInt32());
        AssertOk(await api.PostAsync(chat, new { body = "Bốn", carrierUserId = driver.UserId }, owner.AccessToken));
        Assert.Equal(["chat.message", "chat.message"], TypesOf(await NotificationsAsync(driver)));
        Assert.Equal(1, (await api.GetAsync("/api/notifications/unread-count", driver.AccessToken)).Data.GetProperty("count").GetInt32());
        // You are never notified of your own words.
        Assert.DoesNotContain("chat.message", TypesOf(await NotificationsAsync(owner)));
    }

    [Fact]
    public async Task Cancelling_an_order_tells_those_who_were_waiting_and_a_broker_is_told_when_handed_or_revoked()
    {
        var owner = await ShipperAsync();
        var broker = await _kit.BrokerAsync();
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync();
        var (id, _) = await _kit.CreatePublishedAsync(owner);
        await _kit.RequestAsync(id, driver, vehicleId);
        // The driver's request moved the shipment on, so the version to hand over is the current one.
        AssertOk(await api.PostAsync($"/api/shipments/{id}/broker", new { brokerUserId = broker.UserId, version = await _kit.VersionOfAsync(owner, id) }, owner.AccessToken));
        Assert.Equal(["broker.assigned"], TypesOf(await NotificationsAsync(broker)));
        AssertOk(await api.PostAsync($"/api/shipments/{id}/broker/accept", new { version = await _kit.VersionOfAsync(owner, id) }, broker.AccessToken));
        Assert.Contains("broker.accepted", TypesOf(await NotificationsAsync(owner)));

        AssertOk(await api.PostAsync($"/api/shipments/{id}/cancel", new { version = await _kit.VersionOfAsync(owner, id) }, owner.AccessToken));
        Assert.Equal("shipment.cancelled", TypesOf(await NotificationsAsync(driver))[0]);
        Assert.Equal("shipment.cancelled", TypesOf(await NotificationsAsync(broker))[0]);
    }

    [Fact]
    public async Task The_list_is_paged_newest_first_can_show_only_unread_and_refuses_bad_paging()
    {
        var owner = await ShipperAsync();
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync();
        // Five orders, each asked for once, make five notifications for the owner.
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var (id, _) = await _kit.CreatePublishedAsync(owner);
            ids.Add(id);
        }

        var (second, secondVehicle) = await _kit.ApprovedDriverAsync();
        for (var i = 0; i < ids.Count; i++)
        {
            await _kit.RequestAsync(ids[i], i % 2 == 0 ? driver : second, i % 2 == 0 ? vehicleId : secondVehicle);
        }

        var first = await NotificationsAsync(owner, "?pageSize=2");
        Assert.Equal(2, first.GetProperty("items").GetArrayLength());
        Assert.True(first.GetProperty("hasMore").GetBoolean());
        Assert.Equal(5, first.GetProperty("unreadCount").GetInt32());
        Assert.Equal(ids[4], first.GetProperty("items")[0].GetProperty("shipmentId").GetGuid());
        var last = first.GetProperty("items")[1].GetProperty("id").GetInt64();
        var next = await NotificationsAsync(owner, $"?pageSize=2&beforeId={last}");
        Assert.Equal([ids[2], ids[1]], next.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("shipmentId").GetGuid()).ToArray());
        var tail = await NotificationsAsync(owner, $"?pageSize=5&beforeId={next.GetProperty("items")[1].GetProperty("id").GetInt64()}");
        Assert.Single(tail.GetProperty("items").EnumerateArray());
        Assert.False(tail.GetProperty("hasMore").GetBoolean());

        AssertOk(await api.PostAsync("/api/notifications/read", new { ids = new[] { last } }, owner.AccessToken));
        Assert.Equal(4, (await NotificationsAsync(owner, "?unreadOnly=true")).GetProperty("items").GetArrayLength());
        AssertRejected(await api.GetAsync("/api/notifications?pageSize=0", owner.AccessToken), HttpStatusCode.BadRequest, "notification.invalid_pagination");
        AssertRejected(await api.GetAsync("/api/notifications?pageSize=31", owner.AccessToken), HttpStatusCode.BadRequest, "notification.invalid_pagination");
        AssertRejected(await api.GetAsync("/api/notifications?beforeId=-1", owner.AccessToken), HttpStatusCode.BadRequest, "notification.invalid_pagination");
    }

    // ---------------------------------------------------------------- 35 history

    [Fact]
    public async Task The_history_lists_what_happened_in_order_with_who_did_it_and_only_to_the_owner()
    {
        var owner = await ShipperAsync();
        var stranger = await ShipperAsync();
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync("Anh Bốn");
        var (id, tripId) = await BookedAsync(owner, driver, vehicleId, delivered: true);
        AssertOk(await api.PostAsync($"/api/shipments/{id}/incidents", new { category = "Late", description = "Giao trễ hai tiếng" }, owner.AccessToken));
        await CompleteAsync(owner, tripId);
        AssertOk(await api.PostAsync($"/api/shipments/{id}/reviews", new { targetUserId = driver.UserId, stars = 3, comment = "Tạm được" }, owner.AccessToken));

        var history = await api.GetAsync($"/api/shipments/{id}/history", owner.AccessToken);
        AssertOk(history);
        Assert.Matches("^VT[0-9]{7}$", history.Data.GetProperty("code").GetString()!);
        Assert.Equal("Completed", history.Data.GetProperty("status").GetString());
        var entries = history.Data.GetProperty("entries").EnumerateArray().ToArray();
        Assert.Equal(
            ["shipment.created", "shipment.publish", "transport.requested", "transport.accepted", "trip.start", "trip.pickup", "trip.deliver", "incident.reported", "trip.complete", "review.created"],
            entries.Select(x => x.GetProperty("action").GetString()!).ToArray());
        Assert.Equal(entries.Select(x => x.GetProperty("at").GetDateTimeOffset()).Order().ToArray(), entries.Select(x => x.GetProperty("at").GetDateTimeOffset()).ToArray());

        var requested = entries.Single(x => x.GetProperty("action").GetString() == "transport.requested");
        Assert.Equal("Anh Bốn", requested.GetProperty("actorName").GetString());
        Assert.False(requested.GetProperty("byMe").GetBoolean());
        Assert.True(entries[0].GetProperty("byMe").GetBoolean());
        var incident = entries.Single(x => x.GetProperty("action").GetString() == "incident.reported");
        Assert.Equal("Late", incident.GetProperty("detail").GetString());
        Assert.Equal("Giao trễ hai tiếng", incident.GetProperty("note").GetString());
        var review = entries.Last();
        Assert.Equal("3", review.GetProperty("detail").GetString());
        Assert.Equal("Tạm được", review.GetProperty("note").GetString());

        AssertRejected(await api.GetAsync($"/api/shipments/{id}/history", stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        AssertRejected(await api.GetAsync($"/api/shipments/{id}/history", driver.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
    }

    [Fact]
    public async Task A_cancelled_order_history_shows_the_cancellation_and_the_finished_ones_are_found_by_status()
    {
        var owner = await ShipperAsync();
        var (id, version) = await _kit.CreatePublishedAsync(owner, Body(("title", "Sẽ hủy")));
        AssertOk(await api.PostAsync($"/api/shipments/{id}/cancel", new { version }, owner.AccessToken));
        var (driver, vehicleId) = await _kit.ApprovedDriverAsync();
        var (doneId, tripId) = await BookedAsync(owner, driver, vehicleId, delivered: true);
        await CompleteAsync(owner, tripId);
        await _kit.CreatePublishedAsync(owner, Body(("title", "Còn mở")));

        var actions = (await api.GetAsync($"/api/shipments/{id}/history", owner.AccessToken)).Data.GetProperty("entries").EnumerateArray()
            .Select(x => x.GetProperty("action").GetString()!).ToArray();
        Assert.Equal(["shipment.created", "shipment.publish", "shipment.cancel"], actions);

        var finished = await api.GetAsync("/api/shipments/mine?status=Completed,Cancelled", owner.AccessToken);
        Assert.Equal(new[] { doneId, id }.Order().ToArray(), finished.Data.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Order().ToArray());
    }

    // ---------------------------------------------------------------- 2 signed-in devices

    [Fact]
    public async Task Signed_in_devices_are_named_listed_and_one_can_be_signed_out_from_another()
    {
        var phone = ApiHostFixture.NewPhone();
        var registered = await api.PostAsync("/api/auth/register", new { phoneNumber = phone, displayName = "Người Test", password = ApiHostFixture.DefaultPassword, accountType = "Shipper", deviceName = "  Windows  " });
        AssertOk(registered);
        var laptop = registered.Data.GetProperty("accessToken").GetString()!;
        var login = await api.PostAsync("/api/auth/login", new { phoneNumber = phone, password = ApiHostFixture.DefaultPassword, deviceName = "Android" });
        AssertOk(login);
        var phoneToken = login.Data.GetProperty("accessToken").GetString()!;

        var listed = await api.GetAsync("/api/auth/sessions", phoneToken);
        AssertOk(listed);
        var rows = listed.Data.EnumerateArray().ToArray();
        Assert.Equal(["Android", "Windows"], rows.Select(x => x.GetProperty("deviceName").GetString()!).ToArray());
        var current = Assert.Single(rows, x => x.GetProperty("current").GetBoolean());
        Assert.Equal("Android", current.GetProperty("deviceName").GetString());

        var laptopSession = rows.Single(x => !x.GetProperty("current").GetBoolean()).GetProperty("id").GetGuid();
        AssertOk(await api.SendAsync(HttpMethod.Delete, $"/api/auth/sessions/{laptopSession}", null, phoneToken));
        Assert.Single((await api.GetAsync("/api/auth/sessions", phoneToken)).Data.EnumerateArray());
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/account/profile", laptop)).Status);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/api/account/profile", phoneToken)).Status);
        AssertRejected(await api.SendAsync(HttpMethod.Delete, $"/api/auth/sessions/{Guid.NewGuid()}", null, phoneToken), HttpStatusCode.NotFound, "auth.not_found");
    }

    [Fact]
    public async Task A_device_name_is_optional_and_stays_with_the_session_when_it_is_refreshed()
    {
        var phone = ApiHostFixture.NewPhone();
        var registered = await api.PostAsync("/api/auth/register", new { phoneNumber = phone, displayName = "Người Test", password = ApiHostFixture.DefaultPassword });
        AssertOk(registered);
        var refreshed = await api.PostAsync("/api/auth/refresh", new { refreshToken = registered.Data.GetProperty("refreshToken").GetString() });
        AssertOk(refreshed);
        var unnamed = Assert.Single((await api.GetAsync("/api/auth/sessions", refreshed.Data.GetProperty("accessToken").GetString())).Data.EnumerateArray());
        Assert.Equal(JsonValueKind.Null, unnamed.GetProperty("deviceName").ValueKind);

        var named = await api.PostAsync("/api/auth/login", new { phoneNumber = phone, password = ApiHostFixture.DefaultPassword, deviceName = "iPhone" });
        AssertOk(named);
        var rotated = await api.PostAsync("/api/auth/refresh", new { refreshToken = named.Data.GetProperty("refreshToken").GetString() });
        AssertOk(rotated);
        var names = (await api.GetAsync("/api/auth/sessions", rotated.Data.GetProperty("accessToken").GetString())).Data.EnumerateArray()
            .Select(x => x.GetProperty("deviceName").ValueKind == JsonValueKind.Null ? "-" : x.GetProperty("deviceName").GetString()!).Order().ToArray();
        Assert.Equal(["-", "iPhone"], names);
        AssertRejected(await api.PostAsync("/api/auth/login", new { phoneNumber = phone, password = ApiHostFixture.DefaultPassword, deviceName = new string('x', 81) }), HttpStatusCode.BadRequest, "request.validation_failed");
    }

    // ---------------------------------------------------------------- 36 saved addresses

    [Fact]
    public async Task Saved_addresses_are_created_listed_edited_and_deleted_and_belong_to_their_owner()
    {
        var owner = await ShipperAsync();
        var other = await ShipperAsync();
        const string path = "/api/account/addresses";

        var kho = await api.PostAsync(path, new { label = "  Kho Thủ Đức  ", address = "  12 Võ Văn Ngân, Thủ Đức, TP.HCM  ", point = new { latitude = 10.85, longitude = 106.77 } }, owner.AccessToken);
        AssertOk(kho);
        Assert.Equal("Kho Thủ Đức", kho.Data.GetProperty("label").GetString());
        Assert.Equal("12 Võ Văn Ngân, Thủ Đức, TP.HCM", kho.Data.GetProperty("address").GetString());
        Assert.Equal(10.85, kho.Data.GetProperty("point").GetProperty("latitude").GetDouble());
        var khoId = kho.Data.GetProperty("id").GetGuid();
        var cua = await api.PostAsync(path, new { label = "Cửa hàng Q1", address = "5 Nguyễn Huệ, Quận 1" }, owner.AccessToken);
        AssertOk(cua);
        Assert.Equal(JsonValueKind.Null, cua.Data.GetProperty("point").ValueKind);

        var list = (await api.GetAsync(path, owner.AccessToken)).Data.EnumerateArray().Select(x => x.GetProperty("label").GetString()!).ToArray();
        Assert.Equal(["Cửa hàng Q1", "Kho Thủ Đức"], list);
        Assert.Equal(0, (await api.GetAsync(path, other.AccessToken)).Data.GetArrayLength());

        var edited = await api.PutAsync($"{path}/{khoId}", new { label = "Kho chính", address = "12 Võ Văn Ngân, Thủ Đức", version = 1 }, owner.AccessToken);
        AssertOk(edited);
        Assert.Equal("Kho chính", edited.Data.GetProperty("label").GetString());
        Assert.Equal(2, edited.Data.GetProperty("version").GetInt64());
        // An edit that sends no point clears it.
        Assert.Equal(JsonValueKind.Null, edited.Data.GetProperty("point").ValueKind);
        AssertRejected(await api.PutAsync($"{path}/{khoId}", new { label = "Kho chính", address = "Địa chỉ mới", version = 1 }, owner.AccessToken), HttpStatusCode.Conflict, "address.conflict");
        AssertRejected(await api.PutAsync($"{path}/{khoId}", new { label = "Kho chính", address = "Địa chỉ mới", version = 2 }, other.AccessToken), HttpStatusCode.NotFound, "address.not_found");
        AssertRejected(await api.SendAsync(HttpMethod.Delete, $"{path}/{khoId}", null, other.AccessToken), HttpStatusCode.NotFound, "address.not_found");

        var remaining = await api.SendAsync(HttpMethod.Delete, $"{path}/{khoId}", null, owner.AccessToken);
        AssertOk(remaining);
        Assert.Equal(["Cửa hàng Q1"], remaining.Data.EnumerateArray().Select(x => x.GetProperty("label").GetString()!).ToArray());
        AssertRejected(await api.SendAsync(HttpMethod.Delete, $"{path}/{khoId}", null, owner.AccessToken), HttpStatusCode.NotFound, "address.not_found");
    }

    [Fact]
    public async Task A_label_is_used_once_whatever_its_letter_case_and_bad_input_is_refused()
    {
        var owner = await ShipperAsync();
        const string path = "/api/account/addresses";
        AssertOk(await api.PostAsync(path, new { label = "Kho A", address = "Địa chỉ kho A" }, owner.AccessToken));
        AssertRejected(await api.PostAsync(path, new { label = "  kho a ", address = "Địa chỉ khác" }, owner.AccessToken), HttpStatusCode.Conflict, "address.duplicate_label");
        var second = await api.PostAsync(path, new { label = "Kho B", address = "Địa chỉ kho B" }, owner.AccessToken);
        AssertOk(second);
        AssertRejected(await api.PutAsync($"{path}/{second.Data.GetProperty("id").GetGuid()}", new { label = "KHO A", address = "Địa chỉ kho B", version = 1 }, owner.AccessToken),
            HttpStatusCode.Conflict, "address.duplicate_label");
        AssertRejected(await api.PostAsync(path, new { label = "   ", address = "Địa chỉ" }, owner.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        AssertRejected(await api.PostAsync(path, new { label = "Kho C", address = "ab" }, owner.AccessToken), HttpStatusCode.BadRequest, "address.invalid_details");
        AssertRejected(await api.PostAsync(path, new { label = new string('a', 61), address = "Địa chỉ" }, owner.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        AssertRejected(await api.PostAsync(path, new { label = "Kho D", address = "Địa chỉ", point = new { latitude = 95, longitude = 10 } }, owner.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
    }

    [Fact]
    public async Task A_shipper_can_keep_thirty_addresses()
    {
        var owner = await ShipperAsync();
        const string path = "/api/account/addresses";
        for (var i = 0; i < 30; i++)
        {
            AssertOk(await api.PostAsync(path, new { label = "Điểm " + i, address = "Địa chỉ số " + i }, owner.AccessToken));
        }

        AssertRejected(await api.PostAsync(path, new { label = "Điểm 30", address = "Địa chỉ số 30" }, owner.AccessToken), HttpStatusCode.Conflict, "address.limit_reached");
    }
}
