# Quản lý chuyến xe

Chạy lại CoreApi bằng profile http, mở http://localhost:5101/swagger, chọn nhóm Trips.
Dùng Authorize để đổi accessToken giữa tài xế và người tạo tin.
Lấy tripId từ response xác nhận booking hoặc GET /api/bookings/mine.

## Luồng

| Người thao tác | API | Trạng thái sau thao tác |
| --- | --- | --- |
| Tài xế | POST /api/trips/{id}/start | InProgress — đang đến lấy hàng |
| Tài xế | POST /api/trips/{id}/pickup | Loaded — đã nhận hàng |
| Tài xế | POST /api/trips/{id}/deliver | Delivered — chờ chủ tin xác nhận |
| Chủ hàng/môi giới tạo tin | POST /api/trips/{id}/complete | Completed |

GET /api/trips/{id} trả trạng thái, version và các mốc thời gian UTC.
Luôn gửi version mới nhất. Version cũ trả HTTP 409; tải lại chuyến để biết thao tác trước đã thành công hay chưa.
Start và complete nhận:

```json
{ "version": 1 }
```

## Ảnh bàn giao

Tài xế upload ảnh riêng cho từng bước nhận hàng/giao hàng:
POST /api/trips/{id}/proofs, body JSON:

```json
{
  "contentType": "image/jpeg",
  "content": "BASE64_CUA_FILE_ANH"
}
```

PNG/JPEG, tối đa 5 MiB/file. Content là base64 thuần, không thêm tiền tố data URL.
Ở giai đoạn đầu ảnh lưu trong PostgreSQL, kiểm tra dung lượng và chữ ký định dạng.
Chưa có kiểm chứng ảnh chụp thực tế hoặc phân tích nội dung ảnh.
Response trả id của ảnh và tripVersion mới. Dùng hai giá trị này khi gọi pickup/deliver:

```json
{
  "version": 3,
  "proofId": "GUID_ANH_VUA_UPLOAD",
  "note": "Đã kiểm đếm và bàn giao"
}
```

Ảnh phải thuộc đúng chuyến, do tài xế được giao upload; không dùng lại cùng proofId cho hai bước.
Chỉ tài xế và chủ tin được tải ảnh qua GET /api/trips/{id}/proofs/{proofId}.
Ảnh không có đường dẫn public.

## Hủy và sự cố

Hai bên có thể gọi POST /api/trips/{id}/cancel khi Assigned hoặc InProgress (chưa nhận hàng).
Sau khi nhận hàng, hủy trực tiếp bị từ chối; dùng POST /api/trips/{id}/incidents để báo sự cố.
Cả hai API nhận:

```json
{ "version": 2, "reason": "Lý do cụ thể" }
```

Báo sự cố ghi lịch sử và tăng version, không thay đổi trạng thái hay giải phóng xe.
Chưa có quy trình giải quyết tranh chấp hoặc cưỡng chế kết thúc chuyến.
GET /api/trips/{id}/events?page=1 trả lịch sử, mỗi trang 20 dòng.

## Quy tắc dữ liệu

- Assigned, InProgress, Loaded, Delivered đều giữ tài xế và xe; database chặn booking trùng.
- Completed/Cancelled đồng bộ trạng thái booking và tin vận chuyển trong cùng lần ghi với lịch sử.
- Tin đã hủy không tự đăng lại; tạo tin mới nếu cần tìm tài xế khác.
- Có audit người thao tác; dữ liệu ảnh và lý do không ghi vào log chẩn đoán.
- Các API này chưa gồm GPS, bảo hiểm hoặc thanh toán.

Kiểm thử PostgreSQL riêng bao phủ hai loại chủ tin, quyền truy cập, ảnh, chuyển trạng thái,
hoàn tất đồng thời, giữ xe trong chuyến và giải phóng xe sau hoàn tất/hủy.
