# MedCare Pharmacy

Ứng dụng ASP.NET Core MVC/Web API, EF Core và SQL Server. Các API quản trị yêu cầu JWT; admin đầu tiên được tạo một lần qua `POST /api/auth/bootstrap-admin` sau khi cấu hình khóa JWT.

## Cấu hình phát triển

Trong thư mục `MedCare Pharmacy/MedCare Pharmacy`, đặt khóa JWT bí mật bằng User Secrets. Không ghi khóa vào `appsettings.json` hoặc commit khóa vào Git:

```powershell
dotnet user-secrets set "Jwt:Key" "<khóa-ngẫu-nhiên-ít-nhất-32-byte>"
```

Ứng dụng dừng khi khóa không có hoặc ngắn hơn 32 byte. Có thể dùng biến môi trường `Jwt__Key` thay cho User Secrets.

Chạy `BTLWEL.sql` trong SSMS để tạo schema và dữ liệu danh mục demo. Script không tạo sẵn user hoặc mật khẩu. Khi ứng dụng đang chạy, tạo admin đầu tiên bằng API `POST /api/auth/bootstrap-admin` với JSON gồm `fullName`, `phone`, `email`, `password`; mật khẩu phải tối thiểu 12 ký tự và có cả chữ lẫn số. Endpoint chỉ hoạt động khi database chưa có user.

## Lưu ý khi dùng database cũ

- Tài khoản demo cũ dùng mật khẩu `123456` được lưu dạng văn bản thuần. Phiên bản này từ chối hash không phải BCrypt; hãy đặt lại/xóa các tài khoản demo cũ bằng cách quản trị database trước khi sử dụng.
- Chạy lại script schema sẽ bổ sung cột mã thuốc nếu thiếu và chuyển khóa ngoại sản phẩm sang `NO ACTION`, tránh xóa dây chuyền lịch sử đơn hàng/lô nhập. Nếu database có dữ liệu không hợp lệ, SQL Server có thể từ chối thêm ràng buộc để bảo vệ dữ liệu.
- Khóa JWT đã từng được commit trong `appsettings.json`; hãy thay khóa ở mọi môi trường và thu hồi các token cũ. Nếu dự án được chia sẻ, cân nhắc xóa secret khỏi lịch sử Git.

## Luồng dữ liệu

- Client chỉ gửi mã sản phẩm và số lượng khi bán/đặt hàng. Server tra giá hiện tại, tính tổng và cập nhật tồn kho trong transaction.
- Đơn online giữ tồn khi được tạo; khi nhân viên hủy trước khi giao, tồn được hoàn lại. POS tính VAT 5% trên server.
- Thuốc đã có giao dịch hoặc lô nhập không thể xóa để bảo toàn lịch sử.
