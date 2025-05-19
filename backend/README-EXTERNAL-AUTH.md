# Hướng dẫn cấu hình đăng nhập bằng Facebook và Google

## Tổng quan

Chức năng đăng nhập bằng Facebook và Google đã được thêm vào ứng dụng SocialApp. Bài viết này sẽ hướng dẫn các bước để hoàn thành cấu hình và triển khai tính năng này.

## 1. Sửa lỗi biên dịch trong mã nguồn

Trước khi chạy migration để cập nhật cơ sở dữ liệu, cần sửa các lỗi cú pháp trong các tệp mã nguồn:

- Kiểm tra và sửa lỗi trong `AuthController.cs`
- Kiểm tra và sửa lỗi trong `AuthService.cs`

## 2. Cấu hình thông tin ứng dụng

### Facebook Developer Portal

1. Truy cập [Facebook Developer Portal](https://developers.facebook.com/)
2. Tạo một ứng dụng mới (hoặc sử dụng một ứng dụng hiện có)
3. Từ bảng điều khiển ứng dụng, thêm sản phẩm "Facebook Login"
4. Cấu hình OAuth callback URL: `https://your-domain.com/signin-facebook`
5. Lấy thông tin App ID và App Secret để điền vào file `appsettings.json`

### Google Cloud Console

1. Truy cập [Google Cloud Console](https://console.cloud.google.com/)
2. Tạo một dự án mới (hoặc sử dụng dự án hiện có)
3. Đi đến "API & Services > Credentials"
4. Tạo OAuth 2.0 Client ID mới
5. Cấu hình Authorized redirect URIs: `https://your-domain.com/signin-google`
6. Lấy thông tin Client ID và Client Secret để điền vào file `appsettings.json`

## 3. Cập nhật appsettings.json

Thay đổi các giá trị trong phần "Authentication" trong file `appsettings.json`:

```json
"Authentication": {
  "Facebook": {
    "AppId": "YOUR_FACEBOOK_APP_ID",
    "AppSecret": "YOUR_FACEBOOK_APP_SECRET"
  },
  "Google": {
    "ClientId": "YOUR_GOOGLE_CLIENT_ID",
    "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
  }
}
```

## 4. Tạo migration và cập nhật cơ sở dữ liệu

Sau khi sửa lỗi biên dịch, chạy các lệnh sau:

```powershell
cd e:\SocialApp\backend
dotnet build
dotnet ef migrations add AddExternalAuthProviders
dotnet ef database update
```

## 5. Kiểm tra API trong Swagger

API mới để xác thực từ bên ngoài đã được thêm vào:

- `POST /api/Auth/external-login`: Xử lý đăng nhập từ Facebook và Google

Payload mẫu:

```json
{
  "provider": "Facebook", // hoặc "Google"
  "accessToken": "access_token_from_client_side"
}
```

## 6. Frontend tích hợp

Để tích hợp với frontend, bạn cần thực hiện:

1. Thêm SDK của Facebook và Google vào ứng dụng frontend
2. Khi người dùng đăng nhập qua Facebook/Google, lấy access token
3. Gửi access token đến API `external-login` của backend
4. Nhận JWT token từ backend và lưu để sử dụng cho các yêu cầu sau đó

---

Nếu có bất kỳ vấn đề nào về cấu hình hoặc triển khai, hãy kiểm tra:
1. Log của ứng dụng để xem thông tin lỗi chi tiết
2. Đảm bảo thông tin cấu hình (AppId, Secret) chính xác
3. Kiểm tra xem các package và references đã được thêm đúng cách
