# Social Media App - Backend

Backend API cho ứng dụng mạng xã hội được xây dựng với ASP.NET Core 8.

## Tổng quan

Backend cung cấp REST API và real-time chat cho ứng dụng mạng xã hội với các tính năng chính:

- Xác thực và phân quyền người dùng (JWT)
- Quản lý bài viết và bình luận
- Hệ thống chat real-time
- Quản lý hồ sơ người dùng
- Thông báo
- Upload và quản lý media

## Công nghệ sử dụng

- **Framework**: ASP.NET Core 8
- **Database**: PostgreSQL / SQL Server
- **ORM**: Entity Framework Core 8
- **Authentication**: JWT Bearer Token
- **Real-time**: SignalR
- **Cloud Storage**: Cloudinary
- **Password Hashing**: BCrypt.Net
- **Documentation**: Swagger/OpenAPI

## Cấu trúc thư mục

```
backend/
├── Controllers/          # API Controllers
│   ├── Auth/            # Authentication endpoints
│   ├── Post/            # Posts, comments, reactions
│   ├── User/            # User profile management
│   ├── Chat/            # Real-time chat
│   ├── Admin/           # Admin functions
│   └── Notification/    # Notifications
├── Models/              # Entity models
├── DTOs/                # Data Transfer Objects
├── Services/            # Business logic
├── Hubs/                # SignalR hubs
├── Migrations/          # Database migrations
└── wwwroot/            # Static files
```

## Cài đặt và chạy

### Yêu cầu hệ thống

- .NET 8 SDK
- PostgreSQL hoặc SQL Server
- Cloudinary account (cho upload hình ảnh)

### Cài đặt

1. Clone repository
2. Restore packages:
```bash
dotnet restore
```

3. Cập nhật connection string trong `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "your-connection-string"
  }
}
```

4. Chạy migration:
```bash
dotnet ef database update
```

5. Khởi chạy ứng dụng:
```bash
dotnet run
```

API sẽ chạy tại: `https://localhost:7001`

## Environment Variables

Tạo file `.env` hoặc cấu hình trong `appsettings.json`:

## API Endpoints

### Authentication
- `POST /api/auth/register` - Đăng ký tài khoản
- `POST /api/auth/login` - Đăng nhập

### Posts
- `GET /api/posts` - Lấy danh sách bài viết
- `POST /api/posts` - Tạo bài viết mới
- `PUT /api/posts/{id}` - Cập nhật bài viết
- `DELETE /api/posts/{id}` - Xóa bài viết

### Users
- `GET /api/users/profile` - Lấy thông tin profile
- `PUT /api/users/profile` - Cập nhật profile
- `POST /api/users/profile/picture` - Upload avatar

### Chat
- SignalR Hub: `/chathub` - Real-time messaging

## Database

Ứng dụng sử dụng Entity Framework Core với các entity chính:

- User - Người dùng
- Post - Bài viết  
- Comment - Bình luận
- Reaction - Tương tác (like, dislike)
- SimpleMessage - Tin nhắn chat
- Notification - Thông báo

## Swagger Documentation

Khi chạy ở môi trường Development, truy cập Swagger UI tại:
`https://localhost:7001/swagger`
