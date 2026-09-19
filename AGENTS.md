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
- Không giới hạn số dòng máy móc. Thin được đánh giá bằng trách nhiệm, không phải việc giấu code trong private method.

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
