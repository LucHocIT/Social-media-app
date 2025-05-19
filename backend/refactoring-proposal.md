# Đề xuất tái cấu trúc dự án SocialApp

## 1. Loại bỏ TestController

`TestController` chỉ được sử dụng để kiểm tra API và không cần thiết trong môi trường sản phẩm.

## 2. Hợp nhất AuthController và AccountController

Hai controller này có chức năng rất giống nhau và có nhiều endpoint trùng lặp. Nên hợp nhất thành một `AuthController` duy nhất.

## 3. Loại bỏ lớp AuthService

`AuthService` chỉ là một lớp trung gian (facade) chuyển tiếp các phương thức đến các service khác. Có thể loại bỏ lớp này và sử dụng trực tiếp các service cụ thể như `UserAccountService`, `EmailVerificationCodeService` và `UserManagementService` trong controller.

## 4. Thống nhất các endpoint API

Các endpoint giữa `api/Account` và `api/Auth` đang bị trùng lặp. Nên chuẩn hóa các endpoint này thành một định dạng nhất quán:

- Đăng ký: `/api/auth/register` 
- Đăng nhập: `/api/auth/login`
- Xác thực email: `/api/auth/verify-email`
- Gửi mã xác nhận: `/api/auth/send-verification-code`
- Xác thực mã: `/api/auth/verify-code`
- Quên mật khẩu: `/api/auth/forgot-password`
- Đặt lại mật khẩu: `/api/auth/reset-password`
- Quản lý người dùng: `/api/admin/users/*`

## 5. Cải thiện cấu trúc code

Nên tách biệt rõ ràng giữa các lớp Auth, User và Email thay vì để chúng tương tác phức tạp với nhau, tránh các dependency cycle như đã sửa trước đó.

## 6. Thống nhất ngôn ngữ thông báo

Hiện tại dự án đang sử dụng mix giữa tiếng Anh và tiếng Việt trong các thông báo lỗi. Nên thống nhất sử dụng một ngôn ngữ để dễ bảo trì hơn.
