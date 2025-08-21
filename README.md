## 📋 Tổng quan dự án

Hệ thống mạng xã hội (Social Media Platform) là một ứng dụng web giúp người dùng kết nối, chia sẻ nội dung và tương tác với nhau một cách dễ dàng và hiệu quả. Backend được xây dựng với ASP.NET Core 8.0 và Entity Framework Core.

## 🏢 Nghiệp vụ chính

### 1. Quản lý Người dùng & Xác thực (User Management & Authentication)

- **Đăng ký tài khoản**: Đăng ký với email, mật khẩu và xác thực email
- **Đăng nhập bảo mật**: Xác thực JWT với access token và refresh token
- **Quản lý profile**: Cập nhật thông tin cá nhân, ảnh đại diện, bio
- **Theo dõi người dùng**: Follow/Unfollow users, quản lý danh sách following/followers
- **Chặn người dùng**: Block/Unblock users để kiểm soát tương tác
- **Email verification**: Xác thực email khi đăng ký và đổi email

### 2. Quản lý Bài viết (Post Management)

- **Tạo bài viết**: Đăng bài với text, hình ảnh, video
- **Upload media**: Tích hợp Cloudinary để lưu trữ hình ảnh/video
- **Hiển thị timeline**: Feed cá nhân và feed từ những người đang follow
- **Tương tác bài viết**: Like, unlike, reaction với emoji
- **Chia sẻ bài viết**: Share posts với comment
- **Báo cáo bài viết**: Report inappropriate content

### 3. Hệ thống Bình luận (Comment System)

- **Bình luận bài viết**: Comment trên posts với text và emoji
- **Phản hồi bình luận**: Reply to comments (nested comments)
- **Tương tác bình luận**: Like/unlike comments
- **Quản lý bình luận**: Edit, delete own comments
- **Báo cáo bình luận**: Report inappropriate comments
- **Sắp xếp bình luận**: Sort by latest, oldest, most liked

### 4. Hệ thống Chat thời gian thực (Real-time Chat)

- **Chat 1-1**: Nhắn tin trực tiếp giữa hai người dùng
- **Tin nhắn realtime**: Sử dụng SignalR để chat instantaneous
- **Lịch sử chat**: Lưu trữ và hiển thị lịch sử tin nhắn
- **Trạng thái tin nhắn**: Seen/unseen status
- **Emoji reactions**: React tin nhắn với emoji
- **Typing indicators**: Hiển thị khi người khác đang typing

### 5. Hệ thống Thông báo (Notification System)

- **Thông báo realtime**: Push notifications cho các hoạt động
- **Loại thông báo**: Follow, like, comment, mention, message
- **Quản lý thông báo**: Mark as read, delete notifications
- **Cài đặt thông báo**: Bật/tắt các loại thông báo cụ thể
- **Thông báo push**: Integration với web push notifications
- **Email notifications**: Gửi email cho các hoạt động quan trọng

### 6. Quản lý Admin & Kiểm duyệt

- **Quản lý người dùng**: Xem, khóa, mở khóa tài khoản người dùng
- **Kiểm duyệt nội dung**: Review reported posts và comments
- **Thống kê hệ thống**: Dashboard với metrics về users, posts, engagement
- **Quản lý báo cáo**: Process user reports và content violations
- **Logs và audit**: Theo dõi các hoạt động quan trọng trong hệ thống

## 🔧 Công nghệ sử dụng

- **Framework**: ASP.NET Core 8.0
- **Database**: SQL Server với Entity Framework Core
- **Authentication**: JWT Bearer Token
- **Real-time**: SignalR for real-time communication
- **File Storage**: Cloudinary for media files
- **Email**: SMTP integration for email verification
- **Password Security**: BCrypt.Net for password hashing

## 🏗️ Kiến trúc hệ thống

### Cấu trúc thư mục chi tiết

```text
backend/
├── 📁 Controllers/            
│   ├── Auth/
│   │   └── AuthController.cs    
│   ├── User/
│   │   ├── ProfileController.cs 
│   │   └── UserBlockController.cs
│   ├── Post/
│   │   ├── PostsController.cs   
│   │   ├── CommentController.cs
│   │   └── ReactionsController.cs 
│   ├── Chat/
│   │   └── SimpleChatController.cs 
│   ├── Notification/
│   │   └── NotificationController.cs
│   ├── Admin/
│   │   └── UserManagementController.cs # Admin functions
│   ├── HealthController.cs      
│   └── HomeController.cs       
│
├── 📁 Models/                   
│   ├── User.cs                  
│   ├── Post.cs                 
│   ├── PostMedia.cs          
│   ├── Comment.cs           
│   ├── CommentReport.cs      
│   ├── Reaction.cs             
│   ├── UserFollower.cs        
│   ├── UserBlock.cs           
│   ├── Notification.cs        
│   ├── NotificationType.cs      
│   ├── ChatConversation.cs     
│   ├── SimpleMessage.cs        
│   ├── MessageReaction.cs      
│   ├── EmailVerificationCode.cs 
│   └── SocialMediaDbContext.cs  
│
├── 📁 DTOs/                     
│   ├── AuthDTOs.cs            
│   ├── PostAndMediaDTOs.cs     
│   ├── CommentDTOs.cs         
│   ├── ReactionDTOs.cs        
│   ├── ProfileDTOs.cs           
│   ├── UserBlockDTOs.cs         
│   ├── SimpleChatDTOs.cs        
│   ├── NotificationDTOs.cs      
│   ├── SocialLoginDTOs.cs      
│   └── ProfilePictureResults.cs 
│
├── 📁 Services/                
│   ├── Auth/                  
│   ├── User/                
│   ├── Post/                  
│   ├── Comments/             
│   ├── Chat/                   
│   ├── Notification/           
│   ├── Email/                 
│   └── Utils/                  
│
├── 📁 Hubs/                   
│   └── SimpleChatHub.cs        
│
├── 📁 Filters/                
│   └── FileUploadOperationFilter.cs 
│
├── 📁 Migrations/              
│   └── ...                    
│
└── 📁 wwwroot/                 
```

### Kiến trúc phân lớp (Layered Architecture)

```text
┌─────────────────────────────────────┐
│          Presentation Layer         │  ← Controllers (API Endpoints)
├─────────────────────────────────────┤
│           Business Layer            │  ← Services (Business Logic)
├─────────────────────────────────────┤
│         Real-time Layer             │  ← SignalR Hubs (WebSocket)
├─────────────────────────────────────┤
│         Data Access Layer           │  ← Entity Framework Core
├─────────────────────────────────────┤
│           Database Layer            │  ← SQL Server Database
└─────────────────────────────────────┘
```

## 🚀 Cài đặt và chạy

### Yêu cầu hệ thống

- .NET 8.0 SDK
- SQL Server 2019+ hoặc PostgreSQL
- Visual Studio 2022 hoặc VS Code

### Các bước cài đặt

1. **Clone repository**

```bash
git clone https://github.com/MLuc24/Social-media-app.git
cd SocialApp/backend
```

2. **Cấu hình database**

```bash
# Tạo file appsettings.json với connection string
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=SocialMediaDB;Trusted_Connection=true;TrustServerCertificate=true;"
  }
}
```

3. **Chạy migrations**

```bash
dotnet ef database update
```

4. **Chạy ứng dụng**

```bash
dotnet run
```

## 📚 API Documentation

API được thiết kế theo RESTful principles với các endpoint chính:

### Authentication & User Management
- **POST** `/api/auth/register` - Đăng ký tài khoản mới
- **POST** `/api/auth/login` - Đăng nhập (email/password)
- **POST** `/api/auth/refresh-token` - Làm mới access token
- **POST** `/api/auth/verify-email` - Xác thực email
- **GET** `/api/profile/{userId}` - Lấy thông tin profile
- **PUT** `/api/profile/update` - Cập nhật profile
- **POST** `/api/profile/upload-avatar` - Upload ảnh đại diện

### Social Features
- **POST** `/api/profile/follow` - Follow người dùng
- **DELETE** `/api/profile/unfollow/{userId}` - Unfollow người dùng
- **GET** `/api/profile/followers/{userId}` - Danh sách followers
- **GET** `/api/profile/following/{userId}` - Danh sách following
- **POST** `/api/userblock/block` - Block người dùng
- **DELETE** `/api/userblock/unblock/{userId}` - Unblock người dùng

### Post Management
- **GET** `/api/posts` - Lấy feed posts (có phân trang)
- **GET** `/api/posts/{id}` - Lấy chi tiết post
- **POST** `/api/posts` - Tạo post mới (với media)
- **PUT** `/api/posts/{id}` - Cập nhật post
- **DELETE** `/api/posts/{id}` - Xóa post
- **GET** `/api/posts/user/{userId}` - Posts của user cụ thể

### Interactions
- **POST** `/api/reactions/toggle` - Like/unlike post
- **GET** `/api/reactions/{postId}` - Lấy reactions của post
- **GET** `/api/comments/{postId}` - Lấy comments của post
- **POST** `/api/comments` - Tạo comment mới
- **PUT** `/api/comments/{id}` - Cập nhật comment
- **DELETE** `/api/comments/{id}` - Xóa comment

### Chat System
- **GET** `/api/simplechat/conversations` - Danh sách conversations
- **GET** `/api/simplechat/messages/{conversationId}` - Messages của conversation
- **POST** `/api/simplechat/send` - Gửi tin nhắn
- **PUT** `/api/simplechat/mark-read/{conversationId}` - Đánh dấu đã đọc

### Notifications
- **GET** `/api/notifications` - Lấy danh sách thông báo
- **PUT** `/api/notifications/{id}/read` - Đánh dấu đã đọc
- **DELETE** `/api/notifications/{id}` - Xóa thông báo

### Admin Functions
- **GET** `/api/admin/users` - Quản lý danh sách users
- **PUT** `/api/admin/users/{id}/status` - Khóa/mở khóa user
- **GET** `/api/admin/reports` - Xem báo cáo vi phạm

**Response Format**: Tất cả API trả về JSON với cấu trúc chuẩn:
```json
{
  "success": true,
  "data": { ... },
  "message": "Success message",
  "errors": []
}
```

## 🔐 Bảo mật

### Xác thực và Phân quyền
- **JWT Authentication**: Sử dụng access token (15 phút) và refresh token (7 ngày)
- **Role-based Authorization**: User, Admin roles
- **Password Security**: BCrypt hashing với salt rounds cao
- **Email Verification**: Xác thực email bắt buộc khi đăng ký

### Bảo vệ dữ liệu
- **Input Validation**: Model validation cho tất cả DTOs
- **SQL Injection Protection**: Entity Framework parameterized queries
- **XSS Protection**: Built-in ASP.NET Core protection
- **CORS Configuration**: Chỉ cho phép origins được cấu hình
- **File Upload Security**: Validate file types và size limits

### API Security
```csharp
// Security headers và middleware
app.UseHttpsRedirection();
app.UseCors("AllowSpecificOrigins");
app.UseAuthentication();
app.UseAuthorization();
```

### Notification Hub
```csharp
// Real-time notifications
- New follower notifications
- Post like/comment notifications  
- Message notifications
- System announcements
```

## 📊 Database Schema

### Core Entities

**Users Table**
- Id, Email, Username, PasswordHash
- FirstName, LastName, Bio, ProfilePictureUrl
- IsEmailVerified, IsActive, CreatedAt

**Posts Table**
- Id, UserId, Content, CreatedAt, UpdatedAt
- IsDeleted, LikesCount, CommentsCount

**PostMedia Table**
- Id, PostId, MediaUrl, MediaType, PublicId

**Comments Table**
- Id, PostId, UserId, Content, ParentCommentId
- CreatedAt, IsDeleted, LikesCount

**Relationships**
- UserFollowers (FollowerId, FollowingId)
- UserBlocks (BlockerId, BlockedId)
- Reactions (UserId, PostId, ReactionType)

## 📝 Logging & Monitoring

### Logging Configuration
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "SocialApp": "Debug"
    }
  }
}
```

### Key Metrics
- **User Engagement**: DAU, MAU, session duration
- **Content Metrics**: Posts per day, comments, likes
- **Performance**: API response times, error rates
- **Real-time**: Active connections, message volume

## 🔧 Configuration & Environment

### Environment Variables
```bash
# Database
ConnectionStrings__DefaultConnection="Server=.;Database=SocialMediaDB;..."

# JWT Settings  
JWT__SecretKey="your-super-secret-key-here"
JWT__Issuer="SocialMediaAPI"
JWT__Audience="SocialMediaApp"
JWT__AccessTokenExpirationMinutes=15
JWT__RefreshTokenExpirationDays=7

# Cloudinary (Media Storage)
Cloudinary__CloudName="your-cloud-name"
Cloudinary__ApiKey="your-api-key"
Cloudinary__ApiSecret="your-api-secret"

# Email Settings
Email__SmtpServer="smtp.gmail.com"
Email__SmtpPort=587
Email__Username="your-email@domain.com"
Email__Password="your-app-password"

# CORS Settings
CORS__AllowedOrigins="http://localhost:3000,http://localhost:5173"
```

### Development vs Production
```text
Development:
- Detailed error responses
- Swagger UI enabled
- CORS allows all origins
- Verbose logging

Production:
- Generic error messages
- Swagger UI disabled
- Strict CORS policy
- Optimized logging
```


### Real-time Optimization
- **SignalR Scaling**: Redis backplane cho multiple server instances
- **Connection Management**: Efficient connection grouping
- **Message Batching**: Batch notifications để reduce traffic

## 🔍 Troubleshooting

### Common Issues

**1. Database Connection Issues**
```bash
# Check connection string in appsettings.json
# Verify SQL Server/PostgreSQL is running
# Check firewall and network connectivity
```

**2. JWT Token Issues**
```bash
# Verify JWT secret key in configuration
# Check token expiration settings
# Validate issuer and audience claims
```

**3. SignalR Connection Issues**
```bash
# Check CORS settings for WebSocket
# Verify authentication for protected hubs
# Monitor connection limits and scaling
```

**4. Cloudinary Upload Issues**
```bash
# Verify API credentials
# Check file size and type restrictions
# Monitor upload quotas and limits
```

### Performance Monitoring
```bash
# Monitor key metrics
- API response times (< 200ms for simple queries)
- Database query performance
- SignalR connection counts
- Memory usage and garbage collection
- File upload success rates
```
