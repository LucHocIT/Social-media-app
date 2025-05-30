# Script để dừng Redis cho SocialApp
Write-Host "Stopping Redis container..." -ForegroundColor Yellow

$containerExists = docker ps --filter "name=redis-socialapp" --format "{{.Names}}"

if ($containerExists) {
    docker stop redis-socialapp
    Write-Host "Redis stopped successfully! ✅" -ForegroundColor Green
} else {
    Write-Host "Redis container is not running." -ForegroundColor Yellow
}
