using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using VantaiViet.CoreApi.Entities;
using Xunit;

namespace VantaiViet.CoreApi.Tests;

/// <summary>
/// Shipper features 1-10 through the real HTTP pipeline on a migrated PostgreSQL database:
/// register, sign in/out, password change and recovery, personal and business profile, KYC,
/// posting a shipment, pickup and delivery points, and cargo type.
/// </summary>
public sealed class ShipperOnboardingApiTests(ApiHostFixture api) : IClassFixture<ApiHostFixture>
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0xFF, 0xD9];
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0];
    private static long _identityCounter = 79_200_000_000 + (Environment.TickCount % 1_000_000) * 1_000;

    private static string NewIdentityNumber()
    {
        return "0" + Interlocked.Increment(ref _identityCounter).ToString("D11");
    }

    private static string[] Strings(JsonElement array)
    {
        return array.EnumerateArray().Select(x => x.GetString()!).ToArray();
    }

    private static void AssertOk(ApiResult result)
    {
        Assert.True(result.Status == HttpStatusCode.OK, $"Expected 200 but got {(int)result.Status}: {result.Body}");
    }

    private static void AssertRejected(ApiResult result, HttpStatusCode status, string errorCode)
    {
        Assert.True(result.Status == status && result.ErrorCode == errorCode,
            $"Expected {(int)status} {errorCode} but got {(int)result.Status} {result.ErrorCode}: {result.Body}");
    }

    private static object Identity(string number, string subject = "Individual", string dateOfBirth = "1990-05-20",
        string? issuedOn = "2021-03-01", string? expiresOn = null)
    {
        return new { subjectType = subject, fullName = "  Nguyễn Văn Thử  ", dateOfBirth, identityNumber = number, issuedOn, expiresOn };
    }

    private static object Image(byte[] bytes, string contentType = "image/jpeg")
    {
        return new { contentType, content = bytes };
    }

    private async Task<TestSession> ReviewerAsync()
    {
        return await api.WithRoleAsync(await api.RegisterAsync(), "Admin");
    }

    private async Task UploadAllIdentityDocumentsAsync(TestSession session)
    {
        AssertOk(await api.PutAsync("/api/account/kyc/documents/IdentityFront", Image(Jpeg), session.AccessToken));
        AssertOk(await api.PutAsync("/api/account/kyc/documents/IdentityBack", Image(Png, "image/png"), session.AccessToken));
        AssertOk(await api.PutAsync("/api/account/kyc/documents/Selfie", Image(Jpeg), session.AccessToken));
    }

    private async Task<Guid> ApplicationIdOfAsync(TestSession session)
    {
        var status = await api.GetAsync("/api/account/kyc", session.AccessToken);
        AssertOk(status);
        return status.Data.GetProperty("applicationId").GetGuid();
    }

    // ---------------------------------------------------------------- 1-3 account access

    [PostgreSqlFact]
    public async Task RegisterSignInSignOutAndPasswordChangeWorkAsync()
    {
        var phone = ApiHostFixture.NewPhone();
        var registered = await api.PostAsync("/api/auth/register",
            new { phoneNumber = phone, displayName = "Chủ Hàng Một", password = ApiHostFixture.DefaultPassword, accountType = "Shipper" });
        AssertOk(registered);
        Assert.Equal("PendingKyc", registered.Data.GetProperty("accountStatus").GetString());
        Assert.Equal(new[] { "Shipper", "User" }, Strings(registered.Data.GetProperty("roles")));

        AssertRejected(await api.PostAsync("/api/auth/register",
            new { phoneNumber = phone, displayName = "Trùng số", password = ApiHostFixture.DefaultPassword, accountType = "Shipper" }),
            HttpStatusCode.BadRequest, "business.rule_violated");
        AssertRejected(await api.PostAsync("/api/auth/register",
            new { phoneNumber = "0912345678", displayName = "Sai số", password = ApiHostFixture.DefaultPassword, accountType = "Shipper" }),
            HttpStatusCode.BadRequest, "request.validation_failed");
        AssertRejected(await api.PostAsync("/api/auth/register",
            new { phoneNumber = ApiHostFixture.NewPhone(), displayName = "Yếu", password = "password", accountType = "Shipper" }),
            HttpStatusCode.BadRequest, "business.rule_violated");

        AssertRejected(await api.PostAsync("/api/auth/login", new { phoneNumber = phone, password = "Wrong123!" }),
            HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        AssertRejected(await api.PostAsync("/api/auth/login", new { phoneNumber = ApiHostFixture.NewPhone(), password = "Whatever123!" }),
            HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        var session = await api.LoginAsync(phone);
        AssertOk(await api.GetAsync("/api/account/profile", session.AccessToken));
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/account/profile")).Status);

        var refreshed = await api.PostAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });
        AssertOk(refreshed);
        var renewed = refreshed.Data.GetProperty("accessToken").GetString()!;
        AssertOk(await api.GetAsync("/api/account/profile", renewed));

        AssertOk(await api.PostAsync("/api/auth/logout", null, renewed));
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/account/profile", renewed)).Status);
        AssertRejected(await api.PostAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken }),
            HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        AssertRejected(await api.PostAsync("/api/auth/refresh", new { refreshToken = refreshed.Data.GetProperty("refreshToken").GetString() }),
            HttpStatusCode.Unauthorized, "auth.invalid_credentials");

        session = await api.LoginAsync(phone);
        AssertRejected(await api.PostAsync("/api/auth/change-password", new { currentPassword = "Wrong123!", newPassword = "Changed123!" }, session.AccessToken),
            HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        AssertRejected(await api.PostAsync("/api/auth/change-password", new { currentPassword = ApiHostFixture.DefaultPassword, newPassword = "alllowercase1" }, session.AccessToken),
            HttpStatusCode.BadRequest, "auth.invalid_password");
        AssertRejected(await api.PostAsync("/api/auth/change-password", new { currentPassword = ApiHostFixture.DefaultPassword, newPassword = "short" }, session.AccessToken),
            HttpStatusCode.BadRequest, "request.validation_failed");
        AssertOk(await api.PostAsync("/api/auth/change-password", new { currentPassword = ApiHostFixture.DefaultPassword, newPassword = "Changed123!" }, session.AccessToken));
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/account/profile", session.AccessToken)).Status);
        AssertRejected(await api.PostAsync("/api/auth/login", new { phoneNumber = phone, password = ApiHostFixture.DefaultPassword }),
            HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        await api.LoginAsync(phone, "Changed123!");
    }

    [PostgreSqlFact]
    public async Task PasswordRecoveryReportsMissingEmailDeliveryInsteadOfPretendingToSendAsync()
    {
        AssertRejected(await api.PostAsync("/api/auth/forgot-password", new { email = "owner@example.test" }),
            HttpStatusCode.ServiceUnavailable, "auth.not_configured");
        AssertRejected(await api.PostAsync("/api/auth/forgot-password", new { email = "not-an-email" }),
            HttpStatusCode.BadRequest, "request.validation_failed");
        AssertRejected(await api.PostAsync("/api/auth/reset-password", new { challengeId = Guid.NewGuid(), code = "123456", newPassword = "Changed123!" }),
            HttpStatusCode.BadRequest, "auth.invalid_code");
        AssertRejected(await api.PostAsync("/api/auth/reset-password", new { challengeId = Guid.NewGuid(), code = "12", newPassword = "Changed123!" }),
            HttpStatusCode.BadRequest, "request.validation_failed");
    }

    // ---------------------------------------------------------------- 4 personal profile

    [PostgreSqlFact]
    public async Task PersonalProfileCanBeReadRenamedAndGainsAVerifiedEmailAsync()
    {
        var session = await api.RegisterAsync();
        var profile = await api.GetAsync("/api/account/profile", session.AccessToken);
        AssertOk(profile);
        Assert.Equal(session.Phone, profile.Data.GetProperty("phoneNumber").GetString());
        Assert.False(profile.Data.GetProperty("phoneNumberConfirmed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, profile.Data.GetProperty("email").ValueKind);
        Assert.False(profile.Data.GetProperty("emailConfirmed").GetBoolean());
        Assert.Equal("PendingKyc", profile.Data.GetProperty("accountStatus").GetString());
        Assert.Equal(new[] { "Shipper", "User" }, Strings(profile.Data.GetProperty("roles")));

        var renamed = await api.PutAsync("/api/account/profile", new { displayName = "  Công ty Bốn Bể  " }, session.AccessToken);
        AssertOk(renamed);
        Assert.Equal("Công ty Bốn Bể", renamed.Data.GetProperty("displayName").GetString());
        Assert.Equal("Công ty Bốn Bể", (await api.GetAsync("/api/account/profile", session.AccessToken)).Data.GetProperty("displayName").GetString());
        AssertRejected(await api.PutAsync("/api/account/profile", new { displayName = "  " }, session.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        AssertRejected(await api.PutAsync("/api/account/profile", new { displayName = " A " }, session.AccessToken), HttpStatusCode.BadRequest, "account.invalid_details");
        AssertRejected(await api.PutAsync("/api/account/profile", new { displayName = "A" }, session.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.PutAsync("/api/account/profile", new { displayName = "Không token" })).Status);
        Assert.True(await api.QueryDbAsync(db => db.AuditEvents.AnyAsync(x => x.ActorUserId == session.UserId && x.Action == "account.profile_updated")));

        // Email is certified through the existing verification flow, not typed into the profile.
        var requestKey = Guid.NewGuid();
        var requested = await api.PostAsync("/api/verification/requests", new { requestKey, channel = "Email", destination = "Owner@Example.Test" }, session.AccessToken);
        AssertOk(requested);
        var challengeId = requested.Data.GetProperty("id").GetGuid();
        Assert.False(requested.Data.GetProperty("simulated").GetBoolean());
        var code = await api.QueryDbAsync(async db =>
        {
            var row = await db.OtpDeliveries.SingleAsync(x => x.Id == challengeId);
            row.State = "Sent";
            await db.SaveChangesAsync();
            return api.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("VantaiViet.OtpDelivery.v1").Unprotect(row.ProtectedCode);
        });
        var wrong = code == "000000" ? "000001" : "000000";
        AssertRejected(await api.PostAsync($"/api/verification/requests/{challengeId}/verify", new { code = wrong }, session.AccessToken),
            HttpStatusCode.BadRequest, "verification.invalid_code");
        var verified = await api.PostAsync($"/api/verification/requests/{challengeId}/verify", new { code }, session.AccessToken);
        AssertOk(verified);
        Assert.True(verified.Data.GetProperty("verified").GetBoolean());
        profile = await api.GetAsync("/api/account/profile", session.AccessToken);
        Assert.Equal("owner@example.test", profile.Data.GetProperty("email").GetString());
        Assert.True(profile.Data.GetProperty("emailConfirmed").GetBoolean());
    }

    // ---------------------------------------------------------------- 5 business profile

    [PostgreSqlFact]
    public async Task BusinessProfileIsCreatedVersionedAndOnlyForShippersAsync()
    {
        var driver = await api.RegisterAsync("Driver");
        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync("/api/shipper/business-profile", driver.AccessToken)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/shipper/business-profile")).Status);

        var shipper = await api.RegisterAsync();
        AssertRejected(await api.GetAsync("/api/shipper/business-profile", shipper.AccessToken), HttpStatusCode.NotFound, "business_profile.not_found");

        foreach (var badTaxCode in new[] { "123", "03123456789", "abcdefghij", "0312345678-01", "" })
        {
            AssertRejected(await api.PutAsync("/api/shipper/business-profile",
                new { companyName = "Công ty ABC", taxCode = badTaxCode, address = "12 Nguyễn Huệ, Quận 1" }, shipper.AccessToken),
                HttpStatusCode.BadRequest, "request.validation_failed");
        }

        var created = await api.PutAsync("/api/shipper/business-profile",
            new { companyName = "  Công ty TNHH ABC  ", taxCode = "0312345678", address = "12 Nguyễn Huệ, Quận 1, TP.HCM" }, shipper.AccessToken);
        AssertOk(created);
        Assert.Equal("Công ty TNHH ABC", created.Data.GetProperty("companyName").GetString());
        Assert.Equal("0312345678", created.Data.GetProperty("taxCode").GetString());
        var version = created.Data.GetProperty("version").GetInt64();
        Assert.Equal(version, (await api.GetAsync("/api/shipper/business-profile", shipper.AccessToken)).Data.GetProperty("version").GetInt64());

        var branch = await api.PutAsync("/api/shipper/business-profile",
            new { companyName = "Công ty TNHH ABC", taxCode = "0312345678-001", address = "Chi nhánh Thủ Đức", version }, shipper.AccessToken);
        AssertOk(branch);
        Assert.Equal("0312345678-001", branch.Data.GetProperty("taxCode").GetString());
        Assert.Equal(version + 1, branch.Data.GetProperty("version").GetInt64());
        AssertRejected(await api.PutAsync("/api/shipper/business-profile",
            new { companyName = "Công ty ABC", taxCode = "0312345678", address = "Địa chỉ cũ", version }, shipper.AccessToken),
            HttpStatusCode.Conflict, "business_profile.conflict");
        AssertRejected(await api.PutAsync("/api/shipper/business-profile",
            new { companyName = "Công ty ABC", taxCode = "0312345678", address = "Thiếu phiên bản" }, shipper.AccessToken),
            HttpStatusCode.Conflict, "business_profile.conflict");

        var other = await api.RegisterAsync();
        AssertRejected(await api.GetAsync("/api/shipper/business-profile", other.AccessToken), HttpStatusCode.NotFound, "business_profile.not_found");

        var racer = await api.RegisterAsync();
        var payload = new { companyName = "Công ty Đua", taxCode = "0100109106", address = "1 Đường Đua, Hà Nội" };
        var first = await Task.WhenAll(api.PutAsync("/api/shipper/business-profile", payload, racer.AccessToken),
            api.PutAsync("/api/shipper/business-profile", payload, racer.AccessToken));
        Assert.Single(first, x => x.Status == HttpStatusCode.OK);
        Assert.Single(first, x => x.Status == HttpStatusCode.Conflict);
    }

    // ---------------------------------------------------------------- 6 account verification

    [PostgreSqlFact]
    public async Task IndividualKycRunsFromDraftToApprovalAndARejectionCanBeResubmittedAsync()
    {
        var shipper = await api.RegisterAsync();
        var none = await api.GetAsync("/api/account/kyc", shipper.AccessToken);
        AssertOk(none);
        Assert.Equal("None", none.Data.GetProperty("status").GetString());
        Assert.Empty(none.Data.GetProperty("documents").EnumerateArray());
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/account/kyc")).Status);

        var number = NewIdentityNumber();
        AssertRejected(await api.PutAsync("/api/account/kyc/documents/IdentityFront", Image(Jpeg), shipper.AccessToken), HttpStatusCode.BadRequest, "kyc.identity_required");
        AssertRejected(await api.PostAsync("/api/account/kyc/submit", null, shipper.AccessToken), HttpStatusCode.BadRequest, "kyc.identity_required");
        AssertRejected(await api.PutAsync("/api/account/kyc/identity", Identity(number, dateOfBirth: "2015-01-01"), shipper.AccessToken), HttpStatusCode.BadRequest, "kyc.invalid_details");
        AssertRejected(await api.PutAsync("/api/account/kyc/identity", Identity(number, issuedOn: "2999-01-01"), shipper.AccessToken), HttpStatusCode.BadRequest, "kyc.invalid_details");
        AssertRejected(await api.PutAsync("/api/account/kyc/identity", Identity(number, issuedOn: "1980-01-01"), shipper.AccessToken), HttpStatusCode.BadRequest, "kyc.invalid_details");
        AssertRejected(await api.PutAsync("/api/account/kyc/identity", Identity(number, expiresOn: "2000-01-01"), shipper.AccessToken), HttpStatusCode.BadRequest, "kyc.invalid_details");
        AssertRejected(await api.PutAsync("/api/account/kyc/identity", Identity(number, issuedOn: "1995-01-01", expiresOn: "2000-01-01"), shipper.AccessToken), HttpStatusCode.BadRequest, "kyc.document_expired");
        AssertRejected(await api.PutAsync("/api/account/kyc/identity", Identity("12345"), shipper.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        AssertRejected(await api.PutAsync("/api/account/kyc/identity", Identity(number, subject: "Company"), shipper.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");

        var draft = await api.PutAsync("/api/account/kyc/identity", Identity(number), shipper.AccessToken);
        AssertOk(draft);
        Assert.Equal("Draft", draft.Data.GetProperty("status").GetString());
        Assert.Equal("Individual", draft.Data.GetProperty("subjectType").GetString());
        Assert.Equal("Nguyễn Văn Thử", draft.Data.GetProperty("fullName").GetString());
        Assert.Equal("1990-05-20", draft.Data.GetProperty("dateOfBirth").GetString());
        Assert.Equal(new string('*', 8) + number[^4..], draft.Data.GetProperty("identityNumberMasked").GetString());
        Assert.DoesNotContain(number, draft.Body.ToString());
        var applicationId = draft.Data.GetProperty("applicationId").GetGuid();
        await api.QueryDbAsync(async db =>
        {
            var detail = await db.KycIdentityDetails.AsNoTracking().SingleAsync(x => x.ApplicationId == applicationId);
            Assert.Equal(32, detail.IdentityNumberHmac.Length);
            Assert.DoesNotContain(number, Encoding.UTF8.GetString(detail.IdentityNumberEncrypted));
            return true;
        });

        AssertRejected(await api.PutAsync("/api/account/kyc/documents/IdentityFront", new { contentType = "image/jpeg", content = Encoding.UTF8.GetBytes("not an image at all") }, shipper.AccessToken),
            HttpStatusCode.BadRequest, "kyc.invalid_image");
        AssertRejected(await api.PutAsync("/api/account/kyc/documents/IdentityFront", Image(Png, "image/jpeg"), shipper.AccessToken), HttpStatusCode.BadRequest, "kyc.invalid_image");
        AssertRejected(await api.PutAsync("/api/account/kyc/documents/IdentityFront", Image(new byte[(5 * 1024 * 1024) + 1]), shipper.AccessToken), HttpStatusCode.BadRequest, "kyc.invalid_image");
        AssertRejected(await api.PutAsync("/api/account/kyc/documents/Passport", Image(Jpeg), shipper.AccessToken), HttpStatusCode.BadRequest, "kyc.invalid_document_type");
        AssertRejected(await api.PutAsync("/api/account/kyc/documents/BusinessLicense", Image(Jpeg), shipper.AccessToken), HttpStatusCode.BadRequest, "kyc.invalid_document_type");
        AssertRejected(await api.PostAsync("/api/account/kyc/submit", null, shipper.AccessToken), HttpStatusCode.BadRequest, "kyc.incomplete");

        await UploadAllIdentityDocumentsAsync(shipper);
        var replaced = await api.PutAsync("/api/account/kyc/documents/IdentityFront", Image(Png, "image/png"), shipper.AccessToken);
        AssertOk(replaced);
        var documents = replaced.Data.GetProperty("documents").EnumerateArray().ToList();
        Assert.Equal(new[] { "IdentityBack", "IdentityFront", "Selfie" }, documents.Select(x => x.GetProperty("type").GetString()!).ToArray());
        Assert.Equal("image/png", documents.Single(x => x.GetProperty("type").GetString() == "IdentityFront").GetProperty("contentType").GetString());
        Assert.All(documents, document => Assert.False(document.TryGetProperty("content", out _), "Image bytes must never be returned to the client."));

        var submitted = await api.PostAsync("/api/account/kyc/submit", null, shipper.AccessToken);
        AssertOk(submitted);
        Assert.Equal("Submitted", submitted.Data.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, submitted.Data.GetProperty("submittedAt").ValueKind);
        Assert.Equal("PendingKyc", (await api.GetAsync("/api/account/profile", shipper.AccessToken)).Data.GetProperty("accountStatus").GetString());
        AssertRejected(await api.PutAsync("/api/account/kyc/identity", Identity(number), shipper.AccessToken), HttpStatusCode.Conflict, "kyc.conflict");
        AssertRejected(await api.PutAsync("/api/account/kyc/documents/Selfie", Image(Jpeg), shipper.AccessToken), HttpStatusCode.Conflict, "kyc.conflict");
        AssertRejected(await api.PostAsync("/api/account/kyc/submit", null, shipper.AccessToken), HttpStatusCode.Conflict, "kyc.conflict");

        Assert.Equal(HttpStatusCode.Forbidden, (await api.PostAsync($"/api/admin/kyc/{applicationId}/approve", new { }, shipper.AccessToken)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.PostAsync($"/api/admin/kyc/{applicationId}/reject", new { rejectionCode = "x" }, shipper.AccessToken)).Status);
        var reviewer = await ReviewerAsync();
        var queue = await api.GetAsync("/api/admin/kyc?status=Submitted&pageSize=100", reviewer.AccessToken);
        AssertOk(queue);
        Assert.Contains(queue.Data.EnumerateArray(), x => x.GetProperty("id").GetGuid() == applicationId && x.GetProperty("documentCount").GetInt32() == 3);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsync($"/api/admin/kyc/{applicationId}/reject", new { rejectionCode = "" }, reviewer.AccessToken)).Status);

        var rejected = await api.PostAsync($"/api/admin/kyc/{applicationId}/reject", new { rejectionCode = "blurry_image", reviewNote = "Ảnh mặt trước bị mờ." }, reviewer.AccessToken);
        AssertOk(rejected);
        Assert.Equal("Rejected", rejected.Data.GetProperty("kycStatus").GetString());
        Assert.Equal("PendingKyc", rejected.Data.GetProperty("accountStatus").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsync($"/api/admin/kyc/{applicationId}/reject", new { rejectionCode = "again" }, reviewer.AccessToken)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsync($"/api/admin/kyc/{applicationId}/approve", new { }, reviewer.AccessToken)).Status);

        var afterRejection = await api.GetAsync("/api/account/kyc", shipper.AccessToken);
        Assert.Equal("Rejected", afterRejection.Data.GetProperty("status").GetString());
        Assert.Equal("blurry_image", afterRejection.Data.GetProperty("rejectionCode").GetString());
        Assert.Equal("Ảnh mặt trước bị mờ.", afterRejection.Data.GetProperty("reviewNote").GetString());

        var second = await api.PutAsync("/api/account/kyc/identity", Identity(number), shipper.AccessToken);
        AssertOk(second);
        Assert.Equal("Draft", second.Data.GetProperty("status").GetString());
        Assert.NotEqual(applicationId, second.Data.GetProperty("applicationId").GetGuid());
        Assert.Empty(second.Data.GetProperty("documents").EnumerateArray());
        AssertRejected(await api.PostAsync("/api/account/kyc/submit", null, shipper.AccessToken), HttpStatusCode.BadRequest, "kyc.incomplete");
        await UploadAllIdentityDocumentsAsync(shipper);
        AssertOk(await api.PostAsync("/api/account/kyc/submit", null, shipper.AccessToken));

        var secondId = await ApplicationIdOfAsync(shipper);
        var approved = await api.PostAsync($"/api/admin/kyc/{secondId}/approve", new { reviewNote = "Hồ sơ hợp lệ." }, reviewer.AccessToken);
        AssertOk(approved);
        Assert.Equal("Active", approved.Data.GetProperty("accountStatus").GetString());
        Assert.Equal("Active", (await api.GetAsync("/api/account/profile", shipper.AccessToken)).Data.GetProperty("accountStatus").GetString());
        var final = await api.GetAsync("/api/account/kyc", shipper.AccessToken);
        Assert.Equal("Approved", final.Data.GetProperty("status").GetString());
        AssertRejected(await api.PutAsync("/api/account/kyc/identity", Identity(number), shipper.AccessToken), HttpStatusCode.Conflict, "kyc.conflict");
        Assert.True(await api.QueryDbAsync(db => db.AuditEvents.AnyAsync(x => x.TargetId == applicationId && x.Action == "kyc.rejected")));
        Assert.Equal(2, await api.QueryDbAsync(db => db.AuditEvents.CountAsync(x => x.ActorUserId == shipper.UserId && x.Action == "kyc.submitted")));
    }

    [PostgreSqlFact]
    public async Task BusinessKycNeedsALicenceAndProfileAndFreezesTheCompanyIdentityAsync()
    {
        var shipper = await api.RegisterAsync();
        var number = NewIdentityNumber();
        AssertOk(await api.PutAsync("/api/account/kyc/identity", Identity(number, "Business"), shipper.AccessToken));
        var withLicence = await api.PutAsync("/api/account/kyc/documents/BusinessLicense", Image(Jpeg), shipper.AccessToken);
        AssertOk(withLicence);
        Assert.Single(withLicence.Data.GetProperty("documents").EnumerateArray());

        var switchedToPerson = await api.PutAsync("/api/account/kyc/identity", Identity(number), shipper.AccessToken);
        AssertOk(switchedToPerson);
        Assert.Equal("Individual", switchedToPerson.Data.GetProperty("subjectType").GetString());
        Assert.Empty(switchedToPerson.Data.GetProperty("documents").EnumerateArray());

        AssertOk(await api.PutAsync("/api/account/kyc/identity", Identity(number, "Business"), shipper.AccessToken));
        await UploadAllIdentityDocumentsAsync(shipper);
        AssertRejected(await api.PostAsync("/api/account/kyc/submit", null, shipper.AccessToken), HttpStatusCode.BadRequest, "kyc.incomplete");
        AssertOk(await api.PutAsync("/api/account/kyc/documents/BusinessLicense", Image(Png, "image/png"), shipper.AccessToken));
        AssertRejected(await api.PostAsync("/api/account/kyc/submit", null, shipper.AccessToken), HttpStatusCode.BadRequest, "kyc.business_profile_required");

        var profile = await api.PutAsync("/api/shipper/business-profile",
            new { companyName = "Công ty TNHH Vận Chuyển Xanh", taxCode = "0301234567", address = "5 Lê Lợi, Quận 1, TP.HCM" }, shipper.AccessToken);
        AssertOk(profile);
        var version = profile.Data.GetProperty("version").GetInt64();
        AssertOk(await api.PostAsync("/api/account/kyc/submit", null, shipper.AccessToken));

        AssertRejected(await api.PutAsync("/api/shipper/business-profile",
            new { companyName = "Công ty TNHH Vận Chuyển Xanh", taxCode = "0309999999", address = "5 Lê Lợi, Quận 1, TP.HCM", version }, shipper.AccessToken),
            HttpStatusCode.Conflict, "business_profile.locked");
        AssertRejected(await api.PutAsync("/api/shipper/business-profile",
            new { companyName = "Tên khác", taxCode = "0301234567", address = "5 Lê Lợi, Quận 1, TP.HCM", version }, shipper.AccessToken),
            HttpStatusCode.Conflict, "business_profile.locked");
        var moved = await api.PutAsync("/api/shipper/business-profile",
            new { companyName = "Công ty TNHH Vận Chuyển Xanh", taxCode = "0301234567", address = "99 Pasteur, Quận 3, TP.HCM", version }, shipper.AccessToken);
        AssertOk(moved);

        var reviewer = await ReviewerAsync();
        AssertOk(await api.PostAsync($"/api/admin/kyc/{await ApplicationIdOfAsync(shipper)}/approve", new { }, reviewer.AccessToken));
        AssertRejected(await api.PutAsync("/api/shipper/business-profile",
            new { companyName = "Công ty TNHH Vận Chuyển Xanh", taxCode = "0309999999", address = "99 Pasteur, Quận 3, TP.HCM", version = moved.Data.GetProperty("version").GetInt64() }, shipper.AccessToken),
            HttpStatusCode.Conflict, "business_profile.locked");
    }

    [PostgreSqlFact]
    public async Task ConcurrentSubmissionsAndSharedIdentityNumbersAreSerializedAsync()
    {
        var number = NewIdentityNumber();
        var first = await api.RegisterAsync();
        AssertOk(await api.PutAsync("/api/account/kyc/identity", Identity(number), first.AccessToken));
        await UploadAllIdentityDocumentsAsync(first);
        var doubleTap = await Task.WhenAll(api.PostAsync("/api/account/kyc/submit", null, first.AccessToken), api.PostAsync("/api/account/kyc/submit", null, first.AccessToken));
        Assert.Single(doubleTap, x => x.Status == HttpStatusCode.OK);
        Assert.Single(doubleTap, x => x.Status == HttpStatusCode.Conflict && x.ErrorCode == "kyc.conflict");

        var copycat = await api.RegisterAsync();
        AssertOk(await api.PutAsync("/api/account/kyc/identity", Identity(number), copycat.AccessToken));
        await UploadAllIdentityDocumentsAsync(copycat);
        AssertRejected(await api.PostAsync("/api/account/kyc/submit", null, copycat.AccessToken), HttpStatusCode.Conflict, "kyc.identity_unavailable");

        var sharedNumber = NewIdentityNumber();
        var left = await api.RegisterAsync();
        var right = await api.RegisterAsync();
        foreach (var session in new[] { left, right })
        {
            AssertOk(await api.PutAsync("/api/account/kyc/identity", Identity(sharedNumber), session.AccessToken));
            await UploadAllIdentityDocumentsAsync(session);
        }

        var race = await Task.WhenAll(api.PostAsync("/api/account/kyc/submit", null, left.AccessToken), api.PostAsync("/api/account/kyc/submit", null, right.AccessToken));
        Assert.Single(race, x => x.Status == HttpStatusCode.OK);
        Assert.Single(race, x => x.Status == HttpStatusCode.Conflict && x.ErrorCode == "kyc.identity_unavailable");
    }

    // ---------------------------------------------------------------- 7-10 shipment, addresses, cargo

    private static object Shipment(string cargoType = "Food", string? description = "  Rau củ tươi  ", string pickup = "  Kho A, Thủ Đức, TP.HCM  ",
        string delivery = "Kho B, Dĩ An, Bình Dương", object? pickupPoint = null, object? deliveryPoint = null, DateTimeOffset? pickupAt = null)
    {
        return new
        {
            title = "Lô hàng thử nghiệm",
            pickupAddress = pickup,
            deliveryAddress = delivery,
            weightKg = 1200.5,
            pickupAt = pickupAt ?? DateTimeOffset.UtcNow.AddDays(2),
            cargoType,
            cargoDescription = description,
            pickup = pickupPoint ?? new { latitude = 10.85, longitude = 106.77 },
            delivery = deliveryPoint ?? new { latitude = 10.91, longitude = 106.76 }
        };
    }

    [PostgreSqlFact]
    public async Task ShipmentCarriesACargoTypeAndKeepsItWhenAnOlderClientEditsAsync()
    {
        var shipper = await api.RegisterAsync();
        var created = await api.PostAsync("/api/shipments", Shipment(), shipper.AccessToken);
        AssertOk(created);
        var shipment = created.Data;
        Assert.Equal("Food", shipment.GetProperty("cargoType").GetString());
        Assert.Equal("Rau củ tươi", shipment.GetProperty("cargoDescription").GetString());
        Assert.Equal("Kho A, Thủ Đức, TP.HCM", shipment.GetProperty("pickupAddress").GetString());
        Assert.Equal("Draft", shipment.GetProperty("status").GetString());
        Assert.Equal(10.85, shipment.GetProperty("pickup").GetProperty("latitude").GetDouble());
        var id = shipment.GetProperty("id").GetGuid();

        var defaulted = await api.PostAsync("/api/shipments", new
        {
            title = "Không nói loại hàng", pickupAddress = "A", deliveryAddress = "B", weightKg = 10, pickupAt = DateTimeOffset.UtcNow.AddDays(1)
        }, shipper.AccessToken);
        AssertOk(defaulted);
        Assert.Equal("General", defaulted.Data.GetProperty("cargoType").GetString());
        Assert.Equal(JsonValueKind.Null, defaulted.Data.GetProperty("cargoDescription").ValueKind);

        AssertRejected(await api.PostAsync("/api/shipments", Shipment("Weapons"), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_cargo_type");
        AssertRejected(await api.PostAsync("/api/shipments", Shipment("food"), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_cargo_type");
        AssertRejected(await api.PostAsync("/api/shipments", Shipment("Other", null), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.cargo_description_required");
        AssertRejected(await api.PostAsync("/api/shipments", Shipment("Other", "  ab "), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.cargo_description_required");
        AssertRejected(await api.PostAsync("/api/shipments", Shipment(description: new string('x', 1001)), shipper.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        var other = await api.PostAsync("/api/shipments", Shipment("Other", "Đồ cổ, dễ vỡ"), shipper.AccessToken);
        AssertOk(other);
        foreach (var cargoType in CargoTypes.All)
        {
            AssertOk(await api.PostAsync("/api/shipments", Shipment(cargoType, "Mô tả hàng hóa"), shipper.AccessToken));
        }

        var version = shipment.GetProperty("version").GetInt64();
        var edited = await api.PutAsync($"/api/shipments/{id}", new
        {
            title = "Đã sửa", pickupAddress = "Kho A", deliveryAddress = "Kho B", weightKg = 1500, pickupAt = DateTimeOffset.UtcNow.AddDays(3), version,
            cargoType = "Machinery", cargoDescription = "Máy ép nhựa"
        }, shipper.AccessToken);
        AssertOk(edited);
        Assert.Equal("Machinery", edited.Data.GetProperty("cargoType").GetString());
        Assert.Equal("Máy ép nhựa", edited.Data.GetProperty("cargoDescription").GetString());
        version = edited.Data.GetProperty("version").GetInt64();

        var olderClient = await api.PutAsync($"/api/shipments/{id}", new
        {
            title = "Chỉ sửa tiêu đề", pickupAddress = "Kho A", deliveryAddress = "Kho B", weightKg = 1500, pickupAt = DateTimeOffset.UtcNow.AddDays(3), version
        }, shipper.AccessToken);
        AssertOk(olderClient);
        Assert.Equal("Machinery", olderClient.Data.GetProperty("cargoType").GetString());
        Assert.Equal("Máy ép nhựa", olderClient.Data.GetProperty("cargoDescription").GetString());
        Assert.Equal(10.85, olderClient.Data.GetProperty("pickup").GetProperty("latitude").GetDouble());
        AssertRejected(await api.PutAsync($"/api/shipments/{id}", new
        {
            title = "Loại sai", pickupAddress = "Kho A", deliveryAddress = "Kho B", weightKg = 1500, pickupAt = DateTimeOffset.UtcNow.AddDays(3),
            version = olderClient.Data.GetProperty("version").GetInt64(), cargoType = "Nope"
        }, shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_cargo_type");
        AssertRejected(await api.PutAsync($"/api/shipments/{id}", new
        {
            title = "Phiên bản cũ", pickupAddress = "Kho A", deliveryAddress = "Kho B", weightKg = 1500, pickupAt = DateTimeOffset.UtcNow.AddDays(3), version
        }, shipper.AccessToken), HttpStatusCode.Conflict, "shipment.conflict");

        version = olderClient.Data.GetProperty("version").GetInt64();
        var published = await api.PostAsync($"/api/shipments/{id}/publish", new { version }, shipper.AccessToken);
        AssertOk(published);
        Assert.Equal("Published", published.Data.GetProperty("status").GetString());
        Assert.Equal("Machinery", published.Data.GetProperty("cargoType").GetString());

        var mine = await api.GetAsync("/api/shipments/mine?pageSize=100", shipper.AccessToken);
        Assert.Contains(mine.Data.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id && x.GetProperty("cargoType").GetString() == "Machinery");
        var stranger = await api.RegisterAsync();
        var open = await api.GetAsync("/api/shipments?pageSize=100", stranger.AccessToken);
        Assert.Contains(open.Data.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id && x.GetProperty("cargoType").GetString() == "Machinery");
        AssertRejected(await api.GetAsync($"/api/shipments/{id}", stranger.AccessToken), HttpStatusCode.NotFound, "shipment.not_found");
        var owned = await api.GetAsync($"/api/shipments/{id}", shipper.AccessToken);
        Assert.Equal("Machinery", owned.Data.GetProperty("shipment").GetProperty("cargoType").GetString());

        var driver = await api.RegisterAsync("Driver");
        AssertRejected(await api.PostAsync("/api/shipments", Shipment(), driver.AccessToken), HttpStatusCode.Forbidden, "shipment.forbidden");
        AssertRejected(await api.PostAsync("/api/shipments", Shipment(pickupAt: DateTimeOffset.UtcNow.AddMinutes(-5)), shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_details");
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.PostAsync("/api/shipments", Shipment())).Status);

        // The database refuses a cargo type the service would never write.
        var violation = await Assert.ThrowsAsync<PostgresException>(() => api.QueryDbAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE public.\"Shipments\" SET \"CargoType\" = 'Bogus' WHERE \"Id\" = {0}", id);
            return true;
        }));
        Assert.Equal(PostgresErrorCodes.CheckViolation, violation.SqlState);
    }

    [PostgreSqlFact]
    public async Task PickupAndDeliveryPointsAreRequiredTrimmedAndPairedAsync()
    {
        var shipper = await api.RegisterAsync();
        AssertRejected(await api.PostAsync("/api/shipments", Shipment(pickup: ""), shipper.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        AssertRejected(await api.PostAsync("/api/shipments", Shipment(delivery: ""), shipper.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        AssertRejected(await api.PostAsync("/api/shipments", Shipment(pickup: new string('a', 501)), shipper.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        AssertRejected(await api.PostAsync("/api/shipments", Shipment(pickupPoint: new { latitude = 91, longitude = 10 }), shipper.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");
        AssertRejected(await api.PostAsync("/api/shipments", new
        {
            title = "Thiếu điểm giao", pickupAddress = "A", deliveryAddress = "B", weightKg = 5, pickupAt = DateTimeOffset.UtcNow.AddDays(1),
            pickup = new { latitude = 10.0, longitude = 106.0 }
        }, shipper.AccessToken), HttpStatusCode.BadRequest, "shipment.invalid_coordinates");

        var withoutPoints = await api.PostAsync("/api/shipments", new
        {
            title = "Chỉ có địa chỉ", pickupAddress = "  Số 1 Đường Láng, Đống Đa, Hà Nội ", deliveryAddress = "Số 9 Nguyễn Trãi, Thanh Xuân, Hà Nội", weightKg = 5,
            pickupAt = DateTimeOffset.UtcNow.AddDays(1)
        }, shipper.AccessToken);
        AssertOk(withoutPoints);
        Assert.Equal("Số 1 Đường Láng, Đống Đa, Hà Nội", withoutPoints.Data.GetProperty("pickupAddress").GetString());
        Assert.Equal(JsonValueKind.Null, withoutPoints.Data.GetProperty("pickup").ValueKind);
        Assert.Equal(JsonValueKind.Null, withoutPoints.Data.GetProperty("delivery").ValueKind);
    }

    // ---------------------------------------------------------------- 8-9 address search

    private static HttpResponseMessage Json(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    [PostgreSqlFact]
    public async Task AddressSearchAsksTheGeocoderOnceAndDropsUnusablePlacesAsync()
    {
        var shipper = await api.RegisterAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/geocoding/search?q=hanoi")).Status);
        AssertRejected(await api.GetAsync("/api/geocoding/search?q=ab", shipper.AccessToken), HttpStatusCode.BadRequest, "geocoding.invalid_query");
        AssertRejected(await api.GetAsync("/api/geocoding/search", shipper.AccessToken), HttpStatusCode.BadRequest, "request.validation_failed");

        api.Geocoder.Requests.Clear();
        api.Geocoder.Respond = _ => Json("""
            [{"display_name":"Ngã tư Hàng Xanh, Bình Thạnh, TP.HCM","lat":"10.8015","lon":"106.7110"},
             {"display_name":"Toạ độ hỏng","lat":"abc","lon":"1"},
             {"display_name":"","lat":"1","lon":"1"},
             {"display_name":"Ngoài phạm vi","lat":"95","lon":"10"}]
            """);
        var found = await api.GetAsync("/api/geocoding/search?q=ng%C3%A3%20t%C6%B0%20h%C3%A0ng%20xanh", shipper.AccessToken);
        AssertOk(found);
        var place = Assert.Single(found.Data.EnumerateArray());
        Assert.Equal("Ngã tư Hàng Xanh, Bình Thạnh, TP.HCM", place.GetProperty("displayName").GetString());
        Assert.Equal(10.8015, place.GetProperty("latitude").GetDouble());
        Assert.Equal(106.7110, place.GetProperty("longitude").GetDouble());
        var upstream = Assert.Single(api.Geocoder.Requests);
        Assert.Equal("geocoder.test", upstream.Host);
        Assert.Contains("countrycodes=vn", upstream.Query);
        Assert.Contains("limit=5", upstream.Query);
        Assert.Contains("q=ng%C3%A3%20t%C6%B0%20h%C3%A0ng%20xanh", upstream.Query);

        AssertOk(await api.GetAsync("/api/geocoding/search?q=%20%20NG%C3%83%20T%C6%AF%20%20H%C3%80NG%20XANH%20", shipper.AccessToken));
        Assert.Single(api.Geocoder.Requests);

        api.Geocoder.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        AssertRejected(await api.GetAsync("/api/geocoding/search?q=khong-co-cache-1", shipper.AccessToken), HttpStatusCode.ServiceUnavailable, "geocoding.unavailable");
        api.Geocoder.Respond = _ => throw new HttpRequestException("network down");
        AssertRejected(await api.GetAsync("/api/geocoding/search?q=khong-co-cache-2", shipper.AccessToken), HttpStatusCode.ServiceUnavailable, "geocoding.unavailable");
        api.Geocoder.Respond = _ => Json("not json");
        AssertRejected(await api.GetAsync("/api/geocoding/search?q=khong-co-cache-3", shipper.AccessToken), HttpStatusCode.ServiceUnavailable, "geocoding.unavailable");

        // A failure is not cached: the same text works as soon as the upstream recovers.
        api.Geocoder.Respond = _ => Json("""[{"display_name":"Hà Nội","lat":"21.0285","lon":"105.8542"}]""");
        Assert.Single((await api.GetAsync("/api/geocoding/search?q=khong-co-cache-1", shipper.AccessToken)).Data.EnumerateArray());
    }

    [PostgreSqlFact]
    public async Task AddressSearchIsRateLimitedPerAccountAsync()
    {
        api.Geocoder.Respond = _ => Json("""[{"display_name":"Đà Nẵng","lat":"16.0544","lon":"108.2022"}]""");
        var busy = await api.RegisterAsync();
        for (var i = 0; i < 20; i++)
        {
            AssertOk(await api.GetAsync("/api/geocoding/search?q=da%20nang", busy.AccessToken));
        }

        AssertRejected(await api.GetAsync("/api/geocoding/search?q=da%20nang", busy.AccessToken), HttpStatusCode.TooManyRequests, "geocoding.rate_limited");
        var calm = await api.RegisterAsync();
        AssertOk(await api.GetAsync("/api/geocoding/search?q=da%20nang", calm.AccessToken));
    }
}
