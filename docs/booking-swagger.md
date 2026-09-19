# Test nhận đơn trên Swagger

Chạy CoreApi bằng profile http trong Visual Studio, mở http://localhost:5101/swagger.
Đăng nhập rồi bấm Authorize, dán accessToken. Khi đổi tài khoản, thay token trong Authorize.

## Điều kiện

- Chủ hàng dùng role Shipper; môi giới dùng Broker. Chủ tin là người xác nhận.
- Tài xế dùng role Driver, AccountStatus = Active, hồ sơ và xe Approved.
- Bằng lái còn hạn đến ngày lấy hàng; xe thuộc tài xế và đủ tải trọng.
- Active hiện là trạng thái tài khoản được duyệt thủ công; chưa phải chứng nhận VNeID hoặc bảo hiểm.
- Mỗi tài xế/xe chỉ có một chuyến Assigned, InProgress, Loaded hoặc Delivered ở giai đoạn này.

## Thứ tự

1. Chủ hàng/môi giới tạo và publish tin qua nhóm Shipments.
   Đơn nhận ngoài hệ thống dùng tài khoản Broker và isExternalOrder = true.
2. Tài xế gọi POST /api/shipments/{shipmentId}/requests:

   ```json
   { "vehicleId": "GUID-XE-DA-DUYET" }
   ```

3. Chủ tin gọi GET /api/shipments/{shipmentId}/requests để xem ứng viên.
4. Chủ tin gọi POST /api/transport-requests/{id}/accept.
   Response chứa booking id, tripId và tripStatus = Assigned.
5. Hai bên xem GET /api/bookings/mine; tài xế xem GET /api/driver/requests.

Các lựa chọn khác:

- Chủ tin từ chối: POST /api/transport-requests/{id}/reject.
- Tài xế rút đề nghị: POST /api/transport-requests/{id}/withdraw.
- Danh sách phân trang bằng page, mỗi trang 20 dòng.

## Hành vi

- Một đơn chỉ có một booking. Xác nhận lại đúng đề nghị đã nhận trả booking cũ.
- Đề nghị còn lại bị từ chối sau khi xác nhận thành công.
- Tin đã Booked không thể hủy bằng API hủy tin.
- Xe/hồ sơ/tài khoản được kiểm tra lại khi xác nhận.
- Sai chủ tin trả 404; không đủ điều kiện trả 403; tranh chấp hoặc tài xế/xe đang bận trả 409.
- Khách ngoài hệ thống không cần tài khoản; thông tin riêng của khách không xuất hiện trong danh sách booking.
- Quản lý bắt đầu/kết thúc chuyến: [hướng dẫn Trips](trips-swagger.md).
- Chưa có cước chốt, thanh toán, bảo hiểm hoặc GPS.

## Kiểm chứng

BookingDatabaseTests tạo database PostgreSQL riêng, migrate, chạy test và xóa đúng database test đó.
Hai trường hợp bao phủ chủ hàng trực tiếp và môi giới nhận đơn ngoài; kiểm tra xác nhận đồng thời,
quyền sở hữu, thu hồi duyệt xe, xác nhận lặp, tài xế đang bận và chặn hủy tin đã đặt.
