# Hướng dẫn sử dụng biến môi trường trong ứng dụng

## Giới thiệu

Ứng dụng này sử dụng file `.env` để lưu trữ các thông tin nhạy cảm như API key, mật khẩu và các thông tin cấu hình. Điều này giúp tăng tính bảo mật và linh hoạt trong việc triển khai ứng dụng ở nhiều môi trường khác nhau.

## Cách cấu hình

1. Tạo file `.env` trong thư mục gốc của ứng dụng backend (nếu chưa có)
2. Thêm các biến môi trường cần thiết vào file

```properties
# Cloudinary Configuration
CLOUDINARY_CLOUD_NAME=your_cloud_name
CLOUDINARY_API_KEY=your_api_key
CLOUDINARY_API_SECRET=your_api_secret

# Database Connection
DB_SERVER=your_db_server
DB_DATABASE=your_db_name
DB_USER=your_db_user
DB_PASSWORD=your_db_password

# JWT Configuration
JWT_KEY=your_jwt_key
JWT_ISSUER=your_issuer
JWT_AUDIENCE=your_audience
JWT_EXPIREDAYS=7

# Social Authentication
GOOGLE_CLIENT_ID=your_google_client_id
GOOGLE_CLIENT_SECRET=your_google_client_secret
FACEBOOK_APP_ID=your_facebook_app_id
FACEBOOK_APP_SECRET=your_facebook_app_secret

# Email Configuration
SMTP_HOST=your_smtp_host
SMTP_PORT=your_smtp_port
EMAIL_USERNAME=your_email_username
EMAIL_PASSWORD=your_email_password
EMAIL_SENDER=your_email_sender
EMAIL_SENDER_NAME=your_sender_name
EMAIL_ENABLE_SSL=true

# API Keys
EMAIL_VERIFICATION_API_KEY=your_verification_api_key
```

## Cách hoạt động

Ứng dụng sẽ tự động tải các biến từ file `.env` khi khởi động. Quá trình này được xử lý bởi lớp `DotEnv` trong `Services/Utils/DotEnv.cs`.

Các dịch vụ trong ứng dụng được thiết kế để:
1. Đầu tiên tìm kiếm giá trị từ biến môi trường
2. Nếu không tìm thấy, sẽ sử dụng giá trị từ file cấu hình (appsettings.json)

## Bảo mật

Lưu ý về bảo mật:

1. **KHÔNG** đưa file `.env` vào hệ thống quản lý mã nguồn (git)
2. Thêm `.env` vào file `.gitignore` 
3. Cung cấp một mẫu file `.env.example` không chứa thông tin nhạy cảm thực
4. Trong môi trường production, nên sử dụng các dịch vụ quản lý cấu hình bảo mật như Azure Key Vault, AWS Secrets Manager, hoặc biến môi trường của hệ thống

## Cấu hình cho các dịch vụ cụ thể

### Cloudinary

Để có được thông tin cấu hình Cloudinary:
1. Đăng ký tài khoản tại [Cloudinary](https://cloudinary.com/users/register/free)
2. Sau khi đăng nhập, vào Dashboard để lấy Cloud name, API Key và API Secret
3. Thêm các giá trị vào file `.env` như hướng dẫn ở trên
