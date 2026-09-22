# Vạn Tải Việt — Coding Rules

Các quy tắc này áp dụng cho toàn bộ mã nguồn trong repository. PHẢI và KHÔNG ĐƯỢC là yêu cầu bắt buộc; NÊN là mặc định, ngoại lệ cần có lý do kỹ thuật cụ thể.

## 1. Kiến trúc và quyền sở hữu

- Một solution `VantaiViet.sln`, hai ứng dụng triển khai độc lập: `VantaiViet.CoreApi` và `VantaiViet.MatchingApi`.
- Cho phép project kiểm thử và class library có trách nhiệm rõ ràng. Không tạo thêm ứng dụng triển khai độc lập khi chưa có yêu cầu.
- CoreApi sở hữu người dùng, tenant, KYC, đơn vận tải, báo giá, booking, điều phối chính thức, thanh toán, công nợ và SignalR thông báo.
- MatchingApi sở hữu tìm kiếm địa lý, lọc ứng viên, chấm điểm và gợi ý ghép chuyến chiều về. Có thể dùng ASP.NET Core Web API kết hợp BackgroundService.
- MatchingApi không tự xác nhận booking, điều xe, duyệt KYC hoặc thay đổi thanh toán.
- Hai ứng dụng không tham chiếu trực tiếp executable project của nhau; không chia sẻ EF entity hoặc DbContext. Giao tiếp qua hợp đồng HTTP hoặc message tường minh.
- Đây là kiến trúc hai dịch vụ, không gọi toàn bộ hệ thống là modular monolith.
- Bên trong mỗi ứng dụng, tổ chức `Controllers`, `Services`, `Services/Interfaces`, `Entities`, `DTOs`, `Data`; nhóm theo feature khi cần. Giữ `Hosting` cho cấu hình khởi động, CoreApi có thể có `Hubs`, MatchingApi có thể có `Workers`.
- Không dùng tầng Application/Domain/Infrastructure hoặc Repository/Unit of Work riêng. Service được inject DbContext và truy vấn EF Core trực tiếp.
- Controller chỉ phụ thuộc service và DTO; entity không phụ thuộc controller/service. Data chứa DbContext, entity configuration và migration khi đã chọn database.
- Không tự thêm CQRS, MediatR, event sourcing, generic base framework hoặc message broker khi chưa có nhu cầu cụ thể.

## 2. Controller, Hub và Worker phải thin

- Controller chỉ nhận request, gọi service và chuyển kết quả thành HTTP response.
- Mặc định một action thực hiện một use case bằng một service method. Việc điều phối nhiều bước nằm trong service.
- Tất cả CRUD, kể cả CRUD đơn giản, phải đi qua service.
- Controller KHÔNG inject DbContext, repository, query service, Unit of Work, cache, storage hoặc external API client.
- Controller KHÔNG chứa SQL, LINQ truy vấn nghiệp vụ, SaveChanges, transaction, tính phí, kiểm tra khả dụng hoặc chuyển trạng thái entity.
- Controller dùng request/response DTO; không trả EF entity hoặc IQueryable.
- Không lặp try/catch ở controller để xử lý lỗi chung; dùng exception handler tập trung.
- Truyền CancellationToken xuống service.
- SignalR Hub chỉ xử lý kết nối, kiểm tra quyền tham gia nhóm và chuyển yêu cầu tới service; không CRUD trực tiếp.
- Worker chỉ nhận/kích hoạt công việc, tạo DI scope, gọi service và quản lý cancellation/acknowledgement. Không chứa thuật toán hoặc truy vấn nghiệp vụ.
- Không giới hạn số dòng máy móc cho controller. Thin được đánh giá bằng trách nhiệm, không phải việc giấu code trong private method. Quy tắc này nói về trách nhiệm của controller và không thay thế ngưỡng độ dài/độ phức tạp của truy vấn ở mục 5.

## 3. Service

- Luồng thống nhất: Controller/Hub/Worker → Service → DbContext → Database.
- Service sở hữu use case: kiểm tra quyền tài nguyên, điều phối nghiệp vụ, xác định transaction boundary và commit.
- Nhận input DTO; trả response DTO hoặc Result<T> theo hợp đồng thống nhất của dự án.
- Không trả IActionResult/HTTP status; không nhận HttpContext, HttpRequest hoặc ClaimsPrincipal.
- Dùng ICurrentActor/ITenantContext đã được xác thực để truy cập ngữ cảnh.
- Service được phép dài nếu vẫn phục vụ một trách nhiệm nhất quán. Không đẩy nghiệp vụ lên controller để làm service ngắn hơn.
- Tách theo nghiệp vụ khi trách nhiệm độc lập hoặc dependency quá nhiều, ví dụ DispatchService, FreightPricingService, SettlementService.
- Không tạo CommonService, HelperService hoặc BaseService gom nghiệp vụ; không có phụ thuộc vòng tròn.
- Không tạo lớp chuyển tiếp thuần túy chỉ để tăng số tầng. Service vẫn là boundary cho mọi use case, kể cả CRUD đơn giản.

## 4. Entity, DTO và validation

- Validation hình dạng input nằm ở request validator; điều kiện use case, authorization và quy tắc nghiệp vụ nằm ở Services; Entities mô tả dữ liệu.
- Không sao chép cùng một quy tắc nghiệp vụ vào nhiều service.
- Service kiểm tra chuyển trạng thái qua method nghiệp vụ rõ ràng: AssignCarrierAsync, StartTripAsync, ConfirmDeliveryAsync, ApproveSettlementAsync.
- Không map request trực tiếp vào trạng thái nghiệp vụ; chỉ service được thay đổi sau khi kiểm tra điều kiện.
- Entity không gọi database, HTTP, cache hoặc SDK bên ngoài. Service dùng TimeProvider khi cần thời gian.
- Tách kiểu dữ liệu riêng khi có nhu cầu cụ thể; không bắt buộc value object hoặc domain policy.
- Tách DTO tạo mới, cập nhật và trả về. Chỉ map các field cho phép để chống overposting.
- Client không quyết định TenantId, quyền sở hữu, giá chốt hoặc trạng thái thanh toán.

## 5. Persistence và EF Core

- Database thống nhất là PostgreSQL, dùng Npgsql EF Core provider tương thích EF Core 10. CoreDbContext và MatchingDbContext độc lập; connection string lấy từ configuration/secrets, không commit mật khẩu.
- Không tự chạy EnsureCreated hoặc Migrate khi khởi động. Migration được triển khai có kiểm soát khi đã có entity và database.

- Service inject DbContext trực tiếp; thực hiện LINQ, projection, CRUD, transaction và SaveChangesAsync tại service.
- Data chứa DbContext, mapping/configuration và migration. Không thêm Repository hoặc Unit of Work bọc DbContext.
- Service không trả IQueryable hoặc EF entity ra controller; trả DTO hoặc Result<T>.
- Service quyết định transaction boundary; gom các thay đổi cần nguyên tử vào một transaction, tránh SaveChanges riêng lẻ trong vòng lặp.
- Query đọc lấy đúng field, lọc/sắp xếp/phân trang tại database; dùng AsNoTracking khi materialize entity không cần tracking.
- Phân trang có thứ tự ổn định và giới hạn page size. Tránh N+1 và query trong vòng lặp.
- Không chạy đồng thời nhiều thao tác trên cùng một DbContext.
- Dùng database constraint bảo vệ uniqueness; kiểm tra trước bằng code không thay thế constraint.
- Dùng concurrency control cho cập nhật cạnh tranh. Chống đặt trùng phải dựa trên invariant và cơ chế database phù hợp, không chỉ một lần kiểm tra khả dụng.
- Mỗi ứng dụng sở hữu DbContext và migration riêng nếu sử dụng EF Core.
- Có thể chung database server nhưng phải tách database/schema và quyền truy cập. Không cross-write hoặc truy vấn trực tiếp bảng của ứng dụng kia.
- Migration phải xét mất dữ liệu, lock và tương thích giữa các phiên bản triển khai.

### Procedure và truy vấn phức tạp

- NÊN dùng stored procedure cho truy vấn dài/phức tạp: nhiều join, CTE, tổng hợp nhiều bước, báo cáo hoặc xử lý dữ liệu theo tập hợp. Với PostgreSQL, dùng function trả bảng khi cần truy vấn trả tập kết quả; dùng procedure khi phù hợp với thao tác thực thi. CRUD và truy vấn đơn giản tiếp tục dùng EF Core.
- Khi gặp truy vấn thuộc nhóm trên trong phạm vi task, phải đánh giá procedure/function trước khi viết thêm LINQ hoặc SQL dài trong C#; nếu giữ trong C#, nêu ngắn gọn lý do kỹ thuật. Không chuyển truy vấn ngoài phạm vi task.
- Service gọi procedure/function qua DbContext/connection của ứng dụng, truyền tham số an toàn và CancellationToken; không nối input vào SQL. Service vẫn kiểm tra authorization, sở hữu use case và transaction boundary; routine không tự commit/rollback transaction do service quản lý.
- Procedure/function phải scope tenant và participant/grant tường minh từ ngữ cảnh đã xác thực; không dựa vào EF query filter cho SQL trong database. Không truy cập dữ liệu của ứng dụng kia.
- Lưu định nghĩa SQL trong Data và triển khai bằng migration hoặc script có version, có cách rollback phù hợp. Không chỉ tạo thủ công trong database rồi bỏ qua mã nguồn.
- Được phép kết nối database và tạo/cập nhật procedure/function trực tiếp khi cần cho task, bằng cấu hình/secrets sẵn có, sau khi xác định đúng database/schema và kiểm tra định nghĩa hiện tại. Không cần hỏi lại cho thao tác đã được cho phép này; không mở rộng sang xóa dữ liệu hoặc thay đổi ngoài phạm vi task. Đồng bộ thay đổi vào migration/script để triển khai lại được; không log credentials.
- Kiểm chứng routine trên PostgreSQL, gồm kết quả, tenant isolation và transaction/concurrency khi liên quan. Chỉ khẳng định cải thiện hiệu năng khi có đo đạc hoặc execution plan hỗ trợ.

## 6. Tenant và phân quyền

- Phân biệt dữ liệu riêng của tenant, dữ liệu nền tảng và giao dịch chia sẻ giữa các doanh nghiệp.
- Dữ liệu riêng phải có TenantId; scope cả đọc, ghi, cache, file và background job.
- Xác thực membership trước khi chọn tenant. Không tin TenantId từ header/body mà chưa kiểm tra.
- Global query filter là lớp bảo vệ bổ sung, không thay thế authorization, kiểm tra khi ghi hoặc scope raw SQL.
- Không tắt tenant filter tùy tiện; luồng quản trị xuyên tenant phải có quyền tường minh và audit.
- Giao dịch chia sẻ dùng participant/grant rõ ràng cho chủ hàng, môi giới, nhà vận tải; giới hạn cả field nhạy cảm được phép thấy.
- MatchingApi phải tự kiểm tra quyền tại endpoint. Service authentication không tự cấp quyền truy cập mọi tenant.
- SignalR group và đường dẫn file không phải cơ chế authorization; kiểm tra quyền trước khi cho truy cập.

## 7. Vận tải và matching

- Phân biệt Shipment, Trip, Leg, Vehicle, Trailer, Container, Quote, Booking và Settlement; không gom vào một entity Order.
- Lọc điều kiện bắt buộc trước khi chấm điểm: loại phương tiện/container, tải trọng, kích thước, khung giờ, khả dụng và điều kiện tuyến.
- Không kết luận tuyến khả thi chỉ bằng khoảng cách đường chim bay.
- MatchingService điều phối; truy vấn ứng viên, policy điều kiện, thuật toán tính điểm và route provider có trách nhiệm tách biệt.
- Thuật toán tính điểm không tự gọi I/O. Kết quả phải tái hiện được khi cố định input, cấu hình, dữ liệu tuyến và random seed nếu có.
- Kết quả gợi ý có candidate, score/lý do, thời điểm tạo, hạn dùng và phiên bản thuật toán; lưu version input khi cần tái hiện.
- CoreApi kiểm tra lại quyền, trạng thái và khả dụng trước khi xác nhận. Gợi ý còn hạn không bảo đảm phương tiện còn trống.
- Lưu snapshot giá cước, phụ phí và hoa hồng đã chốt; không tính lại lịch sử bằng bảng giá hiện tại.
- Đại lượng phải có đơn vị rõ ràng. Lưu thời điểm UTC; lịch hẹn có múi giờ liên quan khi cần.
- Phân biệt thời gian phát sinh GPS và thời gian nhận; không để sự kiện cũ ghi đè mới; có ngưỡng dữ liệu vị trí hết hạn.

## 8. Tài chính

- Dùng decimal ở C# và exact numeric tại database cho tiền, luôn kèm currency; quy định precision, scale và cách làm tròn.
- Không dùng float/double để tính tiền. Back-end quyết định số tiền cuối cùng.
- Mutation tài chính phải có idempotency: key theo tenant/operation, fingerprint input, chống concurrent duplicate; cùng key khác input phải bị từ chối.
- Xác thực webhook, chống lặp và xử lý sai thứ tự. Không xác nhận thanh toán từ redirect trình duyệt.
- Timeout có thể là trạng thái chưa xác định; không tự tạo giao dịch mới trước khi đối soát an toàn.
- Nếu quản lý sổ cái/số dư: dùng bút toán kép cân bằng theo currency; bút toán đã ghi sổ không sửa/xóa, điều chỉnh bằng bút toán liên kết.
- Không đổi số dư mà không có bút toán tương ứng. Kiểm tra hạn mức và phân tách người tạo/người duyệt theo policy nghiệp vụ.

## 9. Tích hợp, async và lỗi

- Gọi dịch vụ qua typed client/interface, URL từ configuration; không hard-code cổng localhost.
- Có service authentication, timeout, cancellation, trace context và error contract.
- Không giữ database transaction trong khi chờ HTTP bên ngoài; không giả định transaction bao phủ hai ứng dụng.
- HTTP cho kết quả tức thời; job/message cho công việc nền khi cần. Không bắt buộc thêm broker ở giai đoạn chưa cần.
- Khi cần bảo đảm ghi dữ liệu và phát event tin cậy, dùng transactional outbox; consumer chịu được message lặp/sai thứ tự.
- Không fire-and-forget cho công việc quan trọng. Retry có giới hạn; mutation chỉ retry khi có idempotency.
- Dùng async/await; không .Result, .Wait(), async void ngoài event handler hoặc Task.Run bọc I/O đã async.
- Truyền CancellationToken cho I/O phù hợp; công việc durable đã tiếp nhận có vòng đời riêng, không mất do client ngắt kết nối.
- Lỗi nghiệp vụ dự kiến dùng Result<T> thống nhất; exception dành cho lỗi bất ngờ/hạ tầng, xử lý tập trung.
- Dùng ProblemDetails với errorCode và traceId. Không trả stack trace/SQL; không catch rồi nuốt lỗi hoặc trả thành công giả.
- Phân biệt không có ứng viên với matching không khả dụng. Matching lỗi không được làm hỏng chức năng độc lập của CoreApi.
- Structured logging, không log secrets hoặc payload cá nhân/tài chính đầy đủ. Audit nghiệp vụ tách khỏi log chẩn đoán.

## 10. C# conventions

- Bật nullable reference types; không dùng null-forgiving để che thiết kế nullability sai.
- PascalCase cho type/method/property; camelCase cho parameter/local; _camelCase cho private instance field; interface tiền tố I.
- Method async có hậu tố Async; CancellationToken là tham số cuối khi phù hợp.
- Constructor injection; không service locator hoặc mutable static state.
- Method dùng thân hàm `{ }` với `return` tường minh; không viết expression-bodied method (`=>`) cho action controller, method service, helper private hay hub method.
- Giữ nguyên expression-bodied cho property truy cập trực tiếp (`DbSet<T> X => Set<T>();`), factory một dòng của record (`ApiResponse<T>.Ok`), và mọi lambda truyền làm đối số (LINQ, cấu hình DI, switch expression). Đây là biểu thức, không phải thân method.
- Không magic number, chuỗi trạng thái rải rác, dead code hoặc comment-out code.
- Comment giải thích lý do. Thống nhất formatting qua .editorconfig khi có mã nguồn.
- Dùng phiên bản ổn định còn hỗ trợ; không tự nâng major hoặc đưa API preview vào luồng production.

## 11. Angular

- Luồng thống nhất: Component → Feature Facade → API Client. Component không inject HttpClient.
- Component xử lý UI; facade quản lý state/điều phối; API client xử lý HTTP và contract có kiểu.
- Standalone component cho code mới; TypeScript strict và strictTemplates.
- Signals cho state đồng bộ, computed cho dữ liệu dẫn xuất; RxJS cho luồng async, tìm kiếm và realtime.
- Không nested subscribe; dùng AsyncPipe/takeUntilDestroyed cho subscription cần quản lý vòng đời.
- Không effect sao chép state có thể tính bằng computed; không nhiều nguồn sự thật cho cùng dữ liệu.
- Typed reactive forms cho form nghiệp vụ; không any để bỏ qua lỗi kiểu.
- Lazy loading theo feature; có loading, empty, error và success.
- Đổi tenant phải hủy request và xóa state/cache cũ; không nhận kết quả muộn của tenant trước. Realtime reconnect phải đồng bộ lại từ server.
- Không optimistic update cho thanh toán hoặc xác nhận điều xe; không tự retry mutation thiếu idempotency.
- Contract tiền phải bảo toàn độ chính xác, ví dụ chuỗi decimal kèm currency; không tính tiền bằng number thông thường.
- Route guard không thay thế authorization back-end; không tùy tiện bỏ sanitization.

## 12. Kiểm chứng quy tắc

- Architecture test kiểm tra controller không phụ thuộc DbContext/Data/Entities, entity không phụ thuộc Services/Controllers và không có dependency cycle.
- Khi các tầng chung một csproj, kiểm tra theo namespace/type dependency; compiler không tự bảo vệ ranh giới folder.
- Test quy tắc nghiệp vụ tại service, authorization/tenant isolation, concurrent booking, idempotency và webhook lặp.
- Test transaction, constraint và SQL quan trọng bằng cùng loại database production; không dùng EF InMemory để chứng minh hành vi đó.
- Không viết test chỉ lặp implementation hoặc tăng coverage hình thức.
- Build, static analysis và các test liên quan phải đạt trước khi merge.

## 13. Quy tắc làm việc tiết kiệm token

- Ưu tiên theo thứ tự: tính đúng đắn → thay đổi tối thiểu → tái sử dụng code → ngữ cảnh tối thiểu → giải thích tối thiểu. Không hy sinh tính đúng đắn để tiết kiệm token.
- Trả lời ngắn gọn; không lặp yêu cầu, giải thích code hiển nhiên, xuất toàn bộ file hoặc viết tổng kết dài. Không tạo tài liệu khi chưa được yêu cầu; không thêm comment cho code hiển nhiên.
- Trước khi code, tìm symbol/reference và kiểm tra implementation hiện tại. Chỉ đọc file liên quan; không quét toàn repository, mở file lớn hoặc thư mục không liên quan khi không cần.
- Tái sử dụng kiến trúc, pattern, helper, component và naming/style hiện có. Ưu tiên sửa code sẵn có thay vì viết lại file; không tạo code trùng, dependency hoặc abstraction không cần thiết.
- Chỉ thay đổi phần cần cho task. Không tự refactor, rename, reformat phần không liên quan; không nâng package, đổi kiến trúc hoặc thêm tính năng khi không cần cho yêu cầu. Giữ tương thích ngược trừ khi task yêu cầu thay đổi.
- Nếu phát hiện lỗi khác, chỉ báo ngắn gọn; không sửa trừ khi lỗi đó chặn task hiện tại.
- Khi sửa bug: xác định code liên quan → tìm nguyên nhân → sửa tối thiểu → kiểm chứng → dừng. Không tiếp tục khám phá khả năng không liên quan sau khi lỗi đã được giải quyết.
- Không chạy lệnh tốn kém khi không cần; không chạy lặp lệnh hoặc đọc lại file không đổi nếu không có lý do. Tránh đọc node_modules, bin, obj, dist, build, .git, generated files và package cache trừ khi cần cho task.
- Sau thay đổi, chạy test/build liên quan nhỏ nhất trước; chỉ sửa lỗi thuộc thay đổi hiện tại. Chỉ mở rộng kiểm thử khi cần, đồng thời bảo đảm các kiểm tra bắt buộc trước merge ở mục 12.
- Cập nhật tiến độ ngắn gọn. Kết quả cuối dùng ba phần `Changed:`, `Files:`, `Validation:` với các gạch đầu dòng ngắn mô tả thay đổi, file đã sửa và kết quả test/build; ghi rõ nếu chưa chạy.
- Không hỏi khi có thể xác định an toàn từ repository. Chỉ hỏi khi yêu cầu bắt buộc còn mơ hồ, các lựa chọn làm thay đổi đáng kể hành vi, hoặc thiếu thông tin cần thiết không có trong repository.
- Với task lớn, chia thành bước nhỏ độc lập, hoàn thành từng bước và giữ ngữ cảnh tập trung vào tính năng được yêu cầu.

## Tài liệu tham chiếu

Các ranh giới và lựa chọn cụ thể trên là convention của Vạn Tải Việt, không phải toàn bộ đều là yêu cầu của các nhà cung cấp.

- Microsoft, thin controllers: https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/develop-asp-net-core-mvc-apps
- Microsoft, application architecture: https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures
- .NET Runtime coding style: https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md
- EF Core query filters: https://learn.microsoft.com/en-us/ef/core/querying/filters
- Angular signals: https://angular.dev/guide/signals

## 14. Tiến độ và bàn giao giữa các phiên (app Chủ hàng)

Người dùng giao việc theo bảng 36 tính năng "CHỦ HÀNG", mỗi lần 10 tính năng, gồm backend (repo này) và Flutter (`C:\Working\Vantaiviet-fontend\shipper-mobile`). Cập nhật mục này cuối mỗi đợt; không ghi mật khẩu hay khóa vào đây.

### Đã xong (cập nhật 22/09/2026, sau đợt 5 rà soát)

- **Đợt 1, tính năng 1–10** (đăng ký, đăng nhập/đăng xuất, quên/đổi mật khẩu, hồ sơ cá nhân, hồ sơ doanh nghiệp, xác minh tài khoản, đăng hàng, điểm lấy, điểm giao, loại hàng). Báo cáo chi tiết: `docs/shipper-batch-1-report.md`.
  - API mới: `GET/PUT /api/account/profile`, `GET/PUT /api/shipper/business-profile`, `/api/account/kyc` (`identity`, `documents/{loại}`, `submit`), `POST /api/admin/kyc/{id}/reject`, `GET /api/geocoding/search`. Lô hàng có `CargoType` (11 loại, bắt buộc mô tả với `Other`) và `CargoDescription`. Migration `AddShipperOnboarding`.
  - Lỗi có sẵn đã sửa: `VerificationController` trả 500 khi sai mã OTP. Dùng helper `ResultResponses.FromResult` thay cho `Problem(extensions: ...)`.
- **Đợt 2, tính năng 11–20** (số lượng, loại xe, giờ lấy hàng kèm hạn giao tùy chọn, giá đề xuất, ghi chú, ảnh hàng hóa, danh sách đơn, chi tiết đơn, sửa đơn, hủy đơn). Migration `AddShipmentDetails`. Chưa có báo cáo riêng cho đợt này.
  - Số lượng kèm đơn vị (`Piece`, `Package`, `Box`, `Pallet`, `Bag`, `Roll`, `Drum`); loại xe là yêu cầu hiển thị cho tài xế (`Any`, `Van`, `SmallTruck`, `MediumTruck`, `HeavyTruck`, `Refrigerated`, `Tractor`, `Container`, `Tanker`), không dùng để lọc điều kiện nhận đơn.
  - Giá là chuỗi số đồng nguyên kèm `currency: "VND"` (`core/money.dart` ở app). `PUT` có `quantity` nghĩa là gửi trạng thái đầy đủ (giá trị null là xóa).
  - Ảnh: `api/shipments/{id}/photos` (danh sách, tải lên, xem, xóa), lưu `bytea` trong PostgreSQL, tối đa 6 ảnh × 5 MB (hằng số `ShipmentLimits`); quy mô lớn thì chuyển sang object storage.
  - Sửa/hủy đơn theo luật "đã có người nhận" = có yêu cầu vận chuyển đang chờ, hoặc đề nghị môi giới đang chờ/đã nhận.
  - App: tab "Đơn hàng" (`shipment_list_tab.dart`, `shipment_list_store.dart`), `shipment_detail_screen.dart`, `shipment_form_screen.dart`, `shipment_photos.dart`. Test backend: `ShipperShipmentApiTests.cs`; test app: `shipment_*_test.dart`, `money_test.dart`, `widget/shipment_*`.
- **Đợt 3, tính năng 21–30** (đăng lại đơn, sao chép đơn, tìm theo mã/tuyến, lọc trạng thái, xem người nhận đơn, xem hồ sơ, chấp nhận/từ chối, chat, gọi điện, theo dõi trạng thái). Migration `AddShipmentCodeAndChat`. "Cò" được hiểu là bên nhận đơn: tài xế gửi yêu cầu kèm xe (`TransportRequest`) và môi giới được giao đơn (`ShipmentBroker`).
  - Đơn có `Code` (`VT0000123`, sequence `ShipmentCodes` làm giá trị mặc định của cột). `GET /api/shipments/mine?q=&status=` (`status` là danh sách phân cách bằng dấu phẩy). `POST /api/shipments/{id}/repost` (chỉ đơn đã hủy, tạo đơn mới đã đăng) và `/copy` (bất kỳ đơn, tạo bản nháp); cả hai sao chép ảnh bằng SQL và giữ khoảng cách giữa giờ lấy và hạn giao.
  - Dịch vụ mới: `ShipmentCarrierService` (`GET shipments/{id}/takers` và `.../takers/{userId}`), `ShipmentChatService` (`.../messages`, poll bằng `afterId`; ghi tin dùng `pg_advisory_xact_lock` theo cuộc trò chuyện để id tăng theo thứ tự commit, nếu không người đọc đang giữ id lớn sẽ bỏ sót tin id nhỏ commit muộn), `ShipmentProgressService` (`.../tracking`). Chấp nhận/từ chối dùng lại `transport-requests/{id}/accept|reject`; thu hồi môi giới dùng lại `.../broker/revoke`.
  - Đã sửa lỗi có sẵn `TripsController` trả 500 cho mọi nhánh lỗi (nay dùng `FromResult(result, "trip")`).
- **Đợt 4, tính năng 31–36** (xác nhận hoàn thành, đánh giá, báo sự cố, thông báo, lịch sử đơn, địa chỉ đã lưu). Migration `AddShipperFollowUpData`.
  - Xác nhận hoàn thành dùng lại `POST /api/trips/{id}/complete` (chỉ chủ đơn, khi chuyến đã `Delivered`).
  - Đánh giá `TripReview` (mỗi người một lần mỗi chuyến, khóa chính `(TripId, TargetUserId)`), sự cố `ShipmentIncident` (đồng thời ghi `TripEvent` để tài xế thấy), thông báo `UserNotification` (không đẩy; app poll số chưa đọc; tạo qua `NotificationEvents.RaiseAsync` trong cùng unit of work với thay đổi gây ra nó, các hook nằm ở `BookingService`, `ShipmentBrokerService`, `ShipmentService.CancelAsync`, `TripService.ChangeAsync`, chat, đánh giá, sự cố), nhật ký đơn `GET shipments/{id}/history` (gộp `AuditEvent`, `TripEvent`, sự cố, đánh giá), địa chỉ đã lưu `SavedAddress` (`/api/account/addresses`, tối đa 30, nhãn không trùng không phân biệt hoa thường).
  - Điểm đánh giá (`rating`) và `recentReviews` hiện trong danh sách người nhận đơn và hồ sơ.
  - Test backend: `ShipperFollowUpApiTests.cs` (21–30), `ShipperFinishingApiTests.cs` (31–36), dùng chung `ShipmentTestKit.cs`. Test app: `followup_units_test.dart`, `finish_units_test.dart`, `widget/order_followup_test.dart`, `widget/order_finishing_test.dart`, fake ở `fakes_orders.dart` và `fakes_finish.dart`; live: `test/live/live_followup_api_test.dart` và `live_followup_ui_test.dart` (cần Admin để duyệt tài xế, dùng `live_support.dart`).
  - App: `OrderServices` (`core/services.dart`) gom `carriers`, `tracking`, `finish`, `notifications`, `addresses`, `launch` (mở trình quay số/bản đồ qua `url_launcher`), `liveInterval` (chat, theo dõi) và `notificationInterval`. Tab thứ 3 là "Thông báo" (huy hiệu số chưa đọc), lịch sử đơn mở từ biểu tượng đồng hồ ở tab Đơn hàng, địa chỉ đã lưu ở tab Tài khoản và trong form đăng hàng. Không có bản đồ trong app: vị trí xe mở bằng liên kết Google Maps.
- **Đợt 5, rà soát cả bảng 36 tính năng** (người dùng nhắc "hình như còn thiếu danh sách các bài đăng"). Không có migration mới. Bốn chỗ hụt so với bảng đã bổ sung:
  - Tính năng 17 (xem tất cả hàng đã đăng): tab "Đơn hàng" đã có từ đợt 2 nhưng Trang chủ chưa có danh sách. Nay Trang chủ có `_OrdersOverview` (`home_tab.dart`): 5 ô đếm theo trạng thái (`home-stat-<status>`, bấm để mở tab Đơn hàng đã lọc), "Đơn gần đây" (`homeRecentCount = 3`, `shipment_list_store.dart`) và "Xem tất cả". API mới `GET /api/shipments/mine/summary` → `ShipmentSummaryResponse(draft, published, booked, completed, cancelled, pendingRequests)`; app đọc qua `ShipmentSummaryStore`.
  - Tính năng 25 (biết ai đang xử lý): `ShipmentResponse.pendingRequests` (yêu cầu vận chuyển đang chờ + đề nghị môi giới đang chờ, tính ở `ShipmentRules.PendingRequestsAsync` cho chi tiết và trong phép chiếu của danh sách). Thẻ đơn hiện chip `shipment-pending-<id>`, Trang chủ hiện băng `home-pending` dẫn tới các đơn đang tìm xe.
  - Tính năng 2 (quản lý phiên đăng nhập): API `GET/DELETE /api/auth/sessions` đã có nhưng app chưa có màn hình. Nay có `account/devices_screen.dart` (mục `menu-devices` ở tab Tài khoản: xem thiết bị, đăng xuất một thiết bị hay tất cả thiết bị khác). Đăng nhập/đăng ký/làm mới nhận thêm `deviceName` (tùy chọn, tối đa 80 ký tự, lưu ở `AuthSessions.DeviceName` đã có sẵn cột), app gửi `deviceLabel()` (`core/device.dart`). Hai phiên cùng một tài khoản có thể hiện "Thiết bị không rõ tên" nếu không gửi tên (ví dụ phiên do `register` ở client khác tạo ra).
  - Tính năng 29 (liên hệ sau khi kết nối): trước đây số điện thoại của tài xế hiện ngay khi họ gửi yêu cầu. Nay `ShipmentCarrierService` (danh sách người nhận và hồ sơ) và `ShipmentChatService` (`partner.phone`) chỉ trả số khi yêu cầu vận chuyển hoặc đề nghị môi giới là `Accepted` (`PhoneOnceConnected`); trước đó là `null`, nhắn tin vẫn mở từ đầu. App hiện "Hiện sau khi bạn chấp nhận" ở hồ sơ và dòng gợi ý `takers-phone-hint`, nút gọi (`taker-call-<id>`, `profile-call`, `chat-call`) chỉ có sau khi chấp nhận. Đây là quy tắc theo đúng chữ của bảng; muốn hiện số sớm hơn thì sửa `PhoneOnceConnected` và `CarrierAccepted`.
  - Test: backend thêm ca trong `ShipperFollowUpApiTests.cs` và `ShipperFinishingApiTests.cs` (78/78 đạt); app 426 đạt, phân tích tĩnh sạch, bản Windows debug build được; app thêm `home_and_devices_test.dart`, `home_devices_units_test.dart`, sửa `order_followup_test.dart` và các fake (`fakes.dart`, `fakes_orders.dart`, `widget/harness.dart`); hai test live follow-up cập nhật theo quy tắc số điện thoại và thêm kiểm `summary`, `pendingRequests`, thiết bị (31/31 live đạt trên DB tạm).
- Đã thêm nền tảng Windows cho `shipper-mobile`; chạy bằng `flutter run -d windows` (mặc định gọi `http://localhost:5101`, đổi bằng `--dart-define=API_BASE_URL=...`). Chưa thử Android/iOS, chưa chạy trên trình duyệt (dùng `dart:io`).
- DB dev `vantaiviet_core` có 2 tài khoản chủ hàng demo: `+84900000101` (Active) và `+84900000102` (PendingKyc); mật khẩu do người dùng nắm. Hai DB `vantaiviet_core` và `vantaiviet_core_test` đã áp migration đến `AddShipperFollowUpData` (API dev chạy bản build cũ vẫn hoạt động vì cột `Code` có giá trị mặc định ở DB; cần build lại API để dùng tính năng mới).

### Còn treo

- **Yêu cầu chưa áp dụng:** bỏ xác minh email trong app chủ hàng, thay bằng xác minh số điện thoại nhưng để trống vì chưa có API xác thực số điện thoại (kênh `Phone` của OTP trả 503 `not_configured`). Kế hoạch: gỡ `EmailVerificationSheet` và khối email ở `profile_screen.dart`, các hàm email trong `account_repository.dart`, model `EmailChallenge`/`EmailVerification`, tham số `pollInterval`, hàng checklist email ở `home_tab.dart` và các test tương ứng; thêm nút "Xác minh số điện thoại (sắp có)" bị vô hiệu ở hồ sơ cá nhân và hàng "Xác minh số điện thoại: Sắp có" (không bấm được) ở Trang chủ. Giữ nguyên backend và luồng quên mật khẩu bằng email. Hệ quả cần nói với người dùng: tài khoản mới không có cách thêm email đã xác minh nên chưa khôi phục mật khẩu được, cho tới khi có khôi phục qua số điện thoại.
- **Câu hỏi mở cho người dùng sau đợt 5** (chưa làm vì bảng không nói rõ): (a) tính năng 27 "Chọn cò nếu cần xác nhận" có thể hiểu là tùy chọn tự động chấp nhận yêu cầu đầu tiên (mặc định vẫn là chủ hàng tự chọn); (b) tính năng 12 loại xe mới là yêu cầu hiển thị, chưa chặn tài xế có xe không hợp; (c) sự cố chưa cho đính kèm ảnh; (d) quên mật khẩu chỉ có qua email đã xác minh, chưa có SMS; (e) chưa có thông báo đẩy và bản đồ trong app.
- **Đã hết bảng 36 tính năng của chủ hàng.** Việc còn lại xoay quanh app khác: app tài xế và môi giới chưa có giao diện chat, thông báo, đánh giá tương ứng (backend đã hỗ trợ đối xứng: tài xế đọc/gửi tin `messages` không cần `carrierUserId`, có `notifications`).
- Lỗi ngoài phạm vi: Admin chưa có màn xem sự cố mà chủ hàng báo (`ShipmentIncidents`, trạng thái `Open`, chưa có quy trình xử lý). Thông báo cũ chưa bị dọn (chưa có job xóa). Admin chưa có màn xem ảnh/CCCD của hồ sơ KYC, `admin-web` chưa có nút Từ chối. Chưa có job xóa ảnh KYC theo `DeleteAfter`.

## 15. Tiến độ và bàn giao giữa các phiên (app Cò xe)

Người dùng đưa bảng tính năng "CÒ XE" riêng (9 nhóm: Tài khoản, Nguồn hàng, Tìm xe, Mạng lưới, Giao dịch, Hoa hồng, Uy tín, Thông báo, Quản lý), yêu cầu làm 10 tính năng đầu trước. App Flutter là `C:\Working\Vantaiviet-fontend\broker-mobile` (trước đó chỉ có 1 file `main.dart` là màn đăng nhập tĩnh, chưa có nền tảng Windows/test — đã `flutter create --platforms=windows .` để bổ sung).

### Đã xong (cập nhật 22/09/2026, đợt 1: tính năng 1–10)

- **Phát hiện quan trọng:** backend đã có sẵn hạ tầng cho môi giới đăng hộ đơn của khách ngoài app (`Shipment.IsExternalOrder/ExternalCustomerName/ExternalCustomerPhone`, `ShipmentRules.CanPostAsync` đã cho role BROKER đăng đơn ngoài). Vì vậy các tính năng 4–8 ("Nguồn hàng") hầu như không cần sửa backend — chỉ cần app Flutter gọi lại đúng API `/api/shipments` sẵn có (tạo, sửa, `delete` khi Draft, `publish`, `cancel`, `repost`, `copy`) với `isExternalOrder: true`.
- **Tính năng 1 (Đăng ký/đăng nhập):** dùng lại xác thực số điện thoại + mật khẩu chung của hệ thống (như app Chủ hàng), không dựng OTP riêng như bảng ghi "SĐT + OTP" — tránh phân mảnh cơ chế đăng nhập đã có, kèm quản lý phiên (`GET/DELETE /api/auth/sessions`, màn `account/devices_screen.dart`).
- **Tính năng 2 (Hồ sơ cò xe):** entity mới `BrokerProfile` (`OperatingArea`, `YearsExperience`), `GET/PUT /api/broker/profile` (`[Authorize(Roles="Broker")]`), mirror đúng pattern `ShipperBusinessProfile`/`BusinessProfileService`. "Tên" dùng lại `DisplayName` có sẵn của tài khoản, không tạo trường trùng.
- **Tính năng 3 (Xác minh danh tính):** bảng ghi rõ "ở giai đoạn sau" — bỏ qua đợt này theo đúng yêu cầu.
- **Tính năng 4–8 (Nguồn hàng — đăng/sửa/xóa/đăng lại/sao chép/quản lý trạng thái):** tái dùng nguyên vẹn API `/api/shipments/*` của app Chủ hàng, không đổi backend. App có `shipment/shipment_form_screen.dart` (thêm 2 ô bắt buộc: tên và số điện thoại khách hàng thật), `shipment_detail_screen.dart`, `shipment_list_tab.dart`, `shipment_list_store.dart` — chuyển thể trực tiếp từ `shipper-mobile`, bỏ ảnh/định vị bản đồ/địa chỉ đã lưu (không thuộc 10 tính năng này). "Xóa" chỉ áp dụng cho bản nháp (dùng lại `DELETE /api/shipments/{id}` sẵn có, giới hạn Draft).
- **Tính năng 9–10 (Tìm xe — danh sách xe đang tìm hàng, tìm theo tuyến):** hoàn toàn mới, chưa có trước đó. Entity `VehicleAvailability` (tài xế đăng: xe, điểm đi/đến, thời gian rảnh, hạn tin tối đa 30 ngày). `POST/GET/DELETE /api/driver/vehicle-availability` (tài xế đăng/xem của mình/rút, cần `DriverProfile` và `Vehicle` đã `Approved`, mirror `BookingService.EligibleAsync`), `GET /api/broker/vehicle-availability?fromArea=&toArea=&vehicleType=` (môi giới tìm, ILIKE khớp một phần địa chỉ, chỉ trả tin `Open` và chưa hết hạn). Migration `AddBrokerFeatures`. Chưa có giao diện tài xế đăng tin (app `driver-mobile` chưa làm tới) — test dùng thẳng API qua `LiveDriver.postAvailability()` giống cách `live_support.dart` của app Chủ hàng giả lập tài xế.
- API mới đầy đủ: xem `Vantaiviet-fontend/shared/api-contracts.md` mục Broker.
- App Flutter: bộ khung mới hoàn toàn (`main.dart`, `home_page.dart`, `home_tab.dart`, `auth/*`, `broker/*`, `vehicles/*`, `shipment/*`, `account/*`), phần lớn cổng thẳng từ `shipper-mobile` (đã kiểm tra không có logic riêng cho chủ hàng) rồi bỏ bớt phần chưa cần (ảnh, geocoding, địa chỉ đã lưu, chat/tracking/đánh giá — nằm ở nhóm tính năng khác chưa tới lượt). 4 tab: Trang chủ, Nguồn hàng, Tìm xe, Tài khoản.
- Test: backend `BrokerFeaturesApiTests.cs` (8 test, dùng `ShipmentTestKit.ApprovedDriverAsync()`/`BrokerAsync()` có sẵn) — 86/86 cùng bộ cũ (78 cũ + 8 mới). App: `test/auth_units_test.dart`, `test/broker_units_test.dart`, `test/shipment_units_test.dart` (unit), `test/widget/*` (4 file, 17 test) — 30/30. Live: `test/live/live_broker_test.dart` (`BROKER_LIVE_API`/`BROKER_LIVE_ADMIN_PHONE`/`BROKER_LIVE_ADMIN_PASSWORD`, dùng chung `live_support.dart` kiểu app Chủ hàng nhưng thêm `LiveDriver.postAvailability`) — 4/4 trên DB tạm. `flutter analyze` sạch, `flutter build windows --debug` build được.

### Đợt 2, tính năng 11–20 — BACKEND + FLUTTER + TEST ĐÃ XONG (cập nhật 22/09/2026, phiên thứ hai trong ngày)

Người dùng gõ "làm tiếp 10 tính năng tiếp theo" ngay sau đợt 1 (phiên đầu, chỉ làm backend). Phiên thứ hai cùng ngày làm nốt toàn bộ giao diện Flutter, test (unit/widget/live) và tài liệu API cho đợt 2 — xem "Đã xong ở phiên hai" ngay dưới phần mô tả backend.

- **Tính năng 11 (Lọc xe):** `GET /api/broker/vehicle-availability` có thêm `minPayloadKg=` (lọc tải trọng tối thiểu, so với `Vehicle.MaxPayloadKg`) bên cạnh `vehicleType=` đã có ở đợt 1.
- **Tính năng 12 (Xe rỗng chiều về):** `VehicleAvailability` có thêm cột `IsReturnTrip` (tài xế tự đánh dấu khi đăng tin, `POST /api/driver/vehicle-availability` nhận `isReturnTrip`). Kết quả tìm kiếm của cò luôn ưu tiên tin `IsReturnTrip=true` lên đầu (`OrderByDescending(IsReturnTrip)` rồi mới đến mới nhất); có thêm cờ lọc `returnTripOnly=true` để chỉ xem loại này. `Status` của `VehicleAvailability` có thêm giá trị `Matched` (dùng ở tính năng 20).
- **Tính năng 13 + 18 (Gửi đề nghị chuyến + Báo giá chuyến):** gộp làm một hành động — cò đưa giá cho một tin đăng xe còn `Open`: `POST /api/shipments/{shipmentId}/proposals` `{vehicleAvailabilityId, proposedPrice}` (entity mới `VehicleProposal`, trạng thái `Pending`). Chỉ người quản lý được đơn (`CanDispatchShipment` — chủ đơn hoặc cò được giao và đã `Accepted`, dùng lại đúng hàm SQL mà `BookingService` dùng) mới gửi được; đơn phải `Published`; mỗi tin đăng xe chỉ có một đề nghị "còn sống" (`Pending`/`CounterOffered`) tại một thời điểm. `GET /api/shipments/{shipmentId}/proposals` xem các đề nghị của một đơn.
- **Tính năng 19 (Thương lượng giá):** tài xế được phản hồi **đúng một lần**: `POST /api/driver/vehicle-proposals/{id}/counter {counterPrice, version}` (chỉ từ trạng thái `Pending`, chuyển sang `CounterOffered`; phản hồi lần hai bị từ chối — đây là giới hạn cố ý để tránh làm cả một luồng đàm phán nhiều vòng, đã báo cho người dùng). Cò xem trả lời qua `POST /api/broker/vehicle-proposals/{id}/accept-counter {version}` (chốt theo giá tài xế trả) hoặc `POST .../withdraw {version}` (rút đề nghị). Tài xế cũng có thể `POST /api/driver/vehicle-proposals/{id}/reject {version}` thẳng từ đầu.
- **Tính năng 20 (Xác nhận giao chuyến — "tài xế nhận đơn"):** tài xế chấp nhận giá gốc của cò (`POST /api/driver/vehicle-proposals/{id}/accept {version}`, chỉ từ `Pending`) hoặc cò chấp nhận giá tài xế trả (`accept-counter`, chỉ từ `CounterOffered`) đều dẫn tới **đặt chuyến ngay lập tức**: service tạo thẳng một `TransportRequest` ở trạng thái `Accepted` (bỏ qua `Pending`, vì đàm phán đã là sự đồng thuận), rồi tạo `Booking`+`Trip` y hệt luồng tài xế tự xin đơn được duyệt — nhờ vậy `GET .../takers`, chat, theo dõi chuyến đều hoạt động bình thường không cần sửa gì thêm. Đơn chuyển `Booked`, **`Shipment.ProposedPrice` được cập nhật thành giá cuối cùng đã chốt** (không phải giá cò đưa ra ban đầu nếu tài xế trả giá khác). Tin đăng xe chuyển `Matched`; mọi đề nghị "còn sống" khác trên CÙNG tin đăng đó tự động bị `Rejected` (giống cách các yêu cầu vận chuyển khác bị từ chối khi một cái được duyệt). Có khóa dòng (`FOR UPDATE`/`FOR SHARE`) và các điều kiện tương tự `BookingService.AcceptAsync` (tài xế/xe phải còn `Approved`, không đang chạy chuyến khác → `resource_unavailable`).
- **Tính năng 14–15 (Danh sách tài xế, Tài xế yêu thích):** entity mới `SavedDriver` (`BrokerUserId`, `DriverUserId` — duy nhất một cặp, `GroupId?`, `IsFavorite`, `Note?`). `POST/GET/PUT/DELETE /api/broker/drivers` (`GET` nhận `groupId=`/`favoritesOnly=`, sắp xếp mục yêu thích lên đầu). Cần biết `DriverUserId` trước (lấy từ kết quả tìm xe ở tính năng 9–10); chưa có màn tìm tài xế riêng.
- **Tính năng 16 (Nhóm tài xế riêng):** entity mới `DriverGroup` (`BrokerUserId`, `Name`, không trùng tên không phân biệt hoa thường trong cùng một cò — kiểm tra ở tầng service như `SavedAddress`, không dùng ràng buộc DB). `POST/GET/PUT/DELETE /api/broker/driver-groups`. Xóa nhóm chỉ gỡ `GroupId` của các tài xế trong đó (không xóa `SavedDriver`).
- **Tính năng 17 (Mời tài xế vào app):** `GET /api/broker/invite` trả `{code}` — code hiện **chính là `UserId` của cò** (dạng hex 32 ký tự, không sinh mã ngắn riêng, không cần lưu trữ thêm — đơn giản hóa có chủ đích, nêu trong "Còn treo"). Đăng ký (`POST /api/auth/register`) nhận thêm `referralCode` (tùy chọn); nếu `accountType=Driver` và mã hợp lệ trỏ tới một tài khoản có role Broker, `User.ReferredByUserId` được gán và tài xế mới tự động được thêm vào `SavedDrivers` của cò đó. Mã sai hoặc không phải Broker bị **bỏ qua âm thầm**, không chặn đăng ký. `GET /api/broker/referrals` liệt kê tài xế đã đăng ký qua mã của mình.
- Migration `AddBrokerNetworkAndProposals` (bảng `VehicleProposals`, `SavedDrivers`, `DriverGroups`; cột `VehicleAvailabilities.IsReturnTrip`, `Users.ReferredByUserId`; check constraint `Status` của `VehicleAvailabilities` mở rộng thêm `Matched`). Đã áp cho `vantaiviet_core_test` và (phiên hai) cho `vantaiviet_core` (DB dev) — API dev dùng được tính năng này ngay.
- `ResultResponses.cs` (helper dùng chung): thêm `already_saved`, `duplicate_name`, `resource_unavailable` vào nhóm lỗi trả 409 (trước đó những mã lỗi tương tự đã có sẵn 409 nhưng ba mã này thì chưa, mặc dù `booking.resource_unavailable` đã trả 409 từ trước qua một switch riêng trong `BookingsController` không dùng chung helper này — không đụng tới file đó).
- Service mới: `VehicleProposalService`/`IVehicleProposalService` (`Services/VehicleProposalService.cs`), `DriverNetworkService`/`IDriverNetworkService` (`Services/DriverNetworkService.cs`, gồm nhóm + tài xế đã lưu + mời). Controller mới: `VehicleProposalControllers.cs` (2 controller: cò và tài xế), `DriverNetworkController.cs`. Đã đăng ký DbSet (`VehicleProposals`, `SavedDrivers`, `DriverGroups`) và DI.
- API đầy đủ: xem `Vantaiviet-fontend/shared/api-contracts.md` mục Broker — cập nhật đầy đủ cho đợt 2 (vehicle availability, vehicle proposals, driver network).
- Test backend: `BrokerAdvancedApiTests.cs` (11 test — lọc theo tải trọng/ưu tiên chiều về, cả luồng đề nghị/thương lượng/chấp nhận, từ chối/rút, một tin đăng chỉ có một đề nghị sống, chấp nhận một đề nghị làm các đề nghị khác trên cùng tin đăng tự rớt, chỉ đúng người quản lý đơn/đúng tài xế mới thao tác được, cò được giao đơn (không phải chủ) cũng đề nghị được, lưu/yêu thích/nhóm tài xế, mời tài xế qua mã, mã mời sai không chặn đăng ký) — **11/11 đạt**. Bộ đầy đủ backend (97 test = 86 cũ + 11 mới) đạt khi chạy riêng; ba test DB cũ (đã biết từ trước, xem "Bẫy khi làm việc trên máy này") có thể FAIL ngẫu nhiên khi chạy cả bộ, không liên quan tới thay đổi đợt này.

**Đã xong ở phiên hai cùng ngày — Flutter, test, tài liệu và migration dev cho đợt 2:**

- Giao diện: không thêm tab mới. Tính năng 11 (lọc tải trọng) và 12 (xe rỗng chiều về, ưu tiên lên đầu) vào thẳng `vehicles/vehicle_search_tab.dart` (ô lọc tải trọng + `FilterChip` "Xe rỗng chiều về", badge trên thẻ xe). Tính năng 13+18 (gửi đề nghị) là nút "Gửi đề nghị" trên mỗi thẻ xe ở tab Tìm xe (`vehicles/propose_dialog.dart` — chọn một đơn `Published` của chính mình rồi nhập giá); tính năng 19-20 (xem/thương lượng/chốt/rút đề nghị) là một mục "Đề nghị giá đã gửi" ngay trong `shipment/shipment_detail_screen.dart` của đơn đó (không cần màn riêng). Tính năng 14-16 (lưu tài xế/yêu thích/nhóm) là nút "Lưu tài xế" trên thẻ xe (`vehicles/save_driver_dialog.dart`) cộng hai màn mới trong Tài khoản: `network/saved_drivers_screen.dart` (lọc theo Tất cả/Yêu thích/nhóm, sửa nhóm+ghi chú, xóa) và `network/driver_groups_screen.dart` (tạo/sửa/xóa nhóm, mở từ nút trên AppBar của màn tài xế đã lưu). Tính năng 17 (mời tài xế) là `network/invite_screen.dart` (mã mời + nút sao chép + danh sách đã mời qua mã), mở từ Tài khoản. File mới: `vehicles/vehicle_proposal_models.dart`, `vehicles/vehicle_proposal_repository.dart`, `network/driver_network_models.dart`, `network/driver_network_repository.dart`.
- **Bẫy đã gặp:** `VehicleProposalService.WithdrawAsync` trả về `ShipmentResult<bool>` (chỉ `true`), khác với `accept`/`accept-counter`/`reject`/`counter` đều trả nguyên `VehicleProposalResponse` — viết `VehicleProposalRepository.withdraw` trả `Future<void>` (không parse JSON thành `VehicleProposal`) thì mới đúng; lần đầu viết nhầm parse như các hàm kia, chỉ phát hiện ra khi chạy live test thật (unit test dùng mock JSON tự bịa nên không bắt được lỗi này — nhớ khớp response thật của server, không chỉ khớp interface mình muốn).
- **Bẫy khác:** Flutter 3.47.5 (bản rất mới) đã seal `RadioListTile`/`Radio`'s `groupValue`/`onChanged` là deprecated, phải dùng `RadioGroup<T>` bọc quanh nhóm các `RadioListTile` (chỉ còn `value` trên mỗi item). Ngoài ra, nhét `ListView(shrinkWrap: true)` bên trong `SizedBox` chiều cao cố định (định làm danh sách đơn hàng cuộn riêng trong hộp thoại "Gửi đề nghị") làm `pumpAndSettle` treo vô hạn kèm assertion nội bộ `!semantics.parentDataDirty` của framework — đổi sang `Column` thường bên trong `SingleChildScrollView` sẵn có của `AlertDialog` (giống cách các hộp thoại khác trong app đã làm) thì hết treo.
- Test: `test/vehicle_proposal_units_test.dart`, `test/driver_network_units_test.dart` (unit), thêm 4 test vào `test/broker_units_test.dart` cho bộ lọc tải trọng/chiều về; `test/widget/vehicle_search_batch2_test.dart`, `test/widget/proposal_flow_test.dart`, `test/widget/driver_network_test.dart` (widget, 14 test — test sao chép mã mời cần tự mock kênh `SystemChannels.platform` vì `flutter_test` không có sẵn mock cho `Clipboard.setData`). Tổng bộ Flutter broker-mobile: 60 test unit+widget đạt (30 cũ + 30 mới), `flutter analyze` sạch, `flutter build windows --debug` build được. Live: mở rộng `test/live/live_broker_test.dart` thêm nhóm "broker features 11-20" (5 test) và `test/live/live_support.dart` (`LiveDriver.postAvailability` nhận `isReturnTrip`, thêm `counterProposal`/`acceptProposal`/`rejectProposal`; `newLiveDriver` nhận `referralCode` tùy chọn) — cả 9 live test (4 đợt 1 + 5 đợt 2) đạt trên DB tạm thật.
- Đã áp migration `AddBrokerNetworkAndProposals` cho `vantaiviet_core` (DB dev) và cập nhật `shared/api-contracts.md` mục Broker đầy đủ cho đợt 2.

### Đợt 3, nhóm Hoa hồng + Uy tín + một phần Thông báo/Quản lý — cập nhật 22/09/2026, phiên thứ ba (sau khi người dùng gửi lại nguyên văn bảng 35 dòng gốc)

Người dùng gõ "làm tiếp đợt 3" rồi (sau khi hết hạn mức, tiếp tục phiên) gửi lại **nguyên văn bảng tính năng gốc** (xem bảng đầy đủ ở mục "Bảng tính năng gốc và tình trạng" bên dưới) — lần đầu tiên có bảng thật thay vì mô tả tóm tắt, nên có thể đối chiếu chính xác từng dòng thay vì đoán.

- **Hoa hồng (thiết lập/theo dõi/thống kê):** `Shipment` có thêm cột `BrokerCommissionRate` (decimal 0-100, **phần trăm** trên `ProposedPrice` — bảng gốc cho ví dụ "đơn 8 triệu, hoa hồng 300k" gợi ý số tiền cố định, nhưng chọn tỉ lệ % linh hoạt hơn và vẫn tính đúng bản chất, tự quyết có disclose). Đặt khi tạo/sửa đơn (đi cùng nhóm `Quantity` — gửi `quantity` là gửi lại toàn bộ "chi tiết thương mại" gồm cả trường này, giống `ProposedPrice`/`Notes`), giữ nguyên khi đăng lại/sao chép. `GET/POST /api/broker/revenue` + `/summary` (chỉ đơn `IsExternalOrder` của chính cò, đã `Booked`/`Completed`, có đặt `BrokerCommissionRate`) — hoa hồng = `ProposedPrice × BrokerCommissionRate ÷ 100`, tính từ hai giá trị đã lưu sẵn trên dòng đó (không suy ngược theo bảng giá hiện tại). Màn Flutter `broker/broker_revenue_screen.dart` (mở từ Tài khoản → "Doanh thu & hoa hồng"): tổng số chuyến/doanh thu/hoa hồng + danh sách từng chuyến. **Còn thiếu:** thống kê theo ngày/tháng (bảng gốc "Thống kê doanh thu... theo ngày/tháng") — hiện chỉ có tổng all-time.
- **Uy tín (đánh giá tài xế, điểm uy tín cò xe, báo cáo tài xế):**
  - Đánh giá tài xế: **không cần sửa backend** — hệ thống đánh giá chung (`TripReview`, `/api/shipments/{id}/reviews`) vốn đã cho phép chủ đơn đánh giá cả tài xế lẫn "cò giữ đơn" rồi (dùng chung với app Chủ hàng). Chỉ thêm giao diện: cổng `review_section.dart`/`stars.dart` từ `shipper-mobile` sang `finish/` (bỏ phần `OrderServices` facade, nhận thẳng `ShipmentReviewRepository`), gắn vào `shipment_detail_screen.dart` khi đơn `Completed`.
  - Điểm uy tín cò xe: **không có endpoint/bảng riêng** — `BrokerProfileResponse` có thêm `rating: { average, count }` tính trực tiếp từ `TripReviews` nơi `TargetUserId` = chính cò (tức là feed từ đúng luồng "chủ hàng đánh giá cò giữ đơn" ở trên). Chỉ hiện khi đã có hồ sơ `BrokerProfile` (chưa điền hồ sơ thì chưa thấy điểm, kể cả nếu có review — giới hạn nhỏ, disclose). Hiện ở `broker_profile_screen.dart`.
  - Báo cáo tài xế: entity mới `DriverReport` (`ReporterUserId`, `DriverUserId`, `Category` ∈ `NoShow|Misconduct|UnsafeDriving|Other`, `Description` 3-1000 ký tự). `POST/GET /api/broker/drivers/{id}/reports`, `/drivers/reports/mine`. **Chưa có** hàng đợi duyệt cho admin (giống ghi chú cũ về `ShipmentIncidents` chưa có màn xử lý) — chỉ lưu lại, disclose. Giao diện: nút "Báo cáo tài xế" trong `network/saved_drivers_screen.dart` (`network/driver_report_dialog.dart`).
- **Thông báo (một phần):** thêm tab "Thông báo" thứ 5 (`finish/notifications_tab.dart` + `notification_store.dart`, cổng gần như nguyên văn từ `shipper-mobile`, poll 30 giây) — dùng lại nguyên `/api/notifications*` sẵn có, không đổi backend cho riêng phần này.
  - "Tài xế nhận/từ chối" (đã làm): thêm 6 loại thông báo mới `vehicle_proposal.received|countered|accepted|rejected|withdrawn` (bắn ở đúng các bước tương ứng trong `VehicleProposalService`) và `saved_driver.available` (bắn khi một tài xế **đã được cò lưu** đăng tin xe rảnh mới — `NotificationEvents.RaiseAsync` phải sửa `shipmentId` thành `Guid?` và thêm tham số `subjectOverride` để hỗ trợ thông báo không gắn với đơn hàng nào).
  - "Chuyến thay đổi" (đã làm một phần): dùng lại nguyên `trip.*` đã có — hoạt động ngay cho đơn cò tự nguồn (cò = `OwnerUserId` nên vốn đã nhận thông báo khi tài xế start/pickup/deliver). **Chưa nối** cho trường hợp cò được giao/dispatch đơn của người khác (`TripParticipant` không phải `OwnerUserId`) — `TripService.ChangeAsync` chỉ báo cho owner hoặc driver, chưa báo cho participant khác; đây thuộc nhánh "cò nhận đơn hộ chủ hàng" nói chung chưa có giao diện (xem "Còn treo").
  - **"Có hàng phù hợp" (đơn trên tuyến cò quan tâm) — CHƯA LÀM.** Không có khái niệm "tuyến cò quan tâm" để lưu, cũng không có thông báo khi có đơn mới khớp. "Có xe phù hợp" chỉ làm được một góc hẹp (tài xế đã lưu đăng tin) chứ chưa phải "mọi xe khớp tiêu chí tìm kiếm nói chung".
- **Quản lý (một phần):** `GET /api/shipments/mine/frequent-routes?take=` (nhóm theo đúng cặp địa chỉ, chỉ trả tuyến đăng **từ 2 lần trở lên** — tuyến mới đăng 1 lần chưa gọi là "thường dùng", tự quyết định filter `Count()>1` sau khi viết test phát hiện thiếu lọc). Màn `management/management_screen.dart` (mở từ Tài khoản → "Quản lý"): số đếm theo trạng thái đơn (dùng lại `/api/shipments/mine/summary`) + danh sách tuyến thường dùng. **Chưa làm:** "tỷ lệ tìm được xe" (bảng gốc mục Thống kê) và một dashboard tổng hợp thật sự (hiện chỉ là số đếm + danh sách, không phải biểu đồ/dashboard).
- Migration `AddBrokerRevenueReputationAndReports` (cột `Shipments.BrokerCommissionRate`; bảng `DriverReports`). Áp cho cả `vantaiviet_core_test` và `vantaiviet_core` (DB dev).
- **Bẫy đã gặp (đáng chú ý cho phiên sau):**
  - Query LINQ chiếu thẳng vào `record` (không phải anonymous type) rồi `OrderBy`/`Take` tiếp — EF Core không dịch được, ném `InvalidOperationException` bị `GlobalExceptionHandler` chuyển thành lỗi chung `business.rule_violated` (400) không rõ nguyên nhân. Sửa bằng cách chiếu vào anonymous type trước, `ToListAsync()`, rồi mới `Select` sang DTO trong bộ nhớ — đúng pattern `ShipmentService.ListAsync` đã dùng sẵn (bài học: khi thêm `GroupBy`/`OrderBy` phức tạp, luôn chiếu anonymous type trước khi vật chất hóa, đừng chiếu thẳng vào DTO).
  - Viết live test mô phỏng "chủ hàng" bằng `ApiAuthRepository.register()` của **broker-mobile** — nhưng repo đó **luôn gửi cứng `accountType: 'Broker'`** (đặc thù app Cò xe), nên "chủ hàng" giả lập thực ra lại là một cò khác, làm sai toàn bộ luồng gán/duyệt đơn. Phải tự gọi thẳng `POST /api/auth/register` với `accountType: 'Shipper'` qua `ApiClient` trần, giống cách `LiveDriver` mô phỏng tài xế — không dùng repository có sẵn của app khi mô phỏng một vai trò khác vai trò chính của app đó.
  - Khi một luồng đặt chuyến trực tiếp (không qua đề nghị giá) cần cả tài xế lẫn cò, dễ nhầm gọi API bằng **sai token** (vd. gọi `POST /api/shipments/{id}/requests` bằng token của cò thay vì tài xế) — lỗi trả về nhìn giống lỗi nghiệp vụ thật (`booking.conflict`/403) nên dễ tưởng backend sai; luôn kiểm tra lại đúng actor trước khi nghi ngờ endpoint. Nhiều test đặt chuyến trong cùng nhóm cũng nên dùng **tài xế riêng cho từng test** thay vì dùng chung một tài xế của `setUpAll` — tài xế dùng chung đã bận chuyến (Assigned) từ test trước sẽ làm test sau lỗi khó hiểu.
- Test: `BrokerManagementApiTests.cs` (9 test backend — hoa hồng qua đặt trực tiếp lẫn qua đề nghị giá, đơn chưa đặt hoa hồng/còn nháp không lên danh sách, điểm uy tín cò xe phản ánh đánh giá thật, báo cáo tài xế + validate, tuyến thường dùng lọc đúng >1 lần, thông báo bắn đúng ở từng bước đề nghị giá) — **9/9 đạt**, cả bộ backend 106/106 khi chạy riêng. Flutter: `test/widget/notifications_test.dart`, `reputation_and_reports_test.dart`, `revenue_and_management_test.dart` (15 test) — tổng bộ unit+widget broker-mobile **75/75**. Live: mở rộng `live_broker_test.dart` thêm nhóm "broker features 21-25" (4 test: hoa hồng, báo cáo tài xế, tuyến thường dùng, điểm uy tín) — **13/13** (4+5+4) trên DB tạm thật; `live_support.dart` không đổi (không cần thêm tiện ích riêng, tự đăng ký "chủ hàng"/"tài xế" ngay trong test).
- Đã cập nhật `shared/api-contracts.md` mục Broker: `brokerCommissionRate`, `GET/POST /api/broker/revenue*`, `rating` trên `BrokerProfileResponse`, `driver reports`, `frequent-routes`, các loại thông báo mới.

### Đợt 4, Theo dõi chuyến + Lịch sử chuyến — cập nhật 22/09/2026, phiên thứ tư

Người dùng gõ "tiếp tục hoàn thiện chức năng cò xe". Khi bắt đầu phiên, phần Flutter của hai tính năng này (`lib/tracking/*`, `lib/finish/history_screens.dart`, `finish_repositories.dart`'s `ShipmentHistoryRepository`, wiring vào `shipment_detail_screen.dart`/`home_page.dart`/`main.dart`, và fakes/harness trong `test/`) **đã có sẵn trong cây nguồn** — dấu mốc thời gian sửa file (~21:54-22:04, sau đợt 3 lúc ~17:52) cho thấy một phiên trước đó đã viết xong nhưng **chưa kịp ghi lại vào bộ nhớ/AGENTS.md**, chưa viết test, chưa chạy `flutter analyze`/`build`/live test. Phiên này xác minh lại toàn bộ và bổ sung phần còn thiếu:

- **Không cần sửa backend gì cả** — `/api/shipments/{id}/tracking` (`ShipmentProgressService`) và `/api/shipments/{id}/history` (`ShipmentHistoryService`) đều dùng chung hàm SQL `CanDispatchShipment` (chủ đơn hoặc cò được giao đã `Accepted`) nên tự động đúng quyền cho cò xe mà không cần thay đổi gì — đã đọc lại service để xác nhận trước khi tin vào việc "chưa test = chưa chắc đúng".
- **Flutter đã có sẵn (đã kiểm tra, không sửa logic):** `tracking/tracking_models.dart` (`ShipmentTracking`/`ShipmentTrip`/`TrackingCarrier`/`GpsPoint`, bản của cò bỏ `pickup`/`delivery` GeoPoint so với bản `shipper-mobile` — đơn giản hóa không quan trọng vì cò không cần xem lại tọa độ đơn mình đăng), `tracking/tracking_repository.dart`, `tracking/tracking_screen.dart` (nút "Theo dõi chuyến" trong `shipment_detail_screen.dart`, không có nút "Báo sự cố" như bản Chủ hàng — cò xe chưa có `finish/incident_screen.dart`, ngoài phạm vi bảng 35 dòng), `finish/history_screens.dart` (`ShipmentHistoryScreen` xem nhật ký một đơn qua nút "Nhật ký đơn hàng", `OrderHistoryScreen` xem toàn bộ đơn `Completed`/`Cancelled` qua nút lịch sử ở tab Nguồn hàng).
- **Bổ sung trong phiên này:** `test/widget/tracking_and_history_test.dart` (7 test — các bước hành trình + xác nhận hoàn thành, vị trí xe khi đang chạy, đơn `Published` chưa ai nhận, nhật ký một đơn có nội dung đúng theo `historyText`, đơn chưa có gì ghi lại, danh sách "Lịch sử chuyến" gộp Completed+Cancelled và mở được một đơn, danh sách rỗng); mở rộng `test/live/live_broker_test.dart` thêm nhóm "theo dõi chuyến + lịch sử chuyến" (1 test full-flow: đăng đơn → tìm tài xế qua đề nghị giá → tài xế nhận → `start`/`pickup`/`deliver` có ảnh chứng minh → cò xác nhận hoàn thành qua `TrackingRepository.confirmComplete` → đọc lại nhật ký, kiểm tra đủ `shipment.created`→`shipment.publish`→`trip.start`→`trip.pickup`→`trip.deliver`→`trip.complete` đúng thứ tự).
- Kết quả: `flutter analyze` sạch, `flutter build windows --debug` build được, unit+widget broker-mobile **82/82** (75 cũ + 7 mới), live **14/14** (13 cũ + 1 mới) trên DB tạm `broker_live_<8 số>` dựng riêng cho phiên này rồi xóa sau khi xong (theo đúng quy trình ở [[reference-local-verification-recipe]] — API dev thật của người dùng không hề được động tới, không gửi email thật).
- Không có bẫy mới đáng ghi lại ngoài việc xác nhận: khi mở phiên mới và thấy file nguồn đã có sẵn một tính năng "được ghi trong bộ nhớ là chưa làm", luôn kiểm tra dấu mốc thời gian sửa file và đọc lại code trước khi cho là bộ nhớ đúng — bộ nhớ có thể trễ so với cây nguồn nếu phiên trước bị ngắt quãng (hết hạn mức, crash) trước khi kịp cập nhật tài liệu.

### Đợt 5, "Có hàng phù hợp" + thống kê doanh thu theo kỳ + "tỷ lệ tìm được xe" + "loại thùng" — cập nhật 22/09/2026, phiên thứ năm

Người dùng gõ "làm tiếp các phần còn lại", tức toàn bộ mục còn ✗/~ liên quan tới bốn dòng cuối cùng của bảng gốc chưa xong đầy đủ (trừ "Xác minh danh tính" đã chủ động bỏ từ đợt 1). Cả bốn đều làm full-stack (backend + Flutter + test backend/widget/live) trong một phiên.

- **"Có hàng phù hợp" (Thông báo, hoàn toàn mới):** entity `BrokerRouteInterest` (`BrokerUserId`, `FromArea`, `ToArea` — không có `Version`, chỉ tạo/xem/xóa, giống `DriverGroup`). `POST/GET/DELETE /api/broker/route-interests` (`[Authorize(Roles="Broker")]`, unique theo cặp from/to không phân biệt hoa thường trong cùng một cò — 409 `duplicate_route`, tối đa 20 — 409 `limit_reached`). Khi một đơn chuyển `Published` (trong `ShipmentService.TransitionAsync`, ngay trước khi gọi `SaveAsync` — giống cách `CancelAsync` bắn thông báo trước khi lưu), hệ thống lấy **toàn bộ** `BrokerRouteInterests` (không lọc SQL, so khớp substring không phân biệt hoa thường trong bộ nhớ bằng `string.Contains` — bảng này luôn nhỏ nên không cần tối ưu, tự quyết định để tránh rủi ro dịch LINQ sang ILIKE lồng nhau giữa hai bảng khác nhau) rồi bắn thông báo `cargo.available` (loại mới trong `NotificationTypes`) cho mọi cò có `FromArea`/`ToArea` đều là substring của `PickupAddress`/`DeliveryAddress` đơn đó — `NotificationEvents.RaiseAsync` tự loại trừ trường hợp người nhận trùng người đăng (không cần tự lọc chủ đơn). Giao diện: `network/route_interest_models.dart`/`_repository.dart`/`_screen.dart` (`RouteInterestsScreen`, mở từ Tài khoản → "Tuyến quan tâm"), và `finish_models.dart` thêm case `cargo.available` cho text/icon thông báo.
- **Thống kê doanh thu theo ngày/tháng (Hoa hồng):** `GET /api/broker/revenue/by-period?granularity=day|month&take=` (1-60, mặc định month/12; 400 `invalid_pagination` ngoài khoảng) trong `BrokerRevenueService.ByPeriodAsync` — **gộp nhóm trong bộ nhớ, không dùng EF `GroupBy`** (deal của một cò luôn ít, tránh hẳn rủi ro dịch LINQ đã gặp ở đợt 3), theo `Shipment.UpdatedAt` (đơn tính từ lúc `Booked`, không chỉ lúc `Completed`, giống logic doanh thu đã có). Giao diện: `broker_revenue_screen.dart` thêm hai `ChoiceChip` "Theo ngày"/"Theo tháng" và danh sách theo kỳ, tải riêng với danh sách chi tiết từng chuyến (đổi kỳ không ảnh hưởng phần trên).
- **"Tỷ lệ tìm được xe" (Quản lý → Thống kê):** thêm `FoundVehicleRate` (double?, phần trăm 0-100) vào `ShipmentSummaryResponse` sẵn có (`GET /api/shipments/mine/summary`, dùng chung với app Chủ hàng nên shipper-mobile cũng được luôn) — tính bằng (Booked+Completed) ÷ (Published+Booked+Completed+Cancelled), tức trong số đơn **đã từng gửi đi** (Draft không tính vì chưa ai thấy), bao nhiêu đã tìm được xe; `null` nếu chưa từng đăng đơn nào. Giao diện: `management_screen.dart` thêm thẻ tỷ lệ (key `found-vehicle-rate`/`found-vehicle-rate-empty`) ngay dưới hàng đếm trạng thái.
- **"Loại thùng" (Tìm xe → Lọc xe):** cột mới `Vehicle.BodyType` (string?, tự do như `VehicleType`, không ràng buộc DB, tài xế có thể bỏ trống) — set qua `POST/PUT /api/driver/vehicles` (`CreateVehicleRequest`/`UpdateVehicleRequest`/`VehicleResponse` đều thêm `bodyType` optional). `VehicleAvailabilityService.SearchAsync`/`PageAsync` thêm tham số `bodyType` (so khớp chính xác, giống `vehicleType`, join thêm `Vehicles.BodyType`). Giao diện: `vehicle_availability_models.dart` thêm `BodyTypeOption`/`bodyTypes` (3 gợi ý: Kín/Bạt/Lửng — tự chọn bộ giá trị, không trùng với các "loại xe" đã có sẵn như Container/Refrigerated/Tanker vốn đã nằm trong `vehicleTypes` từ đợt 1-2), thêm hàng `ChoiceChip` loại thùng thứ hai trong `vehicle_search_tab.dart` (dưới hàng loại xe) và hiện trên thẻ xe khi có.
- Migration `AddBrokerRouteInterestsAndVehicleBodyType` (bảng `BrokerRouteInterests`; cột `Vehicles.BodyType`). Áp cho cả `vantaiviet_core_test` và `vantaiviet_core` (DB dev).
- **Bẫy đã gặp:** `dotnet ef migrations add ... --no-build` rồi `dotnet ef database update ... --no-build` ngay sau đó (dùng lại đúng build vừa tạo) vẫn báo `PendingModelChangesWarning` bị ném thành lỗi cứng dù model không hề đổi giữa hai lệnh — nguyên nhân không rõ (có thể `--no-build` của `database update` không tái dùng chính xác artefact vừa build cho `migrations add`); bỏ `--no-build` khỏi `database update` (để nó tự rebuild, chậm hơn ~10 giây) thì hết — nếu gặp lại lỗi này ở đợt sau, thử bỏ `--no-build` trước khi nghi ngờ migration sai.
- Test: `BrokerRouteInterestAndStatsApiTests.cs` (5 test backend — validate/limit/duplicate tuyến quan tâm rồi xóa, thông báo đúng người xem/không tự thông báo chủ đơn, gộp doanh thu theo ngày/tháng đúng + từ chối granularity sai, tỷ lệ tìm được xe tính đúng qua Draft/Published/Booked/Cancelled, lọc xe theo loại thùng) — **5/5 đạt**, cả bộ backend 37/37 khi chạy riêng nhóm Broker (109/111 khi chạy toàn bộ 111 test của solution — 2 fail là `ShipperMigrationTests`/`OtpQueueDatabaseTests`, đúng hai trong ba test DB cũ đã biết ở "Bẫy khi làm việc trên máy này", không liên quan đợt này). Flutter: `test/widget/route_interests_and_stats_test.dart` (8 test) — tổng unit+widget broker-mobile **90/90** (82 cũ + 8 mới), `flutter analyze` sạch, `flutter build windows --debug` build được. Live: thêm nhóm "broker batch 5" vào `live_broker_test.dart` (4 test) — **18/18** (14 cũ + 4 mới) trên DB tạm `broker_live_<8 số>` dựng/xóa riêng cho phiên này.
- Đã cập nhật `shared/api-contracts.md`: `foundVehicleRate` trên shipment summary, `GET /api/broker/revenue/by-period`, `cargo.available` trong danh sách loại thông báo, `bodyType` trên `Vehicle`/vehicle-availability search+response, mục "Route interests" mới.
- **Sau đợt này, chỉ còn `✗` duy nhất trong bảng 35 dòng là "Xác minh danh tính"** (bảng gốc tự ghi "giai đoạn sau", chủ động bỏ từ đợt 1) — mọi dòng khác đã ✓ hoặc còn `~` vì lý do đã disclose (xem "Còn treo" cập nhật bên dưới).

### Bảng tính năng gốc và tình trạng (nguyên văn bảng người dùng gửi 22/09/2026, dùng làm nguồn tham chiếu duy nhất — đừng suy diễn lại từ mô tả tóm tắt)

✓ = đã xong, ~ = làm một phần/đơn giản hóa (xem đợt tương ứng ở trên), ✗ = chưa làm.

| Nhóm | Tính năng | Mô tả gốc | Tình trạng |
| --- | --- | --- | --- |
| Tài khoản | Đăng ký/đăng nhập | SĐT + OTP | ✓ (dùng SĐT+mật khẩu chung hệ thống, không dựng OTP riêng — đợt 1) |
| | Hồ sơ cò xe | Tên, khu vực hoạt động, kinh nghiệm | ✓ (đợt 1) |
| | Xác minh danh tính | CCCD/VNeID ở giai đoạn sau | ✗ (bảng tự ghi "giai đoạn sau" — chủ động bỏ qua) |
| Nguồn hàng | Đăng đơn hàng | Điểm lấy/trả, loại hàng, tải trọng, loại xe, thời gian | ✓ (đợt 1) |
| | Sửa/xóa đơn | Quản lý đơn đã đăng | ✓ (đợt 1, xóa chỉ áp dụng bản nháp) |
| | Đăng lại đơn | Đơn cũ có thể đăng lại nhanh | ✓ (đợt 1) |
| | Sao chép đơn | Phù hợp khi có nhiều chuyến giống nhau | ✓ (đợt 1) |
| | Quản lý trạng thái | Đang tìm xe → Có xe → Đang vận chuyển → Hoàn thành | ✓ (đợt 1) |
| Tìm xe | Danh sách xe đang tìm hàng | Xem xe phù hợp trong khu vực | ✓ (đợt 1) |
| | Tìm theo tuyến | Ví dụ Hải Phòng → Hà Nội | ✓ (đợt 1) |
| | Lọc xe | Container, tải trọng, loại thùng... | ✓ (đợt 2: lọc tải trọng `minPayloadKg` + loại xe; đợt 5: thêm `bodyType` "loại thùng" riêng — Kín/Bạt/Lửng) |
| | Xe rỗng chiều về | Ưu tiên tài xế đang cần hàng chiều về | ✓ (đợt 2) |
| | Gửi đề nghị chuyến | Gửi đơn trực tiếp cho tài xế | ✓ (đợt 2, gộp chung với "Báo giá chuyến") |
| Mạng lưới | Danh sách tài xế | Lưu những tài xế thường xuyên làm việc | ✓ (đợt 2) |
| | Tài xế yêu thích | Ưu tiên tài xế uy tín | ✓ (đợt 2) |
| | Nhóm tài xế riêng | Ví dụ nhóm container Hải Phòng | ✓ (đợt 2) |
| | Mời tài xế vào app | Link/mã giới thiệu | ✓ (đợt 2; mã hiện là `UserId` thô, chưa phải link/QR đẹp) |
| Giao dịch | Báo giá chuyến | Cò đưa mức giá cho tài xế | ✓ (đợt 2, gộp chung với "Gửi đề nghị chuyến") |
| | Thương lượng giá | Hai bên gửi/counter giá | ✓ (đợt 2; tài xế chỉ được trả giá **một lần**, không nhiều vòng — đã disclose) |
| | Xác nhận giao chuyến | Tài xế nhận đơn | ✓ (đợt 2) |
| | Theo dõi chuyến | Xem trạng thái vận chuyển | ✓ (đợt 4, cổng từ `shipper-mobile/lib/tracking/*`, không đổi backend) |
| | Lịch sử chuyến | Toàn bộ chuyến đã môi giới | ✓ (đợt 4, cổng từ `shipper-mobile/lib/finish/history_screens.dart`, không đổi backend) |
| Hoa hồng | Thiết lập hoa hồng | Ví dụ đơn 8 triệu, hoa hồng cò 300k | ✓ (đợt 3, làm dưới dạng % `BrokerCommissionRate` trên giá đơn thay vì số tiền cố định — tự quyết, disclose) |
| | Theo dõi hoa hồng | Biết từng chuyến kiếm được bao nhiêu | ✓ (đợt 3, `GET /api/broker/revenue`) |
| | Thống kê doanh thu | Theo ngày/tháng | ✓ (đợt 3: tổng all-time qua `GET /api/broker/revenue/summary`; đợt 5: `GET /api/broker/revenue/by-period?granularity=day|month`) |
| Uy tín | Đánh giá tài xế | Sau khi hoàn thành chuyến | ✓ (đợt 3, dùng lại hệ thống đánh giá chung, chỉ thêm giao diện) |
| | Điểm uy tín cò xe | Dựa trên lịch sử giao dịch | ✓ (đợt 3, tính từ `TripReviews` có sẵn, hiện trong hồ sơ cò) |
| | Báo cáo tài xế | Hủy chuyến, không đến nhận hàng... | ✓ (đợt 3, `DriverReport`; danh mục `NoShow/Misconduct/UnsafeDriving/Other` — "hủy chuyến" gần nhất rơi vào `Other`; **chưa có** hàng đợi admin duyệt) |
| Thông báo | Có xe phù hợp | Báo khi xuất hiện xe phù hợp đơn | ~ (đợt 3, chỉ báo khi **tài xế đã lưu** đăng tin mới — chưa phải "mọi xe khớp tiêu chí tìm kiếm" nói chung) |
| | Có hàng phù hợp | Báo khi có đơn trên tuyến cò quan tâm | ✓ (đợt 5, `BrokerRouteInterest` + thông báo `cargo.available` khi một đơn mới khớp tuyến đã lưu) |
| | Tài xế nhận/từ chối | Push notification | ✓ (đợt 3, qua các loại thông báo `vehicle_proposal.*`; là poll, không phải push thật) |
| | Chuyến thay đổi | Báo các thay đổi quan trọng | ~ (đợt 3, hoạt động cho đơn cò tự nguồn vì cò là chủ đơn; **chưa nối** cho trường hợp cò được giao đơn của người khác) |
| Quản lý | Dashboard | Đơn đang tìm xe, đang chạy, hoàn thành | ~ (đợt 3, màn "Quản lý" mới có số đếm theo trạng thái, chưa phải dashboard biểu đồ) |
| | Thống kê | Số đơn, tỷ lệ tìm được xe, doanh thu | ✓ (đợt 3: số đơn + doanh thu; đợt 5: `foundVehicleRate` trên `GET /api/shipments/mine/summary`) |
| | Tuyến thường dùng | Lưu tuyến để đăng đơn nhanh | ✓ (đợt 3, `GET /api/shipments/mine/frequent-routes`, chỉ tuyến đăng ≥2 lần) |

### Còn treo (app Cò xe, cập nhật sau đợt 5)

- **Việc còn thiếu, mọi dòng ✗/~ còn lại đều là đơn giản hóa đã disclose** (không còn dòng nào "hoàn toàn chưa làm" ngoài "Xác minh danh tính" chủ động bỏ từ đợt 1): "Có xe phù hợp" mới báo cho tài xế **đã lưu**, chưa phải mọi xe khớp tiêu chí tìm kiếm nói chung; "Chuyến thay đổi" chưa nối cho trường hợp cò được giao đơn người khác; "Dashboard" (Quản lý) mới là số đếm, chưa phải biểu đồ; "Báo cáo tài xế" chưa có hàng đợi admin duyệt.
- **Câu hỏi/đơn giản hóa cần xác nhận với người dùng khi có dịp** (đã tự quyết để không chặn tiến độ): (a) đàm phán giá chỉ cho tài xế trả giá **một lần**; (b) mã mời là `UserId` thô, chưa phải liên kết/QR đẹp; (c) "Thiết lập hoa hồng" làm theo **%** thay vì số tiền cố định như ví dụ trong bảng; (d) "Báo cáo tài xế" chưa có danh mục "hủy chuyến" riêng (dùng "Khác"), và chưa có hàng đợi cho admin xem/xử lý báo cáo; (e) "Có hàng phù hợp" bắn thông báo cho **mọi** đơn khớp tuyến kể cả đơn của shipper thường (không riêng gì đơn mà cò có thể "nhận hộ"), vì hiện chưa có cách để cò tự nhận một đơn không phải của mình — thuần thông báo để biết thị trường, không kèm hành động nào mới; (f) "loại thùng" chỉ có 3 gợi ý (Kín/Bạt/Lửng), là chip gợi ý chứ không phải danh sách cố định phía server (giống `vehicleType`).
- Giao diện tài xế đăng "xe đang tìm hàng" và trả lời đề nghị giá chưa có (thuộc `driver-mobile`, ngoài phạm vi app Cò xe); backend đã hỗ trợ đối xứng nên chỉ cần thêm màn hình khi tới lượt driver-mobile. Test mọi đợt đều giả lập tài xế (và từ đợt 3, cả chủ hàng) bằng cách gọi thẳng API (`LiveDriver`/`ApiClient` trần ở live test, `ApprovedDriverAsync`/`api.RegisterAsync()` ở test backend).
- "Cò nhận đơn hộ chủ hàng khác" (broker as a taker — `/api/shipments/{id}/broker`, `/api/broker-assignments/mine`) vẫn hoàn toàn chưa có giao diện trong `broker-mobile`; các test liên quan (đợt 2 "A_broker_assigned_to_someone_elses_shipment...", đợt 3 test điểm uy tín) chỉ gọi thẳng API để dựng bối cảnh, không phải tính năng đã làm cho người dùng cuối.
- Chưa build thử Android/iOS cho `broker-mobile`.

### Bẫy riêng của app Cò xe

- `broker-mobile` ban đầu thiếu `windows/`, `test/`, `analysis_options.yaml` (chưa từng `flutter create`). Chạy `flutter create --platforms=windows --project-name vantaiviet_broker --org com.vantaiviet .` để bổ sung mà không mất `lib/main.dart`/`pubspec.yaml` đã có — lệnh này giữ nguyên file đã tồn tại, chỉ thêm file thiếu.
- `pubspec.yaml` gốc thiếu hẳn mục `dev_dependencies` (không có `flutter_test`) — phải tự thêm mới `flutter test` chạy được.
- Repository môi giới dùng khóa lưu trữ an toàn riêng (`vantaiviet.broker.refreshToken`, khác `vantaiviet.shipper.refreshToken`) để không lẫn phiên đăng nhập giữa hai app trên cùng máy.

### Bẫy khi làm việc trên máy này

- API dev của người dùng chạy ở cổng 5101 và khóa DLL cấu hình Debug. Build/test bằng một cấu hình riêng, ví dụ `dotnet build src/VantaiViet.CoreApi -c Batch2` và `dotnet test tests/VantaiViet.CoreApi.Tests -c Batch2`. Không build file `.sln` với cấu hình này (solution không có cấu hình đó).
- PostgreSQL ở `localhost:4455`; connection string nằm trong `src/VantaiViet.CoreApi/appsettings.Development.json`. `dotnet ef` không đọc file đó (`CoreDbContextFactory` chỉ đọc user-secrets và biến môi trường, nếu không có thì cố ý trỏ vào cổng 1) nên phải truyền `--connection "..."`. Migration mới áp cho `vantaiviet_core_test` trước khi chạy test, và (nếu chỉ thêm) áp cho `vantaiviet_core` cho API dev.
- Test backend cần biến `CORE_DATABASE_TEST_CONNECTION` trỏ `vantaiviet_core_test`. `tests/.../ApiHostFixture.cs` dựng DB tạm, chạy toàn bộ migration và khởi động API thật trên cổng ngẫu nhiên. Test live của Flutter ở `test/live/*.dart` (đầu file ghi biến môi trường cần đặt).
- **Không chạy API ở Development với cấu hình mặc định:** user-secrets của người dùng có SMTP Gmail thật (`Email:Simulate=false`); đợt 1 đã lỡ gửi 1 email thật. Luôn đặt `Notifications__Email__Simulate=true` và `Notifications__Smtp__Host=smtp.invalid`, và `Notifications__WorkerEnabled=false` khi trỏ vào DB dev để không đụng hàng đợi OTP.
- Dịch vụ tìm địa chỉ (nominatim) không truy cập được trên máy này, nên tìm địa chỉ luôn hiện nhánh "nhập tay".
- Không chạy `dart format` lên cả `lib` của `shipper-mobile`: code cũ được viết tay (không qua formatter, không có git), formatter 80 cột sẽ xuống dòng lại hàng chục file và làm phát sinh cảnh báo `curly_braces_in_flow_control_structures`. Chỉ format file mới do mình tạo. Phiên 21/09/2026 đã lỡ làm vậy một lần (hành vi không đổi, đã dọn cảnh báo bằng `dart fix`).
- Ba test DB cũ (`ShipperMigrationTests`, `OtpQueueDatabaseTests`, `AuthSessionDatabaseTests`) có thể quá thời gian đọc Npgsql (30 giây) khi chạy trong bộ 86 test đầy đủ, kể cả không có gì khác chạy song song — có vẻ do tải PostgreSQL tích lũy qua nhiều test nặng, không phải lỗi thật. Chạy riêng (`--filter`) luôn đạt. 86/86 đạt khi chạy riêng từng test lẻ; chạy cả bộ có thể thấy 1 trong 3 test trên báo FAIL ngẫu nhiên, chạy lại (đôi khi 2 lần) thường qua.
- Widget của Flutter có `ListView` cuộn ngang (dòng chip lọc): nếu có nhiều chip hơn bề ngang cửa sổ test (900px), `find.byKey` không thấy các chip nằm ngoài viewport ban đầu vì `ListView` chỉ build phần tử gần vùng nhìn thấy (lazy). Chọn chip đầu danh sách khi viết test, hoặc cuộn tới bằng `dragUntilVisible` nếu cần chip cuối.
- Test live của Flutter: `find.text` cũng khớp chữ đang gõ trong ô nhập, nên khi chờ một tin nhắn hay báo cáo vừa gửi phải chờ phần tử khác (ví dụ danh sách tin, dòng "chưa có báo cáo" biến mất), và `tester.tap(find.byType(BackButton))` cần `.last` vì các màn bên dưới vẫn còn trong cây. Cuối test phải gọi `api.close()`.
- Test widget của Flutter: các tab của `HomePage` nằm trong `IndexedStack`, nên một chữ như tiêu đề đơn hay nhãn trạng thái có thể trùng giữa Trang chủ và tab Đơn hàng. Khi kiểm nội dung, giới hạn finder bằng `find.descendant(of: find.byType(HomeTab / ShipmentListTab), ...)`, và đặt tên đơn mẫu không trùng nhãn trạng thái ("Đã hủy", "Đã xong"...).
- Bash tool lỗi với heredoc dài chứa dấu nháy không cân: ghi script phụ trợ ra file bằng công cụ Write rồi chạy.
