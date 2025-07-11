# Script để chạy cả backend và frontend cùng lúc
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

param(
    [string]$Mode = "local"  # local, docker, docker-dev
)

Write-Host "Đang khởi động SocialApp với mode: $Mode" -ForegroundColor Green

switch ($Mode) {
    "docker" {
        Write-Host "Chạy với Docker (Production mode)..." -ForegroundColor Yellow
        Write-Host "Backend sẽ kết nối với SQL Server: MANHLUC\MSPML" -ForegroundColor Cyan
        docker-compose down
        docker-compose up --build
        break
    }
    "docker-dev" {
        Write-Host "Chạy với Docker (Development mode)..." -ForegroundColor Yellow
        Write-Host "Backend sẽ kết nối với SQL Server: MANHLUC\MSPML" -ForegroundColor Cyan
        docker-compose -f docker-compose.dev.yml down
        docker-compose -f docker-compose.dev.yml up --build
        break
    }
    default {
        # Local development mode
        Write-Host "Chạy local development..." -ForegroundColor Yellow

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
        break
    }
}
