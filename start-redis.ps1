# Script để khởi động Redis cho SocialApp
Write-Host "Checking Redis container status..." -ForegroundColor Yellow

# Kiểm tra container Redis có tồn tại không
$containerExists = docker ps -a --filter "name=redis-socialapp" --format "{{.Names}}"

if ($containerExists) {
    # Kiểm tra container có đang chạy không
    $containerRunning = docker ps --filter "name=redis-socialapp" --format "{{.Names}}"
    
    if ($containerRunning) {
        Write-Host "Redis is already running!" -ForegroundColor Green
    } else {
        Write-Host "Starting existing Redis container..." -ForegroundColor Blue
        docker start redis-socialapp
        Write-Host "Redis started successfully!" -ForegroundColor Green
    }
} else {
    Write-Host "Creating new Redis container..." -ForegroundColor Blue
    docker run -d --name redis-socialapp -p 6379:6379 redis:alpine
    Write-Host "Redis container created and started!" -ForegroundColor Green
}

# Kiểm tra Redis có thể kết nối được không
Write-Host "Testing Redis connection..." -ForegroundColor Yellow
Start-Sleep -Seconds 2

$testResult = docker exec redis-socialapp redis-cli ping 2>$null

if ($testResult -eq "PONG") {
    Write-Host "Redis is ready! ✅" -ForegroundColor Green
    Write-Host "Redis is running on: localhost:6379" -ForegroundColor Cyan
} else {
    Write-Host "Redis connection test failed! ❌" -ForegroundColor Red
}
