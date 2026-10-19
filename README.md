# MedCare Pharmacy — Hệ thống quản lý nhà thuốc trực tuyến

**Bài tập lớn môn Lập trình Web 2**  
Repository: [phanhuyvinh/BTLLapTrinhWeb2](https://github.com/phanhuyvinh/BTLLapTrinhWeb2)

---

## 1. Giới thiệu

**MedCare Pharmacy** là ứng dụng web quản lý nhà thuốc, hỗ trợ bán hàng online, bán tại quầy (POS), quản lý kho theo lô (batch), đơn thuốc, chat tư vấn và báo cáo. Hệ thống phân quyền rõ ràng theo 3 vai trò: **Admin**, **Dược sĩ (Pharmacist)** và **Khách hàng (Customer)**.

### Công nghệ chính

| Thành phần | Công nghệ |
|---|---|
| Backend / Framework | ASP.NET Core (MVC + Web API), .NET 10 |
| ORM | Entity Framework Core + SQL Server |
| Xác thực | JWT Bearer (API) + Cookie HttpOnly (trang MVC staff) |
| Mã hóa mật khẩu | BCrypt.Net-Next |
| API docs | Swagger / Swashbuckle |
| Bảo vệ | Rate limiting (auth, tạo đơn), CORS |
| Database | SQL Server / LocalDB |

---

## 2. Tính năng theo vai trò

### Khách hàng (Customer)
- Đăng ký / đăng nhập / quản lý hồ sơ, đổi mật khẩu, upload avatar
- Xem danh sách sản phẩm, chi tiết sản phẩm
- Giỏ hàng, thanh toán (checkout), đặt hàng online (COD)
- Upload đơn thuốc (prescription)
- Xem lịch sử đơn hàng
- Chat / tư vấn với dược sĩ

### Dược sĩ (Pharmacist)
- Dashboard dược sĩ
- Bán hàng tại quầy (**POS**)
- Quản lý đơn hàng online (duyệt, cập nhật trạng thái: Approved / Shipping / Completed / Cancelled)
- Cảnh báo tồn kho / hạn sử dụng
- Chat tư vấn khách hàng

### Quản trị viên (Admin)
- Dashboard quản trị
- Quản lý người dùng (khóa/mở, phân quyền)
- Quản lý danh mục (Categories), nhà cung cấp (Suppliers)
- Quản lý thuốc / sản phẩm
- Nhập kho (Purchase Receipt + Inventory Batch)
- Báo cáo (Reports)

---

## 3. Cấu trúc thư mục (tóm tắt)

```
BTLLapTrinhWeb2/
├── MedCarePharmacy_Complete.sql          # Script tạo DB + seed dữ liệu
├── .gitignore
└── MedCare Pharmacy/
    ├── MedCare Pharmacy.slnx
    └── MedCare Pharmacy/                 # Project ASP.NET Core
        ├── Controllers/                  # MVC + API Controllers
        │   ├── AdminController.cs
        │   ├── AuthController.cs
        │   ├── CategoriesController.cs
        │   ├── ChatController.cs
        │   ├── CustomerController.cs
        │   ├── HomeController.cs
        │   ├── InventoryBatchesController.cs
        │   ├── OrdersController.cs
        │   ├── PharmacistController.cs
        │   ├── PrescriptionsController.cs
        │   ├── ProductsController.cs
        │   ├── ReportsController.cs
        │   ├── SuppliersController.cs
        │   └── UsersController.cs
        ├── Models/
        │   ├── Data/                     # AppDbContext
        │   ├── Entities/                 # User, Product, Order, ...
        │   └── ViewModels/
        ├── Views/
        │   ├── Admin/                    # Categories, Medicines, NhapKho, Reports, Suppliers, Users
        │   ├── Customer/                 # Cart, Checkout, Login, Products, Orders, ...
        │   ├── Pharmacist/               # POS, OnlineOrders, Consultation, InventoryAlert
        │   ├── Home/
        │   └── Shared/
        ├── ViewComponents/
        ├── wwwroot/                      # css, js, images, uploads, lib
        ├── Program.cs
        ├── appsettings.json
        └── MedCare Pharmacy.csproj
```

### Các entity chính
- **User** — tài khoản (role: admin / pharmacist / customer)
- **Category**, **Supplier**, **Product**
- **InventoryBatch**, **InventoryMovement**, **PurchaseReceipt**, **PurchaseReceiptLine**
- **Order**, **OrderDetail** (Online / POS)
- **Prescription**, **ChatMessage**

---

## 4. Yêu cầu hệ thống

- [.NET SDK 10](https://dotnet.microsoft.com/download) (hoặc phiên bản tương thích với `net10.0`)
- SQL Server / SQL Server LocalDB
- (Khuyến nghị) Visual Studio 2022 hoặc VS Code + C# extension

---

## 5. Cài đặt & chạy dự án

### Bước 1: Clone repository

```bash
git clone https://github.com/phanhuyvinh/BTLLapTrinhWeb2.git
cd BTLLapTrinhWeb2
```

### Bước 2: Tạo database

1. Mở **SQL Server Management Studio (SSMS)** hoặc Azure Data Studio.
2. Kết nối tới instance SQL Server / LocalDB.
3. Chạy toàn bộ file:

```text
MedCarePharmacy_Complete.sql
```

Script sẽ:
- Tạo database `MedCarePharmacyDB` (nếu chưa có)
- Tạo các bảng, index, ràng buộc
- Seed danh mục, nhà cung cấp, ~299 sản phẩm + lô hàng
- Tạo tài khoản admin mặc định

### Bước 3: Cấu hình kết nối

File `MedCare Pharmacy/MedCare Pharmacy/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=MedCarePharmacyDB;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Jwt": {
    "Key": "MedCare_Secret_Key_Super_Secure_AtLeast32Chars_2026!",
    "Issuer": "MedCareApi",
    "Audience": "MedCareClient",
    "ExpireDays": 7
  }
}
```

- Thay `Server=...` nếu dùng SQL Server thật (không phải LocalDB).
- `Jwt:Key` phải ≥ 32 byte. Có thể dùng User Secrets / biến môi trường `Jwt__Key` cho môi trường production.

### Bước 4: Chạy ứng dụng

```bash
cd "MedCare Pharmacy/MedCare Pharmacy"
dotnet restore
dotnet run
```

Hoặc mở solution trong Visual Studio và nhấn **F5**.

- Ứng dụng mặc định: MVC + API cùng host.
- Trong môi trường Development: Swagger UI được bật (thường tại `/swagger`).

### Tài khoản mặc định (sau khi chạy SQL)

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Admin | `admin@medcare.vn` | `Admin12345678` |

> Mật khẩu được hash bằng BCrypt. Có thể đăng ký tài khoản customer mới qua API/form đăng ký.

---

## 6. API & bảo mật (tóm tắt)

- **Auth**: `POST /api/Auth/login`, `register`, `logout`, `update-profile`, `change-password`, `avatar`, ...
- **Orders**: tạo đơn online (rate limit), POS (admin/pharmacist), cập nhật trạng thái, xem theo user
- **Products, Categories, Suppliers, Inventory, Prescriptions, Chat, Reports, Users** — các controller tương ứng
- JWT cho API; Cookie `medcare_page_auth` cho trang staff (MVC)
- Rate limiting: policy `auth` (10 req/phút), `order-create` (20 req/phút)
- CORS cho phép frontend local (`localhost:5500`, `3000`, ...)
- Upload đơn thuốc / avatar có giới hạn kích thước; đường dẫn cũ `/uploads/prescriptions` được chặn trực tiếp

---

## 7. Ghi chú phát triển

- Project target: **net10.0**
- Packages chính: EF Core SqlServer, JwtBearer, BCrypt.Net-Next, Swashbuckle
- Mật khẩu đăng ký tối thiểu **12 ký tự**, gồm chữ và số
- Endpoint `bootstrap-admin` chỉ dùng khi chưa có user nào trong DB

---

## 8. License & đóng góp

Dự án phục vụ mục đích học tập (Bài tập lớn Lập trình Web 2).  
Mọi đóng góp / báo lỗi vui lòng mở Issue hoặc Pull Request trên repository.

---

**Tác giả:** phanhuyvinh  
**Repo:** https://github.com/phanhuyvinh/BTLLapTrinhWeb2
