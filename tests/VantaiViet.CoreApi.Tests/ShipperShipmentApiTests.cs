using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VantaiViet.CoreApi.Entities;
using Xunit;

namespace VantaiViet.CoreApi.Tests;

/// <summary>
/// Shipper features 11-20 through the real HTTP pipeline on a migrated PostgreSQL database: quantity, vehicle type,
/// pickup and delivery deadline, proposed price, notes, cargo photos, and listing, viewing, editing and cancelling shipments.
/// </summary>
public sealed class ShipperShipmentApiTests(ApiHostFixture api) : IClassFixture<ApiHostFixture>
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0xFF, 0xD9];
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0];

    private static void AssertOk(ApiResult result)
    {
        Assert.True(result.Status == HttpStatusCode.OK, $"Expected 200 but got {(int)result.Status}: {result.Body}");
    }

    private static void AssertRejected(ApiResult result, HttpStatusCode status, string errorCode)
    {
        Assert.True(result.Status == status && result.ErrorCode == errorCode,
            $"Expected {(int)status} {errorCode} but got {(int)result.Status} {result.ErrorCode}: {result.Body}");
    }

    /// <summary>A complete request; each override replaces one field, and a null override sends an explicit JSON null.</summary>
    private static Dictionary<string, object?> Body(params (string Key, object? Value)[] overrides)
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
            ["notes"] = "  Giao trong giờ hành chính  "
        };
        foreach (var (key, value) in overrides)
        {
            body[key] = value;
        }

        return body;
    }

    private static Dictionary<string, object?> WithVersion(Dictionary<string, object?> body, long version)
    {
        body["version"] = version;
        return body;
    }

    private static object Image(byte[] bytes, string contentType = "image/jpeg")
    {
        return new { contentType, content = bytes };
    }

    private async Task<(Guid Id, long Version)> CreateDraftAsync(TestSession owner, Dictionary<string, object?>? body = null)
    {
        var created = await api.PostAsync("/api/shipments", body ?? Body(), owner.AccessToken);
        AssertOk(created);
        return (created.Data.GetProperty("id").GetGuid(), created.Data.GetProperty("version").GetInt64());
    }

    private async Task<(Guid Id, long Version)> CreatePublishedAsync(TestSession owner, Dictionary<string, object?>? body = null)
    {
        var (id, version) = await CreateDraftAsync(owner, body);
        var published = await api.PostAsync($"/api/shipments/{id}/publish", new { version }, owner.AccessToken);
        AssertOk(published);
        return (id, published.Data.GetProperty("version").GetInt64());
    }

    private async Task<long> VersionOfAsync(TestSession owner, Guid id)
    {
        var detail = await api.GetAsync($"/api/shipments/{id}", owner.AccessToken);
        AssertOk(detail);
        return detail.Data.GetProperty("shipment").GetProperty("version").GetInt64();
    }

    private async Task<(TestSession Driver, Guid VehicleId)> ApprovedDriverAsync()
    {
        var driver = await api.RegisterAsync("Driver");
        var vehicleId = Guid.NewGuid();
        await api.QueryDbAsync(async db =>
        {
            await db.Users.Where(x => x.Id == driver.UserId).ExecuteUpdateAsync(x => x.SetProperty(u => u.AccountStatus, "Active"));
            db.DriverProfiles.Add(new DriverProfile { UserId = driver.UserId, LicenseClass = "C", LicenseExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), Status = "Approved" });
            db.Vehicles.Add(new Vehicle
            {
                Id = vehicleId,
                DriverUserId = driver.UserId,
                LicensePlate = "T-" + Guid.NewGuid().ToString("N")[..10],
                VehicleType = "Xe tải nhỏ",
                MaxPayloadKg = 5000,
                Status = "Approved"
            });
            await db.SaveChangesAsync();
            return true;
        });
        return (driver, vehicleId);
    }

    private async Task<Guid> RequestAsync(Guid shipmentId, TestSession driver, Guid vehicleId)
    {
        var request = await api.PostAsync($"/api/shipments/{shipmentId}/requests", new { vehicleId }, driver.AccessToken);
        AssertOk(request);
        return request.Data.GetProperty("id").GetGuid();
    }

    private async Task<(HttpStatusCode Status, string? ContentType, byte[] Bytes)> DownloadAsync(string path, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await api.Http.SendAsync(request);
        return (response.StatusCode, response.Content.Headers.ContentType?.MediaType, await response.Content.ReadAsByteArrayAsync());
    }

    private Task<string> ShipmentStatusAsync(Guid id)
    {
        return api.QueryDbAsync(db => db.Shipments.Where(x => x.Id == id).Select(x => x.Status).SingleAsync());
    }

    private Task<string> RequestStatusAsync(Guid id)
    {
        return api.QueryDbAsync(db => db.TransportRequests.Where(x => x.Id == id).Select(x => x.Status).SingleAsync());
    }

    private Task<string> BrokerStatusAsync(Guid shipmentId)
    {
        return api.QueryDbAsync(db => db.Set<ShipmentBroker>().Where(x => x.ShipmentId == shipmentId).Select(x => x.Status).SingleAsync());
    }

    // ---------------------------------------------------------------- 11-15 the details of the goods and the job

    [PostgreSqlFact]
    public async Task ShipmentCarriesQuantityVehicleDeadlinePriceAndNotesAsync()
    {
        var shipper = await api.RegisterAsync();
        var pickupAt = DateTimeOffset.UtcNow.AddDays(3);
        var deliverBy = pickupAt.AddDays(2);
        var created = await api.PostAsync("/api/shipments", Body(("pickupAt", pickupAt), ("deliverByAt", deliverBy)), shipper.AccessToken);
        AssertOk(created);
        var shipment = created.Data;
        Assert.Equal(20, shipment.GetProperty("quantity").GetInt32());
        Assert.Equal("Pallet", shipment.GetProperty("quantityUnit").GetString());
        Assert.Equal("Container", shipment.GetProperty("vehicleType").GetString());
        Assert.Equal(JsonValueKind.String, shipment.GetProperty("proposedPrice").ValueKind);
        Assert.Equal("15000000", shipment.GetProperty("proposedPrice").GetString());
        Assert.Equal("VND", shipment.GetProperty("currency").GetString());
        Assert.Equal("Giao trong giờ hành chính", shipment.GetProperty("notes").GetString());
        Assert.Equal(0, shipment.GetProperty("photoCount").GetInt32());
        Assert.Equal(deliverBy.ToUnixTimeSeconds(), shipment.GetProperty("deliverByAt").GetDateTimeOffset().ToUnixTimeSeconds());
        Assert.Equal(pickupAt.ToUnixTimeSeconds(), shipment.GetProperty("pickupAt").GetDateTimeOffset().ToUnixTimeSeconds());
        Assert.True(shipment.GetProperty("createdAt").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-5));

        // A client that knows nothing about the new fields still posts: the shipment is one package, any vehicle, no price.
        var plain = await api.PostAsync("/api/shipments", new
        {
            title = "Bản cũ", pickupAddress = "A, Hà Nội", deliveryAddress = "B, Hải Phòng", weightKg = 10, pickupAt = DateTimeOffset.UtcNow.AddDays(1)
        }, shipper.AccessToken);
        AssertOk(plain);
        Assert.Equal(1, plain.Data.GetProperty("quantity").GetInt32());
        Assert.Equal("Package", plain.Data.GetProperty("quantityUnit").GetString());
        Assert.Equal("Any", plain.Data.GetProperty("vehicleType").GetString());
        Assert.Equal(JsonValueKind.Null, plain.Data.GetProperty("proposedPrice").ValueKind);
        Assert.Equal(JsonValueKind.Null, plain.Data.GetProperty("deliverByAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, plain.Data.GetProperty("notes").ValueKind);
        Assert.Equal("VND", plain.Data.GetProperty("currency").GetString());

        // Blank notes and a blank price mean "none".
        var blank = await api.PostAsync("/api/shipments", Body(("notes", "   "), ("proposedPrice", "")), shipper.AccessToken);
        AssertOk(blank);
        Assert.Equal(JsonValueKind.Null, blank.Data.GetProperty("notes").ValueKind);
        Assert.Equal(JsonValueKind.Null, blank.Data.GetProperty("proposedPrice").ValueKind);

        foreach (var unit in new[] { "Piece", "Package", "Box", "Pallet", "Bag", "Roll", "Drum" })
        {
            AssertOk(await api.PostAsync("/api/shipments", Body(("quantityUnit", unit)), shipper.AccessToken));
        }

        foreach (var vehicle in new[] { "Any", "Van", "SmallTruck", "MediumTruck", "HeavyTruck", "Refrigerated", "Tractor", "Container", "Tanker" })
        {
            AssertOk(await api.PostAsync("/api/shipments", Body(("vehicleType", vehicle)), shipper.AccessToken));
        }

        var open = await api.PostAsync($"/api/shipments/{shipment.GetProperty("id").GetGuid()}/publish", new { version = shipment.GetProperty("version").GetInt64() }, shipper.AccessToken);
        AssertOk(open);
        var mine = await api.GetAsync("/api/shipments/mine?pageSize=100", shipper.AccessToken);
        var listed = Assert.Single(mine.Data.EnumerateArray(), x => x.GetProperty("id").GetGuid() == shipment.GetProperty("id").GetGuid());
        Assert.Equal("15000000", listed.GetProperty("proposedPrice").GetString());
        Assert.Equal("Container", listed.GetProperty("vehicleType").GetString());
        var driver = await api.RegisterAsync("Driver");
        var marketplace = await api.GetAsync("/api/shipments?pageSize=100", driver.AccessToken);
        var seen = Assert.Single(marketplace.Data.EnumerateArray(), x => x.GetProperty("id").GetGuid() == shipment.GetProperty("id").GetGuid());
        Assert.Equal("Giao trong giờ hành chính", seen.GetProperty("notes").GetString());
        Assert.Equal(20, seen.GetProperty("quantity").GetInt32());
    }

    [PostgreSqlFact]
    public async Task DetailsAreValidatedAndTheDatabaseBacksTheRulesUpAsync()
    {
        var shipper = await api.RegisterAsync();
        foreach (var quantity in new object[] { 0, -3, 1_000_001 })
        {
            AssertRejected(await api.PostAsync("/api/shipments", Body(("quantity", quantity)), shipper.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        }

        AssertRejected(await api.PostAsync("/api/shipments", Body(("quantityUnit", "pallet")), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_quantity_unit");
        AssertRejected(await api.PostAsync("/api/shipments", Body(("quantityUnit", "Truckload")), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_quantity_unit");
        AssertRejected(await api.PostAsync("/api/shipments", Body(("vehicleType", "Spaceship")), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_vehicle_type");
        AssertRejected(await api.PostAsync("/api/shipments", Body(("vehicleType", "container")), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_vehicle_type");

        // Money is an exact whole-dong string: no decimals, signs, separators, leading zeros, more than 11 digits, or JSON numbers.
        foreach (var price in new[] { "0", "-5", "1.5", "1,500", "15 000", "015", "abc", "100000000000", "1e6" })
        {
            AssertRejected(await api.PostAsync("/api/shipments", Body(("proposedPrice", price)), shipper.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsync("/api/shipments", Body(("proposedPrice", 15000000)), shipper.AccessToken)).Status);
        var highest = await api.PostAsync("/api/shipments", Body(("proposedPrice", "99999999999")), shipper.AccessToken);
        AssertOk(highest);
        Assert.Equal("99999999999", highest.Data.GetProperty("proposedPrice").GetString());
        AssertOk(await api.PostAsync("/api/shipments", Body(("proposedPrice", "1")), shipper.AccessToken));

        AssertRejected(await api.PostAsync("/api/shipments", Body(("notes", new string('n', 1001))), shipper.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        AssertOk(await api.PostAsync("/api/shipments", Body(("notes", new string('n', 1000))), shipper.AccessToken));

        var pickupAt = DateTimeOffset.UtcNow.AddDays(3);
        AssertRejected(await api.PostAsync("/api/shipments", Body(("pickupAt", pickupAt), ("deliverByAt", pickupAt.AddHours(-1))), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_deliver_by");
        AssertRejected(await api.PostAsync("/api/shipments", Body(("pickupAt", pickupAt), ("deliverByAt", pickupAt)), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_deliver_by");
        AssertOk(await api.PostAsync("/api/shipments", Body(("pickupAt", pickupAt), ("deliverByAt", pickupAt.AddMinutes(1))), shipper.AccessToken));
        AssertRejected(await api.PostAsync("/api/shipments", Body(("pickupAt", DateTimeOffset.UtcNow.AddMinutes(-1))), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_details");

        var (id, _) = await CreateDraftAsync(shipper);
        foreach (var statement in new[]
        {
            "UPDATE public.\"Shipments\" SET \"Quantity\" = 0 WHERE \"Id\" = {0}",
            "UPDATE public.\"Shipments\" SET \"QuantityUnit\" = 'Truckload' WHERE \"Id\" = {0}",
            "UPDATE public.\"Shipments\" SET \"VehicleType\" = 'Spaceship' WHERE \"Id\" = {0}",
            "UPDATE public.\"Shipments\" SET \"ProposedPrice\" = 0 WHERE \"Id\" = {0}",
            "UPDATE public.\"Shipments\" SET \"Currency\" = 'USD' WHERE \"Id\" = {0}",
            "UPDATE public.\"Shipments\" SET \"DeliverByAt\" = \"PickupAt\" WHERE \"Id\" = {0}"
        })
        {
            var violation = await Assert.ThrowsAsync<PostgresException>(() => api.QueryDbAsync(async db =>
            {
                await db.Database.ExecuteSqlRawAsync(statement, id);
                return true;
            }));
            Assert.Equal(PostgresErrorCodes.CheckViolation, violation.SqlState);
        }
    }

    // ---------------------------------------------------------------- 19 edit

    [PostgreSqlFact]
    public async Task EditingReplacesOrKeepsTheDetailsAndOlderClientsNeverClearThemAsync()
    {
        var shipper = await api.RegisterAsync();
        var (id, version) = await CreateDraftAsync(shipper, Body(("pickup", new { latitude = 10.85, longitude = 106.77 }), ("delivery", new { latitude = 10.91, longitude = 106.76 })));

        // An older client edits only what it knows: the details and points it never heard of stay as they were.
        var older = await api.PutAsync($"/api/shipments/{id}", new
        {
            title = "Tên mới", pickupAddress = "Kho A", deliveryAddress = "Kho B", weightKg = 900, pickupAt = DateTimeOffset.UtcNow.AddDays(4), version
        }, shipper.AccessToken);
        AssertOk(older);
        Assert.Equal("Tên mới", older.Data.GetProperty("title").GetString());
        Assert.Equal(20, older.Data.GetProperty("quantity").GetInt32());
        Assert.Equal("Container", older.Data.GetProperty("vehicleType").GetString());
        Assert.Equal("15000000", older.Data.GetProperty("proposedPrice").GetString());
        Assert.Equal("Giao trong giờ hành chính", older.Data.GetProperty("notes").GetString());
        Assert.NotEqual(JsonValueKind.Null, older.Data.GetProperty("deliverByAt").ValueKind);
        Assert.Equal(10.85, older.Data.GetProperty("pickup").GetProperty("latitude").GetDouble());
        Assert.Equal(106.76, older.Data.GetProperty("delivery").GetProperty("longitude").GetDouble());
        version = older.Data.GetProperty("version").GetInt64();

        // A client that sends the details replaces all of them, and a null clears an optional one.
        var replaced = await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(("quantity", 5), ("quantityUnit", "Box"), ("vehicleType", "Van"),
            ("proposedPrice", null), ("deliverByAt", null), ("notes", "Hàng dễ vỡ")), version), shipper.AccessToken);
        AssertOk(replaced);
        Assert.Equal(5, replaced.Data.GetProperty("quantity").GetInt32());
        Assert.Equal("Box", replaced.Data.GetProperty("quantityUnit").GetString());
        Assert.Equal("Van", replaced.Data.GetProperty("vehicleType").GetString());
        Assert.Equal(JsonValueKind.Null, replaced.Data.GetProperty("proposedPrice").ValueKind);
        Assert.Equal(JsonValueKind.Null, replaced.Data.GetProperty("deliverByAt").ValueKind);
        Assert.Equal("Hàng dễ vỡ", replaced.Data.GetProperty("notes").GetString());
        // The edited address no longer has the old pin.
        Assert.Equal(JsonValueKind.Null, replaced.Data.GetProperty("pickup").ValueKind);
        Assert.Equal(JsonValueKind.Null, replaced.Data.GetProperty("delivery").ValueKind);
        version = replaced.Data.GetProperty("version").GetInt64();

        var pinned = await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(("pickup", new { latitude = 21.03, longitude = 105.85 }), ("delivery", new { latitude = 20.86, longitude = 106.68 })), version), shipper.AccessToken);
        AssertOk(pinned);
        Assert.Equal(21.03, pinned.Data.GetProperty("pickup").GetProperty("latitude").GetDouble());
        Assert.Equal(106.68, pinned.Data.GetProperty("delivery").GetProperty("longitude").GetDouble());
        version = pinned.Data.GetProperty("version").GetInt64();
        AssertRejected(await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(("pickup", new { latitude = 21.03, longitude = 105.85 })), version), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_coordinates");

        AssertRejected(await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(("quantityUnit", null)), version), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_quantity_unit");
        AssertRejected(await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(("vehicleType", null)), version), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_vehicle_type");
        AssertRejected(await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(("vehicleType", "Spaceship")), version), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_vehicle_type");
        AssertRejected(await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(("proposedPrice", "1.5")), version), shipper.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        AssertRejected(await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(("pickupAt", DateTimeOffset.UtcNow.AddHours(-1))), version), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_details");
        Assert.Equal("20 pallet nước giải khát", (await api.GetAsync($"/api/shipments/{id}", shipper.AccessToken)).Data.GetProperty("shipment").GetProperty("title").GetString());

        // Moving the pickup past a deadline that was kept would leave a shipment that delivers before it collects.
        var withDeadline = await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(("deliverByAt", DateTimeOffset.UtcNow.AddDays(6))), version), shipper.AccessToken);
        AssertOk(withDeadline);
        version = withDeadline.Data.GetProperty("version").GetInt64();
        AssertRejected(await api.PutAsync($"/api/shipments/{id}", new
        {
            title = "Đổi giờ", pickupAddress = "Kho A", deliveryAddress = "Kho B", weightKg = 900, pickupAt = DateTimeOffset.UtcNow.AddDays(9), version
        }, shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_deliver_by");

        AssertRejected(await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(), version + 5), shipper.AccessToken), HttpStatusCode.Conflict, "shipment.conflict");
        var stranger = await api.RegisterAsync();
        AssertRejected(await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(), version), stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
    }

    [PostgreSqlFact]
    public async Task APublishedShipmentCanBeEditedUntilSomeoneHasAskedForTheJobAsync()
    {
        var shipper = await api.RegisterAsync();
        var (id, version) = await CreatePublishedAsync(shipper);

        var detail = await api.GetAsync($"/api/shipments/{id}", shipper.AccessToken);
        AssertOk(detail);
        var actions = detail.Data.GetProperty("actions");
        Assert.True(actions.GetProperty("canEdit").GetBoolean());
        Assert.True(actions.GetProperty("canCancel").GetBoolean());
        Assert.False(actions.GetProperty("canPublish").GetBoolean(), "a published shipment cannot be published again");
        Assert.Equal(JsonValueKind.Null, actions.GetProperty("editLockedReason").ValueKind);

        var edited = await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(("title", "Sửa khi đang tìm xe"), ("proposedPrice", "17500000")), version), shipper.AccessToken);
        AssertOk(edited);
        Assert.Equal("Published", edited.Data.GetProperty("status").GetString());
        Assert.Equal("17500000", edited.Data.GetProperty("proposedPrice").GetString());
        version = edited.Data.GetProperty("version").GetInt64();

        var (driver, vehicleId) = await ApprovedDriverAsync();
        var requestId = await RequestAsync(id, driver, vehicleId);

        // The request raised the version, so an edit form opened before it is stale.
        AssertRejected(await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(("title", "Sửa muộn")), version), shipper.AccessToken), HttpStatusCode.Conflict, "shipment.conflict");
        version = await VersionOfAsync(shipper, id);
        AssertRejected(await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(("title", "Sửa muộn")), version), shipper.AccessToken), HttpStatusCode.Conflict, "shipment.has_takers");
        var locked = (await api.GetAsync($"/api/shipments/{id}", shipper.AccessToken)).Data.GetProperty("actions");
        Assert.False(locked.GetProperty("canEdit").GetBoolean());
        Assert.True(locked.GetProperty("canCancel").GetBoolean());
        Assert.Equal("has_takers", locked.GetProperty("editLockedReason").GetString());
        Assert.Equal("Sửa khi đang tìm xe", (await api.GetAsync($"/api/shipments/{id}", shipper.AccessToken)).Data.GetProperty("shipment").GetProperty("title").GetString());

        // Declining the request frees the shipment again.
        AssertOk(await api.PostAsync($"/api/transport-requests/{requestId}/reject", null, shipper.AccessToken));
        version = await VersionOfAsync(shipper, id);
        AssertOk(await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(("title", "Sửa sau khi từ chối")), version), shipper.AccessToken));
    }

    [PostgreSqlFact]
    public async Task ABrokerOfferedTheJobAlsoLocksEditingUntilItIsRevokedAsync()
    {
        var shipper = await api.RegisterAsync();
        var broker = await api.RegisterAsync("Broker");
        var (id, version) = await CreatePublishedAsync(shipper);

        AssertOk(await api.PostAsync($"/api/shipments/{id}/broker", new { brokerUserId = broker.UserId, version }, shipper.AccessToken));
        version = await VersionOfAsync(shipper, id);
        AssertRejected(await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(), version), shipper.AccessToken), HttpStatusCode.Conflict, "shipment.has_takers");

        AssertOk(await api.PostAsync($"/api/shipments/{id}/broker/accept", new { version }, broker.AccessToken));
        version = await VersionOfAsync(shipper, id);
        AssertRejected(await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(), version), shipper.AccessToken), HttpStatusCode.Conflict, "shipment.has_takers");

        AssertOk(await api.PostAsync($"/api/shipments/{id}/broker/revoke", new { version }, shipper.AccessToken));
        version = await VersionOfAsync(shipper, id);
        AssertOk(await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(("title", "Sau khi thu hồi")), version), shipper.AccessToken));
    }

    [PostgreSqlFact]
    public async Task OnlyTheOwnersOpenShipmentsCanBeEditedAsync()
    {
        var shipper = await api.RegisterAsync();
        var (id, version) = await CreatePublishedAsync(shipper);
        var (driver, vehicleId) = await ApprovedDriverAsync();
        var requestId = await RequestAsync(id, driver, vehicleId);
        AssertOk(await api.PostAsync($"/api/transport-requests/{requestId}/accept", null, shipper.AccessToken));
        version = await VersionOfAsync(shipper, id);

        AssertRejected(await api.PutAsync($"/api/shipments/{id}", WithVersion(Body(), version), shipper.AccessToken), HttpStatusCode.Conflict, "shipment.conflict");
        var booked = (await api.GetAsync($"/api/shipments/{id}", shipper.AccessToken)).Data;
        Assert.Equal("Booked", booked.GetProperty("shipment").GetProperty("status").GetString());
        var actions = booked.GetProperty("actions");
        Assert.False(actions.GetProperty("canEdit").GetBoolean());
        Assert.False(actions.GetProperty("canCancel").GetBoolean());
        Assert.False(actions.GetProperty("canPublish").GetBoolean());
        Assert.Equal("booked", actions.GetProperty("editLockedReason").GetString());
        AssertRejected(await api.PostAsync($"/api/shipments/{id}/cancel", new { version }, shipper.AccessToken), HttpStatusCode.Conflict, "shipment.conflict");
        AssertRejected(await api.PostAsync($"/api/shipments/{id}/photos", Image(Jpeg), shipper.AccessToken), HttpStatusCode.Conflict, "shipment.conflict");

        var (cancelledId, cancelledVersion) = await CreateDraftAsync(shipper);
        AssertOk(await api.PostAsync($"/api/shipments/{cancelledId}/cancel", new { version = cancelledVersion }, shipper.AccessToken));
        cancelledVersion = await VersionOfAsync(shipper, cancelledId);
        AssertRejected(await api.PutAsync($"/api/shipments/{cancelledId}", WithVersion(Body(), cancelledVersion), shipper.AccessToken), HttpStatusCode.Conflict, "shipment.conflict");
        AssertRejected(await api.PostAsync($"/api/shipments/{cancelledId}/publish", new { version = cancelledVersion }, shipper.AccessToken), HttpStatusCode.Conflict, "shipment.conflict");
        Assert.Equal("cancelled", (await api.GetAsync($"/api/shipments/{cancelledId}", shipper.AccessToken)).Data.GetProperty("actions").GetProperty("editLockedReason").GetString());
    }

    // ---------------------------------------------------------------- 20 cancel

    [PostgreSqlFact]
    public async Task CancellingClosesWaitingRequestsAndOffersAndRemovesTheShipmentFromTheMarketAsync()
    {
        var shipper = await api.RegisterAsync();
        var broker = await api.RegisterAsync("Broker");
        var (id, version) = await CreatePublishedAsync(shipper);
        var (driver, vehicleId) = await ApprovedDriverAsync();
        var requestId = await RequestAsync(id, driver, vehicleId);
        version = await VersionOfAsync(shipper, id);
        AssertOk(await api.PostAsync($"/api/shipments/{id}/broker", new { brokerUserId = broker.UserId, version }, shipper.AccessToken));
        version = await VersionOfAsync(shipper, id);

        var stranger = await api.RegisterAsync();
        AssertRejected(await api.PostAsync($"/api/shipments/{id}/cancel", new { version }, stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        AssertRejected(await api.PostAsync($"/api/shipments/{id}/cancel", new { version = version - 1 }, shipper.AccessToken), HttpStatusCode.Conflict, "shipment.conflict");
        Assert.Equal("Pending", await RequestStatusAsync(requestId));

        var cancelled = await api.PostAsync($"/api/shipments/{id}/cancel", new { version }, shipper.AccessToken);
        AssertOk(cancelled);
        Assert.Equal("Cancelled", cancelled.Data.GetProperty("status").GetString());
        Assert.Equal("Rejected", await RequestStatusAsync(requestId));
        Assert.Equal("Revoked", await BrokerStatusAsync(id));
        Assert.True(await api.QueryDbAsync(db => db.AuditEvents.AnyAsync(x => x.TargetId == id && x.Action == "shipment.cancel" && x.ActorUserId == shipper.UserId)));

        var market = await api.GetAsync("/api/shipments?pageSize=100", driver.AccessToken);
        Assert.DoesNotContain(market.Data.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        var mine = await api.GetAsync("/api/shipments/mine?pageSize=100", shipper.AccessToken);
        Assert.Equal("Cancelled", Assert.Single(mine.Data.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id).GetProperty("status").GetString());

        version = await VersionOfAsync(shipper, id);
        AssertRejected(await api.PostAsync($"/api/shipments/{id}/cancel", new { version }, shipper.AccessToken), HttpStatusCode.Conflict, "shipment.conflict");
        AssertRejected(await api.PostAsync($"/api/transport-requests/{requestId}/accept", null, shipper.AccessToken), HttpStatusCode.Conflict, "booking.conflict");
        var (late, lateVehicle) = await ApprovedDriverAsync();
        AssertRejected(await api.PostAsync($"/api/shipments/{id}/requests", new { vehicleId = lateVehicle }, late.AccessToken), HttpStatusCode.Conflict, "booking.conflict");
    }

    [PostgreSqlFact]
    public async Task ACancellationAndAConfirmationRacingEachOtherHaveExactlyOneWinnerAsync()
    {
        var shipper = await api.RegisterAsync();
        for (var round = 0; round < 6; round++)
        {
            var (id, _) = await CreatePublishedAsync(shipper);
            var (driver, vehicleId) = await ApprovedDriverAsync();
            var requestId = await RequestAsync(id, driver, vehicleId);
            var version = await VersionOfAsync(shipper, id);

            var results = await Task.WhenAll(
                api.PostAsync($"/api/shipments/{id}/cancel", new { version }, shipper.AccessToken),
                api.PostAsync($"/api/transport-requests/{requestId}/accept", null, shipper.AccessToken));
            Assert.True(results.Count(x => x.Status == HttpStatusCode.OK) == 1,
                $"Round {round}: exactly one of cancel and accept must win, got {results[0].Status} and {results[1].Status}: {results[0].Body} {results[1].Body}");

            var shipmentStatus = await ShipmentStatusAsync(id);
            var requestStatus = await RequestStatusAsync(requestId);
            var bookings = await api.QueryDbAsync(db => db.Bookings.CountAsync(x => x.ShipmentId == id));
            if (shipmentStatus == "Booked")
            {
                Assert.Equal(("Accepted", 1), (requestStatus, bookings));
            }
            else
            {
                Assert.Equal(("Cancelled", "Rejected", 0), (shipmentStatus, requestStatus, bookings));
            }
        }
    }

    // ---------------------------------------------------------------- 16 photos

    [PostgreSqlFact]
    public async Task PhotosAreUploadedListedDownloadedAndRemovedByTheOwnerAsync()
    {
        var shipper = await api.RegisterAsync();
        var (id, version) = await CreateDraftAsync(shipper);
        Assert.Empty((await api.GetAsync($"/api/shipments/{id}/photos", shipper.AccessToken)).Data.EnumerateArray());

        var first = await api.PostAsync($"/api/shipments/{id}/photos", Image(Jpeg), shipper.AccessToken);
        AssertOk(first);
        var second = await api.PostAsync($"/api/shipments/{id}/photos", Image(Png, "image/png"), shipper.AccessToken);
        AssertOk(second);
        var photos = second.Data.EnumerateArray().ToArray();
        Assert.Equal(2, photos.Length);
        Assert.Equal(new[] { "image/jpeg", "image/png" }, photos.Select(x => x.GetProperty("contentType").GetString() ?? string.Empty).ToArray());
        Assert.Equal(new[] { Jpeg.Length, Png.Length }, photos.Select(x => x.GetProperty("sizeBytes").GetInt32()).ToArray());
        Assert.All(photos, x => Assert.False(x.TryGetProperty("content", out _), "a listing never carries the image itself"));

        var jpegId = photos[0].GetProperty("id").GetGuid();
        var pngId = photos[1].GetProperty("id").GetGuid();
        var jpeg = await DownloadAsync($"/api/shipments/{id}/photos/{jpegId}", shipper.AccessToken);
        Assert.Equal((HttpStatusCode.OK, "image/jpeg"), (jpeg.Status, jpeg.ContentType));
        Assert.Equal(Jpeg, jpeg.Bytes);
        var png = await DownloadAsync($"/api/shipments/{id}/photos/{pngId}", shipper.AccessToken);
        Assert.Equal((HttpStatusCode.OK, "image/png"), (png.Status, png.ContentType));
        Assert.Equal(Png, png.Bytes);

        var detail = (await api.GetAsync($"/api/shipments/{id}", shipper.AccessToken)).Data;
        Assert.Equal(2, detail.GetProperty("shipment").GetProperty("photoCount").GetInt32());
        Assert.Equal(2, detail.GetProperty("photos").GetArrayLength());
        var listed = Assert.Single((await api.GetAsync("/api/shipments/mine?pageSize=100", shipper.AccessToken)).Data.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal(2, listed.GetProperty("photoCount").GetInt32());

        // Photos change with the shipment's edits but do not disturb the version an open edit form is holding.
        Assert.Equal(version, await VersionOfAsync(shipper, id));

        var removed = await api.SendAsync(HttpMethod.Delete, $"/api/shipments/{id}/photos/{jpegId}", null, shipper.AccessToken);
        AssertOk(removed);
        Assert.Equal(pngId, Assert.Single(removed.Data.EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await DownloadAsync($"/api/shipments/{id}/photos/{jpegId}", shipper.AccessToken)).Status);
        AssertRejected(await api.SendAsync(HttpMethod.Delete, $"/api/shipments/{id}/photos/{jpegId}", null, shipper.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        AssertRejected(await api.SendAsync(HttpMethod.Delete, $"/api/shipments/{id}/photos/{Guid.NewGuid()}", null, shipper.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        Assert.Equal(1, (await api.GetAsync($"/api/shipments/{id}", shipper.AccessToken)).Data.GetProperty("shipment").GetProperty("photoCount").GetInt32());

        // A draft can be deleted, and its photos go with it.
        AssertOk(await api.SendAsync(HttpMethod.Delete, $"/api/shipments/{id}?version={version}", null, shipper.AccessToken));
        Assert.Equal(0, await api.QueryDbAsync(db => db.ShipmentPhotos.CountAsync(x => x.ShipmentId == id)));
    }

    [PostgreSqlFact]
    public async Task OnlyRealImagesWithinTheLimitsAreAcceptedAsync()
    {
        var shipper = await api.RegisterAsync();
        var (id, _) = await CreateDraftAsync(shipper);
        var path = $"/api/shipments/{id}/photos";

        AssertRejected(await api.PostAsync(path, Image("not an image at all"u8.ToArray()), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_image");
        AssertRejected(await api.PostAsync(path, Image(Png, "image/jpeg"), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_image");
        AssertRejected(await api.PostAsync(path, Image(Jpeg, "image/png"), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_image");
        AssertRejected(await api.PostAsync(path, Image(Jpeg, "image/gif"), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_image");
        AssertRejected(await api.PostAsync(path, Image([0xFF, 0xD8, 0xFF]), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_image");
        var oversized = new byte[(5 * 1024 * 1024) + 1];
        oversized[0] = 0xFF;
        oversized[1] = 0xD8;
        oversized[2] = 0xFF;
        oversized[^2] = 0xFF;
        oversized[^1] = 0xD9;
        AssertRejected(await api.PostAsync(path, Image(oversized), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_image");
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsync(path, new { contentType = "image/jpeg" }, shipper.AccessToken)).Status);
        Assert.Equal(0, await api.QueryDbAsync(db => db.ShipmentPhotos.CountAsync(x => x.ShipmentId == id)));

        for (var i = 0; i < 6; i++)
        {
            AssertOk(await api.PostAsync(path, Image(Jpeg), shipper.AccessToken));
        }

        AssertRejected(await api.PostAsync(path, Image(Jpeg), shipper.AccessToken), HttpStatusCode.Conflict, "shipment.photo_limit");
        Assert.Equal(6, await api.QueryDbAsync(db => db.ShipmentPhotos.CountAsync(x => x.ShipmentId == id)));
        var photoId = (await api.GetAsync(path, shipper.AccessToken)).Data[0].GetProperty("id").GetGuid();
        AssertOk(await api.SendAsync(HttpMethod.Delete, $"{path}/{photoId}", null, shipper.AccessToken));
        AssertOk(await api.PostAsync(path, Image(Png, "image/png"), shipper.AccessToken));
    }

    [PostgreSqlFact]
    public async Task ConcurrentUploadsNeverExceedThePhotoLimitAsync()
    {
        var shipper = await api.RegisterAsync();
        var (id, _) = await CreateDraftAsync(shipper);
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => api.PostAsync($"/api/shipments/{id}/photos", Image(Jpeg), shipper.AccessToken)));
        Assert.Equal(6, results.Count(x => x.Status == HttpStatusCode.OK));
        Assert.All(results.Where(x => x.Status != HttpStatusCode.OK), x => AssertRejected(x, HttpStatusCode.Conflict, "shipment.photo_limit"));
        Assert.Equal(6, await api.QueryDbAsync(db => db.ShipmentPhotos.CountAsync(x => x.ShipmentId == id)));
    }

    [PostgreSqlFact]
    public async Task PhotosAreOnlyVisibleToPeopleWhoMaySeeTheShipmentAndOnlyTheOwnerChangesThemAsync()
    {
        var shipper = await api.RegisterAsync();
        var stranger = await api.RegisterAsync();
        var (id, _) = await CreateDraftAsync(shipper);
        var uploaded = await api.PostAsync($"/api/shipments/{id}/photos", Image(Jpeg), shipper.AccessToken);
        AssertOk(uploaded);
        var photoId = uploaded.Data[0].GetProperty("id").GetGuid();
        var path = $"/api/shipments/{id}/photos";

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync(path)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await DownloadAsync($"{path}/{photoId}", "not-a-token")).Status);
        AssertRejected(await api.GetAsync(path, stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        Assert.Equal(HttpStatusCode.NotFound, (await DownloadAsync($"{path}/{photoId}", stranger.AccessToken)).Status);
        AssertRejected(await api.PostAsync(path, Image(Jpeg), stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        AssertRejected(await api.SendAsync(HttpMethod.Delete, $"{path}/{photoId}", null, stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");

        // A photo id of one shipment never opens through another shipment's address.
        var (otherId, _) = await CreateDraftAsync(shipper);
        Assert.Equal(HttpStatusCode.NotFound, (await DownloadAsync($"/api/shipments/{otherId}/photos/{photoId}", shipper.AccessToken)).Status);

        // Once the shipment is open for requests, anybody browsing the market may look at its photos, but still not change them.
        var (openId, openVersion) = await CreateDraftAsync(shipper);
        var openPhoto = (await api.PostAsync($"/api/shipments/{openId}/photos", Image(Png, "image/png"), shipper.AccessToken)).Data[0].GetProperty("id").GetGuid();
        AssertOk(await api.PostAsync($"/api/shipments/{openId}/publish", new { version = openVersion }, shipper.AccessToken));
        var browsing = await DownloadAsync($"/api/shipments/{openId}/photos/{openPhoto}", stranger.AccessToken);
        Assert.Equal((HttpStatusCode.OK, "image/png"), (browsing.Status, browsing.ContentType));
        Assert.Single((await api.GetAsync($"/api/shipments/{openId}/photos", stranger.AccessToken)).Data.EnumerateArray());
        AssertRejected(await api.PostAsync($"/api/shipments/{openId}/photos", Image(Jpeg), stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        AssertRejected(await api.SendAsync(HttpMethod.Delete, $"/api/shipments/{openId}/photos/{openPhoto}", null, stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
    }

    [PostgreSqlFact]
    public async Task PhotosStayEditableOnAPublishedShipmentUntilSomeoneAsksForTheJobAsync()
    {
        var shipper = await api.RegisterAsync();
        var (id, _) = await CreatePublishedAsync(shipper);
        var path = $"/api/shipments/{id}/photos";
        var added = await api.PostAsync(path, Image(Jpeg), shipper.AccessToken);
        AssertOk(added);
        var photoId = added.Data[0].GetProperty("id").GetGuid();

        var (driver, vehicleId) = await ApprovedDriverAsync();
        var requestId = await RequestAsync(id, driver, vehicleId);
        AssertRejected(await api.PostAsync(path, Image(Png, "image/png"), shipper.AccessToken), HttpStatusCode.Conflict, "shipment.has_takers");
        AssertRejected(await api.SendAsync(HttpMethod.Delete, $"{path}/{photoId}", null, shipper.AccessToken), HttpStatusCode.Conflict, "shipment.has_takers");
        Assert.Equal(1, await api.QueryDbAsync(db => db.ShipmentPhotos.CountAsync(x => x.ShipmentId == id)));

        // The driver who asked can look at the photos; so can the driver once booked, even after the shipment leaves the market.
        Assert.Equal(HttpStatusCode.OK, (await DownloadAsync($"{path}/{photoId}", driver.AccessToken)).Status);
        AssertOk(await api.PostAsync($"/api/transport-requests/{requestId}/accept", null, shipper.AccessToken));
        Assert.Equal(HttpStatusCode.OK, (await DownloadAsync($"{path}/{photoId}", driver.AccessToken)).Status);
        var other = await api.RegisterAsync("Driver");
        Assert.Equal(HttpStatusCode.NotFound, (await DownloadAsync($"{path}/{photoId}", other.AccessToken)).Status);
    }

    // ---------------------------------------------------------------- 17-18 list and detail

    [PostgreSqlFact]
    public async Task TheOwnersListIsNewestFirstPagedAndOnlyTheirsAsync()
    {
        var shipper = await api.RegisterAsync();
        var other = await api.RegisterAsync();
        Assert.Empty((await api.GetAsync("/api/shipments/mine", shipper.AccessToken)).Data.EnumerateArray());

        var ids = new List<Guid>();
        for (var i = 1; i <= 5; i++)
        {
            ids.Add((await CreateDraftAsync(shipper, Body(("title", $"Đơn số {i}")))).Id);
            await Task.Delay(15);
        }

        await CreateDraftAsync(other, Body(("title", "Của người khác")));
        var newestFirst = Enumerable.Reverse(ids).ToArray();
        var pageOne = (await api.GetAsync("/api/shipments/mine?page=1&pageSize=2", shipper.AccessToken)).Data.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();
        var pageTwo = (await api.GetAsync("/api/shipments/mine?page=2&pageSize=2", shipper.AccessToken)).Data.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();
        var pageThree = (await api.GetAsync("/api/shipments/mine?page=3&pageSize=2", shipper.AccessToken)).Data.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(newestFirst[..2], pageOne);
        Assert.Equal(newestFirst[2..4], pageTwo);
        Assert.Equal(newestFirst[4..], pageThree);
        Assert.Empty((await api.GetAsync("/api/shipments/mine?page=4&pageSize=2", shipper.AccessToken)).Data.EnumerateArray());

        foreach (var query in new[] { "page=0", "page=100001", "pageSize=0", "pageSize=101" })
        {
            AssertRejected(await api.GetAsync($"/api/shipments/mine?{query}", shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_pagination");
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/shipments/mine")).Status);
    }

    [PostgreSqlFact]
    public async Task TheDetailShowsEveryFieldItsPhotosAndWhatTheOwnerMayDoAsync()
    {
        var shipper = await api.RegisterAsync();
        var stranger = await api.RegisterAsync();
        var (id, _) = await CreateDraftAsync(shipper, Body(("cargoType", "Refrigerated"), ("cargoDescription", "Kem que"),
            ("pickup", new { latitude = 10.85, longitude = 106.77 }), ("delivery", new { latitude = 10.91, longitude = 106.76 })));
        AssertOk(await api.PostAsync($"/api/shipments/{id}/photos", Image(Jpeg), shipper.AccessToken));

        var draft = (await api.GetAsync($"/api/shipments/{id}", shipper.AccessToken)).Data;
        var shipment = draft.GetProperty("shipment");
        Assert.Equal("Draft", shipment.GetProperty("status").GetString());
        Assert.Equal("Refrigerated", shipment.GetProperty("cargoType").GetString());
        Assert.Equal("Kem que", shipment.GetProperty("cargoDescription").GetString());
        Assert.Equal(10.85, shipment.GetProperty("pickup").GetProperty("latitude").GetDouble());
        Assert.Equal(106.76, shipment.GetProperty("delivery").GetProperty("longitude").GetDouble());
        Assert.Equal(20, shipment.GetProperty("quantity").GetInt32());
        Assert.Equal("15000000", shipment.GetProperty("proposedPrice").GetString());
        Assert.Equal(1, draft.GetProperty("photos").GetArrayLength());
        var actions = draft.GetProperty("actions");
        Assert.True(actions.GetProperty("canEdit").GetBoolean());
        Assert.True(actions.GetProperty("canPublish").GetBoolean());
        Assert.True(actions.GetProperty("canCancel").GetBoolean());

        // A draft whose pickup time has passed cannot be published until the owner picks a new time.
        await api.QueryDbAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE public.\"Shipments\" SET \"PickupAt\" = now() - interval '1 day', \"DeliverByAt\" = NULL WHERE \"Id\" = {0}", id);
            return true;
        });
        var expired = (await api.GetAsync($"/api/shipments/{id}", shipper.AccessToken)).Data.GetProperty("actions");
        Assert.True(expired.GetProperty("canEdit").GetBoolean());
        Assert.False(expired.GetProperty("canPublish").GetBoolean());

        AssertRejected(await api.GetAsync($"/api/shipments/{id}", stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        AssertRejected(await api.GetAsync($"/api/shipments/{Guid.NewGuid()}", shipper.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync($"/api/shipments/{id}")).Status);
    }
}
