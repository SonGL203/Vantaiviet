# OTP và hàng đợi email

CoreApi dùng bảng public.OtpDeliveries cùng database hiện có. Không cần Redis hoặc database mới.
Migration AddOtpDeliveryQueue được triển khai có kiểm soát; ứng dụng không tự migrate khi khởi động.

## Cấu hình SMTP trong Visual Studio

Chuột phải VantaiViet.CoreApi → Manage User Secrets. Gộp phần sau vào secrets hiện có, giữ nguyên connection string và JWT:

```json
{
  "Notifications": {
    "WorkerEnabled": true,
    "Email": { "Simulate": false },
    "Phone": { "Simulate": true },
    "Smtp": {
      "Host": "SMTP_HOST_CUA_BAN",
      "Port": 587,
      "From": "DIA_CHI_GUI_DA_DUOC_NHA_CUNG_CAP_XAC_MINH",
      "Username": "SMTP_USERNAME",
      "Password": "SMTP_PASSWORD"
    }
  }
}
```

Adapter hiện hỗ trợ SMTP STARTTLS (thường cổng 587), bắt buộc TLS; không hỗ trợ implicit TLS cổng 465 hoặc OAuth2.
Khởi động lại API sau khi đổi secrets. Không đưa mật khẩu SMTP vào Git hoặc gửi qua chat.
Không cấu hình SMTP thì mặc định Development mô phỏng cả hai kênh. Production từ chối mô phỏng.
Phone thật chưa có nhà cung cấp, trả 503 not_configured; chưa tích hợp eSMS/Zalo/SMS.

## Test Swagger

Đăng nhập /api/auth/login, Authorize bằng accessToken. Đây là xác minh liên hệ cho tài khoản đã đăng nhập,
chưa phải đăng nhập không mật khẩu hoặc khôi phục mật khẩu trước đăng nhập.

1. POST /api/verification/requests:

```json
{
  "requestKey": "61e6f6bc-f467-4eee-bcff-e887c02db078",
  "channel": "Email",
  "destination": "EMAIL_CUA_BAN"
}
```

Channel Phone nhận số dạng +84…; thay requestKey bằng GUID mới cho yêu cầu mới.
Retry mạng phải giữ nguyên key và body. Cùng key khác nội dung trả 409. Không tạo lại sau cửa sổ giữ dữ liệu 7 ngày.
2. GET /api/verification/requests/{id}: Pending → Processing → Sent hoặc Failed/Expired.
   Sent nghĩa SMTP chấp nhận, không bảo đảm đến inbox. Hệ thống chưa nhận bounce/complaint webhook.
3. Nếu simulated=true: GET /api/verification/requests/{id}/development-code, chỉ chủ yêu cầu trong Development.
4. POST /api/verification/requests/{id}/verify với {"code":"123456"}.
   Email thật đúng mã: verified=true và cập nhật EmailConfirmed. Giả lập: simulated=true, verified=false;
   không dùng giả lập để chứng nhận quyền sở hữu email/số điện thoại thật.

## Giới hạn và độ tin cậy

- 60 giây giữa hai yêu cầu của cùng user hoặc cùng đích; tối đa 5/giờ cho nhóm này.
- Tối đa 100 yêu cầu/giờ toàn hệ thống, chỉnh Notifications:MaxRequestsPerHour phù hợp gói SMTP.
- Worker toàn hệ thống tối đa một lần gửi/2 giây; lease 60 giây, timeout SMTP 20 giây.
- Retry lỗi tạm: tối đa 3 lần tổng cộng, chờ 15 rồi 30 giây. SMTP lỗi 5xx dừng ngay.
- Transaction ngắn để claim; không giữ transaction khi chờ SMTP. Worker chết thì lease được thu hồi.
- SMTP không có exactly-once: timeout/crash sau khi SMTP đã nhận có thể gửi lặp cùng mã. Request key không loại bỏ cửa sổ này.
- OTP 6 số ngẫu nhiên mật mã, hạn 5 phút, tối đa 5 lần nhập sai, tiêu thụ một lần; yêu cầu mới vô hiệu mã cũ.
- Payload mã được bảo vệ bằng ASP.NET Data Protection, không log mã hoặc nội dung email. Hết hạn/đã dùng thì xóa payload; dọn bản ghi sau 7 ngày.
- Khi triển khai phải lưu bền vững Data Protection keys và dùng chung keys giữa các instance; mất keys sẽ làm OTP đang chờ không đọc được.
- Đây là queue OTP, không phải API gửi email tùy ý/marketing. Chưa có hàng đợi email thông báo chuyến riêng.
- Giới hạn gửi giúp giảm spam nhưng không bảo đảm vào inbox; xác minh domain gửi và SPF/DKIM/DMARC theo hướng dẫn nhà cung cấp trước production.

Đăng ký hiện tại vẫn là luồng thử nghiệm PendingKyc; các cờ xác minh số điện thoại cũ chưa được backfill.
Không xem các tài khoản cũ đã xác nhận tạm là bằng chứng OTP thật.
