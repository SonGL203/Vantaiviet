# Báo cáo hoàn thiện — Chủ hàng, đợt 1 (tính năng 1–10)

Ngày: 21/09/2026 · Backend: `Vantaiviet` (.NET 10, PostgreSQL) · Frontend: `Vantaiviet-fontend/shipper-mobile` (Flutter)

## 1. Kết luận

10 tính năng đầu của chủ hàng đã hoàn thành ở cả backend và frontend, và đã được kiểm chứng bằng năm lớp test:

| Lớp test | Kết quả |
| --- | --- |
| Backend, gọi HTTP thật trên PostgreSQL thật (DB tạm, chạy đủ 12 migration) | 21/21 đạt (9 test cũ + 12 test mới) |
| Frontend, đơn vị + repository (HTTP giả) | 73/73 đạt |
| Frontend, giao diện (repository giả tuân theo luật server) | 52/52 đạt |
| Frontend `ApiClient`/repository ↔ API thật | 12/12 đạt |
| Frontend giao diện thật ↔ API thật (một chủ hàng đi trọn kịch bản) | 1/1 đạt |
| `flutter analyze`, kiểm tra kiến trúc `Test-Architecture.ps1`, build backend 0 cảnh báo | đạt |

Chưa kiểm chứng: chạy app trên thiết bị hoặc giả lập Android/iOS/Windows thật (xem mục 6).

## 2. Từng tính năng

| # | Tính năng | Backend | Frontend | Ghi chú |
| --- | --- | --- | --- | --- |
| 1 | Đăng ký | `POST /api/auth/register` (đã có) | `RegisterScreen` | Luôn tạo tài khoản `Shipper`; kiểm tra tên, số điện thoại, mật khẩu (đúng luật server) trước khi gửi |
| 2 | Đăng nhập / đăng xuất | `login`, `refresh`, `logout` (đã có) | `LoginScreen`, tab Tài khoản | Giữ phiên bằng refresh token; tự làm mới access token hết hạn; hỏi xác nhận trước khi đăng xuất |
| 3 | Quên / đổi mật khẩu | `forgot-password`, `reset-password`, `change-password` (đã có) | `ForgotPasswordScreen`, `ResetPasswordScreen`, `ChangePasswordScreen` | Đổi mật khẩu xong server kết thúc mọi phiên, app quay về đăng nhập. Sai mật khẩu hiện tại chỉ đếm 1 lần vào bộ đếm khóa tài khoản (không tự retry) |
| 4 | Hồ sơ cá nhân | **Mới** `GET/PUT /api/account/profile` | `ProfileScreen`, `EmailVerificationSheet` | Sửa được tên. Số điện thoại là tên đăng nhập nên chỉ đọc. Email chỉ được ghi nhận sau khi nhập đúng mã OTP |
| 5 | Hồ sơ doanh nghiệp | **Mới** bảng `ShipperBusinessProfiles`, `GET/PUT /api/shipper/business-profile` | `BusinessProfileScreen` | MST 10 số hoặc 13 số chi nhánh; có `version` chống ghi đè; khóa tên công ty và MST khi hồ sơ xác minh doanh nghiệp đang chờ hoặc đã duyệt |
| 6 | Xác minh tài khoản | **Mới** `GET /api/account/kyc`, `PUT .../identity`, `PUT .../documents/{loại}`, `POST .../submit`; `POST /api/admin/kyc/{id}/reject` | `KycScreen` | Cá nhân: CCCD + 3 ảnh. Doanh nghiệp: thêm giấy phép kinh doanh và cần hồ sơ doanh nghiệp. Số CCCD được mã hóa, có HMAC chống một CCCD dùng cho 2 tài khoản. Bị từ chối thì tạo hồ sơ mới |
| 7 | Đăng hàng | `POST /api/shipments` + `publish` (đã có, mở rộng) | `CreateShipmentScreen` | "Đăng hàng" = tạo + đăng; nếu đăng lỗi, lần bấm sau đăng lại đúng bản nháp đó (không sinh đơn thừa) |
| 8 | Điểm lấy hàng | Địa chỉ + tọa độ (đã có); **mới** `GET /api/geocoding/search` | Ô địa chỉ + `AddressSearchSheet` | Tìm và ghim vị trí. Sửa tay địa chỉ thì ghim bị gỡ |
| 9 | Điểm giao hàng | như trên | như trên | Ghim cả hai điểm hoặc không ghim (server từ chối một điểm lẻ) |
| 10 | Loại hàng | **Mới** cột `CargoType` (11 loại, có CHECK ở DB) và `CargoDescription` | Chọn loại hàng bằng chip + ô mô tả | `Other` bắt buộc có mô tả; client cũ sửa đơn không gửi loại hàng thì giữ nguyên giá trị cũ |

Trọng lượng và giờ lấy hàng có mặt trên form vì API bắt buộc phải có; phần đầy đủ (số lượng, chọn giờ nâng cao…) thuộc tính năng 11 và 13.

## 3. Thay đổi backend

- Migration `20260921062041_AddShipperOnboarding`: chỉ **thêm** (bảng, cột, ràng buộc), có `Down`. `Down` xóa ảnh `BusinessLicense` trước khi khôi phục ràng buộc cũ. Đã kiểm chứng lên → xuống → lên trên DB tạm.
- Service mới: `AccountProfileService`, `BusinessProfileService`, `AccountKycService`, `GeocodingService` (typed `HttpClient`, bộ nhớ đệm giới hạn 500 mục, giới hạn 20 lượt tìm/phút/tài khoản). Controller mỏng dùng chung `ResultResponses.FromResult`.
- `KycReviewService.RejectAsync` + `POST /api/admin/kyc/{id}/reject` để vòng đời Nộp → Từ chối → Nộp lại chạy được.
- Cấu hình mới: `Kyc:IdentityHmacKey` (khóa dev đã thêm vào `appsettings.Development.json`; **bắt buộc cấu hình khi triển khai**, thiếu thì `503 kyc.not_configured`), `Maps:GeocodingBaseUrl`, `RateLimiting:AuthPermitLimit` (mặc định vẫn 30/phút).
- Ảnh KYC lưu trong DB (`bytea`, tối đa 5 MB) để nộp hồ sơ là một giao dịch nguyên tử; API không bao giờ trả lại ảnh hay số CCCD đầy đủ (chỉ dạng che `********1234`).

## 4. Lỗi phát hiện khi test

1. **Đã sửa** — `VerificationController` trả **500** khi nhập sai mã OTP (đúng ra 400). Nguyên nhân: `Problem(extensions: {errorCode})` trùng khóa với `errorCode` do `CustomizeProblemDetails` thêm sẵn. Chặn trực tiếp luồng xác minh email của chủ hàng nên tôi sửa bằng helper dùng chung.
2. **Chưa sửa (ngoài phạm vi)** — `TripsController` cùng lỗi: mọi nhánh lỗi của chuyến trả 500. Đã tạo tác vụ riêng để sửa và thêm test.
3. Test cũ `ShipmentDatabaseTests` dùng trực tiếp DB `vantaiviet_core_test`, nên DB này cần được áp migration mới (đã áp).
4. Trong lúc viết test live, phát hiện `parseWeight("1.200")` hiểu là 1,2 kg thay vì 1.200 kg (sai 1000 lần với người dùng Việt). Đã sửa và có test.

## 5. Sự cố cần bạn biết

Khi chạy test "thật", tôi khởi động một bản API ở môi trường Development. Môi trường này tự nạp **user-secrets của bạn**, trong đó có SMTP `smtp.gmail.com` với `Email:Simulate=False`. Bước xác minh email của test đã tạo đúng **1 email thật** gửi qua tài khoản Gmail đó tới địa chỉ giả `live.owner@example.test` (tên miền dành riêng cho thử nghiệm nên không tới ai; bạn có thể thấy một thư báo lỗi phát hành trong hộp thư). Sau khi phát hiện, tôi tắt bản API đó và chạy lại với `Simulate=true` và SMTP `smtp.invalid`, nên các lần chạy sau không thể gửi mail thật. Các test xUnit không thể gửi mail: test mới tắt worker (`WorkerEnabled=false`), test cũ dùng cấu hình rỗng không có SMTP.

## 6. Giới hạn và việc còn lại

- Đã thêm nền tảng Windows cho `shipper-mobile` (`flutter create --platforms=windows`); `flutter build windows --debug` build thành công và chạy bằng `flutter run -d windows`. Chưa mở cửa sổ app để thử bằng tay, chưa thử Android/iOS. Camera, `image_picker` và `flutter_secure_storage` gốc vì thế chưa được chạy trên thiết bị thật. App dùng `dart:io` nên chưa chạy trên trình duyệt (Chrome/Edge).
- Tìm địa chỉ cần Internet. Trên máy này không phân giải được `nominatim.openstreetmap.org`, nên chỉ kiểm chứng nhánh "không khả dụng → nhập tay". Bản đồ công cộng của OSM không có cam kết dịch vụ; production nên dùng nhà cung cấp có SLA.
- Ở Development với `Simulate=true`, mã email đúng vẫn **không** được ghi nhận vào tài khoản (thiết kế sẵn của OTP: kênh giả lập không được chứng nhận quyền sở hữu). App hiển thị đúng thông báo này. Cấu hình SMTP thật của bạn thì hoạt động bình thường.
- Admin chưa có màn xem ảnh/CCCD của hồ sơ KYC và `admin-web` chưa có nút Từ chối (API đã có). Đây là phần của quản trị viên, chưa nằm trong đợt này.
- Chưa có job xóa ảnh KYC theo `DeleteAfter`; quy mô lớn nên chuyển ảnh sang object storage.
- `Test-Smoke.ps1` chưa chạy vì nó build cấu hình Debug, mà API dev của bạn (cổng 5101) đang khóa file. Build/test dùng cấu hình `Batch1` để không đụng tiến trình đó.
- `appsettings.Development.json` đang chứa mật khẩu DB (có từ trước), trái quy tắc "không commit mật khẩu" trong `AGENTS.md`.

## 7. Cách chạy lại

Migration đã được áp cho DB dev `vantaiviet_core` (21/09/2026). Lần sau cần áp cho DB khác: `dotnet ef` **không** đọc `appsettings.Development.json` (xem `CoreDbContextFactory`, chỉ đọc user-secrets và biến môi trường, không có thì cố ý trỏ vào cổng 1), nên phải truyền connection:

```powershell
cd C:\Working\Vantaiviet
dotnet ef database update --project src/VantaiViet.CoreApi --connection "Host=localhost;Port=4455;Database=vantaiviet_core;Username=postgres;Password=..."
```

Test backend (DB test riêng đã có bảng mới; nếu tạo DB test mới thì phải migrate trước):

```powershell
$env:CORE_DATABASE_TEST_CONNECTION = "Host=localhost;Port=4455;Database=vantaiviet_core_test;Username=postgres;Password=..."
dotnet test tests/VantaiViet.CoreApi.Tests
```

Test frontend:

```powershell
cd C:\Working\Vantaiviet-fontend\shipper-mobile
flutter test                       # 125 test offline; test live tự bỏ qua
```

Test với API thật (dùng DB cô lập; API phải chạy ở môi trường không gửi mail thật, ví dụ đặt `Notifications__Email__Simulate=true` và `Notifications__Smtp__Host=smtp.invalid`; cần một tài khoản Admin để duyệt KYC):

```powershell
$env:SHIPPER_LIVE_API = "http://127.0.0.1:5111"
$env:SHIPPER_LIVE_ADMIN_PHONE = "+84..."; $env:SHIPPER_LIVE_ADMIN_PASSWORD = "..."
flutter test test/live
```

## 8. Tệp chính

Backend (`src/VantaiViet.CoreApi`): `Services/{AccountProfile,BusinessProfile,AccountKyc,Geocoding}Service.cs`, `Controllers/{Account,AccountKyc,ShipperBusinessProfile,Geocoding}Controller.cs`, `Controllers/ResultResponses.cs`, `Entities/{ShipperBusinessProfile,CargoTypes,KycConstants}.cs`, `Data/Migrations/*AddShipperOnboarding*`; sửa `ShipmentService`, `KycReviewService`, `VerificationController`, `ServiceCollectionExtensions`. Test: `tests/VantaiViet.CoreApi.Tests/{ApiHostFixture,ShipperOnboardingApiTests,ShipperMigrationTests}.cs`.

Frontend (`shipper-mobile/lib`): `core/`, `auth/`, `account/`, `shipment/`, `home_page.dart`, `home_tab.dart`, `main.dart`. Test: `test/` (đơn vị, `widget/`, `live/`). Hợp đồng API: `shared/api-contracts.md`.
