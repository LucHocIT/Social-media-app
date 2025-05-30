# Script để chạy cả backend và frontend cùng lúc
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Write-Host "Đang khởi động SocialApp..." -ForegroundColor Green

# Khởi động Redis trước
Write-Host "Starting Redis..." -ForegroundColor Yellow
& "$PSScriptRoot\start-redis.ps1"

# Tạo thư mục để chứa logs nếu chưa tồn tại
$logsDir = ".\logs"
if (-not (Test-Path $logsDir)) {
    New-Item -ItemType Directory -Path $logsDir | Out-Null
}

# Kiểm tra xem backend có tồn tại không
$backendPath = "e:\SocialApp\backend"
if (-not (Test-Path $backendPath)) {
    Write-Host "Không tìm thấy thư mục backend tại '$backendPath'" -ForegroundColor Red
    exit 1
}

# Kiểm tra xem frontend có tồn tại không
$frontendPath = "e:\SocialApp\frontend"
if (-not (Test-Path $frontendPath)) {
    Write-Host "Không tìm thấy thư mục frontend tại '$frontendPath'" -ForegroundColor Red
    exit 1
}

# Khởi động backend (ASP.NET Core)
Write-Host "Đang khởi động backend..." -ForegroundColor Cyan
Start-Process -FilePath "powershell" -ArgumentList "-NoExit -Command cd '$backendPath'; dotnet watch run" -WindowStyle Normal

# Đợi 5 giây để backend khởi động trước
Write-Host "Chờ backend khởi động..." -ForegroundColor Cyan
Start-Sleep -Seconds 5

# Khởi động frontend (React/Vite)
Write-Host "Đang khởi động frontend..." -ForegroundColor Yellow
Start-Process -FilePath "powershell" -ArgumentList "-NoExit -Command cd '$frontendPath'; npm run dev" -WindowStyle Normal

# Đợi 5 giây để frontend khởi động 
Write-Host "Chờ frontend khởi động..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Tìm kiếm cổng mà frontend đang chạy
$frontendPort = 5173  # Cổng mặc định của Vite
# Nếu muốn kiểm tra xem cổng 3000 có đang được sử dụng không (ví dụ như khi dùng Create React App)
$port3000 = Get-NetTCPConnection -LocalPort 3000 -ErrorAction SilentlyContinue
if ($port3000) {
    $frontendPort = 3000
}

# Mở trình duyệt với URL Frontend
$frontendUrl = "http://localhost:$frontendPort"
Write-Host "Đang mở trình duyệt với URL: $frontendUrl" -ForegroundColor Yellow
Start-Process $frontendUrl

Write-Host "Ứng dụng SocialApp đã được khởi động!" -ForegroundColor Green
Write-Host "Backend running at: https://localhost:7103" -ForegroundColor Cyan
Write-Host "Frontend running at: $frontendUrl" -ForegroundColor Yellow
Write-Host "Nhấn phím bất kỳ để đóng script này (các cửa sổ ứng dụng vẫn chạy)" -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
