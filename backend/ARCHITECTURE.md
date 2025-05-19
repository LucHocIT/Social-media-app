# Kiến trúc ứng dụng SocialApp

## Cấu trúc thư mục

Dự án SocialApp được tổ chức theo các module chức năng, với cấu trúc thư mục như sau:

```
backend/
  ├── Controllers/
  │   ├── Auth/             # Controllers liên quan đến xác thực
  │   │   ├── AuthController.cs
  │   │   └── AccountController.cs
  │   ├── Admin/            # Controllers dành cho quản trị
  │   │   └── UserManagementController.cs
  │   └── User/             # Controllers dành cho người dùng (sẽ phát triển)
  │
  ├── Services/
  │   ├── Auth/             # Dịch vụ xác thực
  │   │   ├── AuthService.cs
  │   │   ├── IAuthService.cs
  │   │   ├── UserAccountService.cs
  │   │   └── IUserAccountService.cs
  │   ├── Email/            # Dịch vụ email và xác thực email
  │   │   ├── EmailVerificationService.cs
  │   │   ├── IEmailVerificationService.cs
  │   │   ├── EmailVerificationCodeService.cs
  │   │   └── IEmailVerificationCodeService.cs
  │   └── User/             # Dịch vụ quản lý người dùng
  │       ├── UserManagementService.cs
  │       └── IUserManagementService.cs
  │
  ├── Models/               # Thực thể dữ liệu và DbContext
  │
  └── DTOs/                 # Các đối tượng truyền dữ liệu
```

## Mô hình kiến trúc

Ứng dụng được xây dựng theo mô hình phân lớp:

1. **Controllers**: Xử lý HTTP request và response, định tuyến API
2. **Services**: Chứa logic nghiệp vụ và xử lý dữ liệu
3. **Models**: Định nghĩa cấu trúc dữ liệu và quan hệ
4. **DTOs**: Đối tượng truyền dữ liệu giữa client và server

## Hướng dẫn mở rộng

Khi thêm chức năng mới:

1. **Định nghĩa các Model mới** trong thư mục Models
2. **Tạo các DTO cần thiết** trong thư mục DTOs
3. **Thực hiện logic nghiệp vụ** trong Service phù hợp
4. **Tạo Controller** để xử lý API endpoints

## Quy ước đặt tên

- **Controllers**: `[Feature]Controller.cs` (AuthController, PostController...)
- **Services**: `[Feature]Service.cs` và `I[Feature]Service.cs`
- **Models**: `[Entity].cs` (User.cs, Post.cs...)
- **DTOs**: `[Feature]DTOs.cs` (AuthDTOs.cs, PostDTOs.cs...)

## Tương lai phát triển

Các module dự kiến sẽ phát triển:

1. **Posts**: Quản lý bài đăng, comment, và like
2. **Messaging**: Hệ thống tin nhắn giữa người dùng
3. **Notifications**: Hệ thống thông báo
4. **Followers**: Quản lý quan hệ theo dõi giữa người dùng
5. **Media**: Quản lý hình ảnh và video

Mỗi module sẽ tuân theo cấu trúc đã được thiết lập với các thư mục Controllers, Services và DTOs tương ứng.
