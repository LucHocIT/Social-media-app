# Hướng dẫn cấu hình Email cho SocialApp

## Cấu hình Email qua biến môi trường

Để cấu hình thông tin gửi email trong ứng dụng SocialApp, bạn có thể sử dụng các biến môi trường sau:

| Biến môi trường | Mô tả | Giá trị mặc định |
|----------------|-------|-----------------|
| EMAIL_SMTP_SERVER | Địa chỉ máy chủ SMTP | smtp.gmail.com |
| EMAIL_SMTP_PORT | Cổng máy chủ SMTP | 587 |
| EMAIL_USERNAME | Tên đăng nhập email | từ appsettings.json |
| EMAIL_PASSWORD | Mật khẩu email (hoặc mật khẩu ứng dụng) | từ appsettings.json |
| EMAIL_SENDER | Địa chỉ email người gửi | từ appsettings.json |
| EMAIL_SENDER_NAME | Tên người gửi hiển thị | SocialApp |
| EMAIL_USE_SSL | Sử dụng kết nối SSL | true |

## Thiết lập trong PowerShell

```powershell
# Thiết lập biến môi trường tạm thời
$env:EMAIL_USERNAME = "your-actual-email@gmail.com"
$env:EMAIL_PASSWORD = "your-app-password"
$env:EMAIL_SENDER = "your-actual-email@gmail.com"

# Chạy ứng dụng
dotnet run
```

## Thiết lập trong Command Prompt

```cmd
:: Thiết lập biến môi trường tạm thời
set EMAIL_USERNAME=your-actual-email@gmail.com
set EMAIL_PASSWORD=your-app-password
set EMAIL_SENDER=your-actual-email@gmail.com

:: Chạy ứng dụng
dotnet run
```

## Thiết lập trong production (server Linux)

```bash
# Thêm vào file .bashrc hoặc .profile
export EMAIL_USERNAME="your-actual-email@gmail.com"
export EMAIL_PASSWORD="your-app-password"
export EMAIL_SENDER="your-actual-email@gmail.com"
```

## Lưu ý:

- Các biến môi trường sẽ được ưu tiên cao hơn so với các giá trị trong file cấu hình appsettings.json
- Nếu không thiết lập biến môi trường, ứng dụng sẽ sử dụng các giá trị từ appsettings.json
