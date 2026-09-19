# Vạn Tải Việt — backend foundation

Một solution .NET 10, hai ASP.NET Core Web API triển khai độc lập.
Coding convention: [AGENTS.md](AGENTS.md).

## Yêu cầu

- .NET SDK 10.0.400 (global.json cho phép bản vá cùng feature band).
- Visual Studio hỗ trợ .NET 10 hoặc Rider tương thích.
- PowerShell 7 để chạy smoke test.

## Build và kiểm tra

```powershell
dotnet restore VantaiViet.sln
dotnet build VantaiViet.sln --no-restore
pwsh -File scripts/Test-Architecture.ps1
pwsh -File scripts/Test-Smoke.ps1
```

Smoke test build solution, chạy hai process trên cổng HTTP riêng 15001/15002,
kiểm tra health, controller/service endpoint và ProblemDetails cho 404,
sau đó dừng đúng các process nó đã tạo.

## Chạy local

Chạy ở hai terminal:

```powershell
dotnet run --project src/VantaiViet.CoreApi --launch-profile http
dotnet run --project src/VantaiViet.MatchingApi --launch-profile http
```

| Ứng dụng | HTTP | HTTPS |
| --- | --- | --- |
| CoreApi | http://localhost:5101 | https://localhost:5001 |
| MatchingApi | http://localhost:5102 | https://localhost:5002 |

Muốn dùng HTTPS: chạy `dotnet dev-certs https --trust` rồi chọn
`--launch-profile https`. Không bắt buộc trust certificate để dùng HTTP local.

Mỗi API cung cấp:
- `GET /health/live`: process đang chạy.
- `GET /health/ready`: các readiness check đã đăng ký đạt.
- `GET /api/system/info`: ví dụ controller mỏng gọi service,
  trả tên dịch vụ, phiên bản và thời điểm UTC.

Readiness hiện chỉ phản ánh ứng dụng vì chưa có dependency ngoài.
Khi thêm database/broker, phải đăng ký health check tương ứng.

## Cấu trúc

```text
src/
  VantaiViet.CoreApi/
  VantaiViet.MatchingApi/
    Controllers/
    Services/
      Interfaces/
    DTOs/
    Entities/
    Data/
    Hosting/
    Properties/
    Program.cs
scripts/
```

Hai project có cùng cách tổ chức. Hosting chứa HTTP exception handler,
cấu hình pipeline và DI composition root. Services chứa nghiệp vụ và được truy vấn
DbContext trực tiếp. Entities chứa model dữ liệu; Data dành cho DbContext, mapping
và migration cho PostgreSQL. Không có Repository hoặc tầng Domain/Application.

## Có sẵn

- Nullable, compiler warnings as errors và .editorconfig.
- DI và endpoint mẫu theo Controller → Service.
- ProblemDetails cho lỗi HTTP, traceId và errorCode.
- Exception handler tập trung; không lộ exception detail qua response.
- Structured JSON console logging và health endpoints.
- CORS allowlist, chỉ cho Angular localhost:4200 trong Development.
- HTTPS redirection ngoài Development.
- Hai profile local, không chứa secrets.
- Architecture guard và smoke test không cần package bên thứ ba.

## Phạm vi hiện tại

Đây là khung backend, đã cấu hình EF Core/PostgreSQL nhưng chưa có database thực tế,
entity, migration, CRUD, JWT/OIDC, tenant membership,
SignalR hub, matching thực tế, thanh toán hoặc Angular app.
Chưa có HTTP integration giữa hai API vì chưa có use case cần gọi.

Endpoint system/info và health là public, không chứa dữ liệu nghiệp vụ.
Trước khi thêm endpoint nghiệp vụ phải cấu hình authentication,
authorization và tenant context đã xác thực; không dùng header tenant tự khai báo.
Không coi bộ khung này là hệ thống nghiệp vụ production-ready.

Không commit secrets. Dùng environment variables hoặc secret manager.
Production cần cấu hình TLS, trusted proxy (nếu có), CORS và identity provider
phù hợp môi trường. Không tự bật forwarded headers cho proxy bất kỳ.

## Thêm tính năng

Cấu hình PostgreSQL và connection string: [docs/database.md](docs/database.md).

1. Đặt request/response trong DTOs, service trong Services, interface trong Services/Interfaces.
2. Đặt entity dữ liệu trong Entities; quy tắc nghiệp vụ nằm trong service.
3. Đặt DbContext/mapping/migration trong Data. Service truy vấn DbContext trực tiếp.
4. Đăng ký DI ở Hosting/ServiceCollectionExtensions.cs.
5. Controller chỉ nhận input, gọi service và map HTTP response.
6. Thêm test nghiệp vụ, authorization và tenant isolation tương ứng.

Architecture guard hiện quét source để phát hiện một số dependency bị cấm
ở controller, entity và project reference. Đây là kiểm tra nhẹ, có thể bị bỏ sót
qua alias hoặc lời gọi gián tiếp; chưa thay thế architecture test phân tích IL,
kiểm tra dependency cycle hoặc code review. Cần mở rộng khi có module nghiệp vụ.
