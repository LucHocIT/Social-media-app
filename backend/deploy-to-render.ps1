# Deploy Backend to Render
# This script helps prepare the backend for deployment to Render

Write-Host "Preparing backend for Render deployment..." -ForegroundColor Green

# Check if we're in the correct directory
if (!(Test-Path "SocialApp.csproj")) {
    Write-Host "Error: Please run this script from the backend directory" -ForegroundColor Red
    exit 1
}

# Build the project to check for errors
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed! Please fix the errors before deploying." -ForegroundColor Red
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green

# Check if render.yaml exists
if (!(Test-Path "render.yaml")) {
    Write-Host "Error: render.yaml not found!" -ForegroundColor Red
    exit 1
}

Write-Host "render.yaml found!" -ForegroundColor Green

# Check if Dockerfile exists
if (!(Test-Path "Dockerfile")) {
    Write-Host "Error: Dockerfile not found!" -ForegroundColor Red
    exit 1
}

Write-Host "Dockerfile found!" -ForegroundColor Green

# Display next steps
Write-Host "" -ForegroundColor White
Write-Host "Next steps to deploy to Render:" -ForegroundColor Cyan
Write-Host "1. Commit and push your changes to GitHub" -ForegroundColor White
Write-Host "2. Go to https://dashboard.render.com/" -ForegroundColor White
Write-Host "3. Click 'New' -> 'Web Service'" -ForegroundColor White
Write-Host "4. Connect your GitHub repository" -ForegroundColor White
Write-Host "5. Configure the service:" -ForegroundColor White
Write-Host "   - Name: socialapp-backend" -ForegroundColor Gray
Write-Host "   - Runtime: Docker" -ForegroundColor Gray
Write-Host "   - Root Directory: backend" -ForegroundColor Gray
Write-Host "   - Region: Singapore" -ForegroundColor Gray
Write-Host "6. Set environment variables (see DEPLOY.md for details)" -ForegroundColor White
Write-Host "7. Create a PostgreSQL database" -ForegroundColor White
Write-Host "8. Deploy!" -ForegroundColor White

Write-Host "" -ForegroundColor White
Write-Host "Important environment variables to set:" -ForegroundColor Cyan
Write-Host "- ASPNETCORE_ENVIRONMENT=Production" -ForegroundColor Gray
Write-Host "- ASPNETCORE_URLS=http://+:5000" -ForegroundColor Gray
Write-Host "- ConnectionStrings__DefaultConnection=(PostgreSQL connection string)" -ForegroundColor Gray
Write-Host "- Jwt__Key=(Strong secret key)" -ForegroundColor Gray
Write-Host "- EmailSettings__Username=(Your email)" -ForegroundColor Gray
Write-Host "- EmailSettings__Password=(Your app password)" -ForegroundColor Gray
Write-Host "- EmailSettings__SenderEmail=(Your email)" -ForegroundColor Gray

Write-Host "" -ForegroundColor White
Write-Host "Your backend URL will be: https://socialapp-backend.onrender.com" -ForegroundColor Green
Write-Host "For detailed instructions, see DEPLOY.md" -ForegroundColor Yellow

Write-Host "" -ForegroundColor White
Write-Host "Ready to deploy!" -ForegroundColor Green
