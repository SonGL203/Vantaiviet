# PostgreSQL

Hai API dùng Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 (EF Core 10).

| API | DbContext | Khóa cấu hình |
| --- | --- | --- |
| CoreApi | Data/CoreDbContext.cs | ConnectionStrings:CoreDatabase |
| MatchingApi | Data/MatchingDbContext.cs | ConnectionStrings:MatchingDatabase |

Context được đăng ký scoped; service inject trực tiếp để truy vấn và lưu dữ liệu.
Chưa khai báo DbSet vì chưa có entity. Mapping sẽ được lấy tự động từ các
IEntityTypeConfiguration trong assembly của từng API.

## Khi chưa có database

Hai API và endpoint system/info vẫn chạy mà không cần PostgreSQL.
Chỉ khi resolve DbContext mới yêu cầu connection string; thiếu cấu hình sẽ có lỗi
rõ trong log và response không lộ chi tiết. Connection string không bảo đảm database
truy cập được. Readiness hiện chưa kiểm tra PostgreSQL, cần bổ sung khi có use case
phụ thuộc database. Không tự EnsureCreated/Migrate hoặc kết nối lúc khởi động.

## Cấu hình khi có server

Dùng hai database và tài khoản riêng: vantaiviet_core và vantaiviet_matching.
Cung cấp các biến môi trường sau cho process/container tương ứng:

```text
ConnectionStrings__CoreDatabase=Host=localhost;Port=5432;Database=vantaiviet_core;Username=YOUR_CORE_USER;Password=YOUR_SECRET
ConnectionStrings__MatchingDatabase=Host=localhost;Port=5432;Database=vantaiviet_matching;Username=YOUR_MATCHING_USER;Password=YOUR_SECRET
```

Đây chỉ là mẫu, không có tài khoản thật. Không commit secrets. ASP.NET Core không
tự đọc file .env; nếu dùng Docker Compose cần truyền biến vào container.
Production cấu hình TLS và xác minh certificate theo nhà cung cấp; tách quyền
runtime và quyền chạy migration. Không bật sensitive data logging.

## Migration và GPS

Khi có entity, thêm Microsoft.EntityFrameworkCore.Design và dotnet-ef 10.x
tương thích, tạo migration riêng từng context. Chưa tạo migration rỗng hoặc
chạy database update trong bản base.

PostGIS/NetTopologySuite sẽ bổ sung cho MatchingApi khi triển khai GPS;
hiện chưa cài extension hoặc thêm spatial model.

Tài liệu: https://www.npgsql.org/efcore/release-notes/10.0.html
