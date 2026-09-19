# GPS và bản đồ — bản thử nghiệm Development

Chạy lại CoreApi bằng profile http trong Visual Studio.

- Bản đồ: http://localhost:5101/tracking/index.html
- API: Swagger, nhóm Tracking.
- Đăng nhập bằng tài khoản hiện có; chọn chuyến trong danh sách hoặc nhập tripId.
- Trang này là công cụ test cùng CoreApi, chưa phải frontend Angular/app tài xế hoàn chỉnh.
- Trang tĩnh và gửi GPS giả lập chỉ khả dụng ở Development.

## Xem lịch sử trên trang test

- Nút Làm mới danh sách tải lại các chuyến được cấp quyền sau khi tạo booking.
- Mở Xem lại lịch sử GPS, kéo thanh chọn để xem tọa độ/thời gian của từng điểm đã tải.
- Điểm xem lại có màu tím; nhãn GIẢ LẬP vẫn được giữ cho dữ liệu mô phỏng.
- Tải thêm lịch sử để lấy trang dữ liệu kế tiếp. Về vị trí mới nhất thoát chế độ xem lại.
- Khi API trả 401, trang dừng GPS và kết nối theo dõi, yêu cầu đăng nhập lại; không xóa điểm chưa gửi trên thiết bị.

## Luồng

1. Tạo booking để có tripId. Chủ tin chọn tọa độ lấy/giao trên bản đồ khi chuyến còn Assigned.
2. Tài xế bắt đầu chuyến qua API Trips. Đăng nhập trên bản đồ bằng tài khoản tài xế.
3. Chọn Bật GPS trình duyệt và cho phép vị trí; gửi khoảng mỗi 10 giây.
4. Chủ hàng/môi giới đăng nhập trên thiết bị khác để xem cùng chuyến.
5. GPS được nhận ở InProgress/Loaded; dừng khi Delivered, Completed hoặc Cancelled.

Trình duyệt cần HTTPS hoặc localhost cho Geolocation. Mở bằng địa chỉ IP LAN qua HTTP không đủ.
Chưa triển khai GPS nền khi khóa màn hình; cần app điện thoại và quyền vị trí nền.

## Quyền xem

Tài xế và người đăng tin có quyền xem mặc định. Chủ tin cấp/thu hồi quyền xem thêm
cho tài khoản Shipper/Broker trong mục Người được xem chuyến hoặc API participants.
Người được cấp chỉ xem bản đồ, không nhận quyền xác nhận/hủy/hoàn tất chuyến.
Khách ngoài chưa có tài khoản chưa được xem. Đây là quyền xem, chưa thay thế quy trình
chủ hàng ủy quyền một môi giới nhận và điều phối đơn.

Mỗi lần lấy snapshot, lịch sử hoặc tuyến đều kiểm tra lại quyền và trạng thái tài khoản.
SignalR chỉ gửi thông báo cần tải lại; không phát tọa độ trực tiếp cho group.
Vì thế kết nối cũ sau thu hồi quyền không tiếp tục đọc được dữ liệu GPS.
Client polling 15 giây và đồng bộ lại khi reconnect; thông báo realtime không phải nguồn dữ liệu duy nhất.

## API

| API | Chức năng |
| --- | --- |
| GET /api/tracking/trips?page=1 | Chuyến có quyền xem, 20/trang |
| GET /api/tracking/{id} | Snapshot, vị trí mới nhất, stale và quyền thao tác |
| POST /api/tracking/{id}/locations | Tài xế gửi 1–100 điểm/lô |
| GET /api/tracking/{id}/locations?afterId=0 | Lịch sử theo cursor, 500 điểm/trang |
| PUT /api/tracking/{id}/stops | Chủ tin đặt tọa độ khi Assigned |
| GET /api/tracking/{id}/route | Tuyến tham khảo OSRM |
| GET /api/tracking/{id}/participants | Chủ tin xem danh sách được cấp |
| PUT hoặc DELETE /api/tracking/{id}/participants/{userId} | Cấp/thu hồi quyền xem |
| /hubs/tracking | SignalR: JoinTrip, LeaveTrip; sự kiện Refresh |

Body gửi GPS:

```json
{
  "points": [{
    "pointId": "GUID-MOI-CHO-MOI-LAN-DO",
    "latitude": 21.0285,
    "longitude": 105.8542,
    "accuracyMeters": 5,
    "recordedAt": "THOI-DIEM-UTC-THUC-TE",
    "simulated": false
  }]
}
```

- Giữ nguyên pointId và nội dung khi gửi lại. Trùng ID khác nội dung trả 409.
- GPS quá 24 giờ, trước lúc bắt đầu chuyến, quá 30 giây trong tương lai,
  tọa độ sai hoặc độ chính xác trên 1.000 m bị từ chối.
- Lưu riêng RecordedAt/ReceivedAt; điểm đến muộn không thay thế vị trí mới hơn.
- Sau 2 phút không có điểm mới, snapshot báo stale.
- Điểm queued chưa gửi được lưu trong localStorage theo tài khoản/chuyến, tối đa 1.000 điểm.
  Token chỉ giữ trong bộ nhớ. Đăng nhập lại cùng tài khoản để gửi tiếp.
- Sau khi chuyến ngừng theo dõi, server không nhận cả lô offline nữa; phải gửi hết trước khi báo giao hàng.
  Có nút xóa điểm chưa gửi; không xóa lịch sử server.
- Giả lập được gắn nhãn riêng. Dữ liệu GPS điện thoại không phải chứng cứ chống giả mạo vị trí.

## Bản đồ và tuyến đường

Leaflet 1.9.4 + MapLibre GL JS 5.6.1 dùng nền vector OpenFreeMap. Nền và thư viện cần kết nối mạng/CDN.
Không tải hàng loạt hoặc cache offline tiles. Trình duyệt dùng HTTP cache và có attribution.

Maps:RoutingBaseUrl trong appsettings.json trỏ tới OSRM demo để test.
Đây là tuyến ô tô tham khảo, chưa xét tải trọng/cầu/đường cấm xe tải.
Production cần chọn nhà cung cấp tile/routing phù hợp, quota và cấu hình endpoint riêng.
Đặt RoutingBaseUrl rỗng để tắt định tuyến; GPS vẫn hoạt động.
GPS xe không gửi tới OSRM; API định tuyến gửi tọa độ lấy/giao hàng.

Môi trường kiểm thử hiện không phân giải được tile.openstreetmap.org. Trang hiển thị lỗi
nền bản đồ thay vì giả rằng tải thành công. Kiểm tra DNS/mạng hoặc cấu hình nhà cung cấp
tiles trong tracking.js nếu cần. Chưa xác minh được hiển thị tiles trực tiếp trong môi trường này.

Tham chiếu chính thức:
- https://leafletjs.com/examples/quick-start/
- https://operations.osmfoundation.org/policies/tiles/
- https://project-osrm.org/docs/v5.24.0/api/

## Kiểm chứng

BookingDatabaseTests chạy database PostgreSQL riêng cho hai luồng chủ hàng/môi giới.
Bao phủ GPS trùng, khác nội dung, sai thời điểm/tọa độ, dữ liệu đến muộn, quyền gửi,
cấp/thu hồi người xem và dừng gửi sau giao hàng, cùng các test vòng đời chuyến.


## Cấu hình nguồn nền bản đồ

Sửa `src/VantaiViet.CoreApi/wwwroot/tracking/map-config.json`, tải lại trang hoặc bấm Thử lại.
- `type`: `vector` cho MapLibre style JSON hoặc `raster` cho mẫu URL `{z}/{x}/{y}`.
- `url`: URL HTTPS của nhà cung cấp; `name`, `maxZoom`, `attribution` theo nhà cung cấp.
- Đây là file công khai. Chỉ dùng browser key giới hạn domain nếu dịch vụ yêu cầu; không đặt secret backend ở đây.
- Không tự chuyển nhà cung cấp khi lỗi. Nút Thử lại tải lại cấu hình và lớp nền, giữ nguyên GPS.
- Tắt zoom animation Leaflet để đồng bộ lớp vector với các điểm GPS khi thay đổi zoom lớn.
- Tham chiếu tích hợp: https://openfreemap.org/quick_start/
