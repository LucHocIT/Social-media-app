# 🚀 Render Deployment Checklist

## ✅ Files Ready
- [x] `render.yaml` - Render configuration
- [x] `Dockerfile` - Docker configuration  
- [x] `appsettings.Production.json` - Production settings
- [x] `DEPLOY.md` - Deployment guide
- [x] `deploy-to-render.ps1` - Deployment script

## 🎯 Deployment Steps

### 1. Push to GitHub
```bash
git add .
git commit -m "Add Render deployment configuration"
git push origin main
```

### 2. Create Render Service
1. Go to [Render Dashboard](https://dashboard.render.com/)
2. Click "New" -> "Web Service"
3. Connect GitHub repository
4. Configure:
   - **Name**: `socialapp-backend`
   - **Runtime**: `Docker`
   - **Root Directory**: `backend`
   - **Region**: `Singapore`
   - **Plan**: `Free`

### 3. Environment Variables
Set these in Render dashboard:

**Required:**
- `ASPNETCORE_ENVIRONMENT` = `Production`
- `ASPNETCORE_URLS` = `http://+:5000`
- `ConnectionStrings__DefaultConnection` = `[PostgreSQL connection string]`
- `Jwt__Key` = `[Generate strong 32+ character secret]`

**Email (Required for auth):**
- `EmailSettings__Username` = `[Your Gmail]`
- `EmailSettings__Password` = `[Gmail app password]`
- `EmailSettings__SenderEmail` = `[Your Gmail]`

**Optional Services:**
- `Cloudinary__CloudName` = `[Your Cloudinary cloud name]`
- `Cloudinary__ApiKey` = `[Your Cloudinary API key]`
- `Cloudinary__ApiSecret` = `[Your Cloudinary API secret]`

### 4. Create Database
1. In Render dashboard: "New" -> "PostgreSQL"
2. Name: `socialapp-db`
3. Copy connection string to `ConnectionStrings__DefaultConnection`

### 5. Update Frontend
Update your frontend API URLs to:
- Backend: `https://socialapp-backend.onrender.com`

### 6. Deploy
Click "Deploy" in Render dashboard

## 🔍 Verification
- [ ] Backend URL accessible: `https://socialapp-backend.onrender.com`
- [ ] API endpoints working: `https://socialapp-backend.onrender.com/api/health`
- [ ] Database migrations applied
- [ ] Frontend can connect to backend
- [ ] Authentication working
- [ ] File uploads working (if Cloudinary configured)

## 🐛 Common Issues
- **Build fails**: Check Dockerfile and dependencies
- **Database connection**: Verify PostgreSQL connection string
- **CORS errors**: Ensure frontend URL is in CORS policy
- **Environment variables**: Double-check all required vars are set

## 📝 URLs
- **Frontend**: https://socailapp-j7s9.onrender.com
- **Backend**: https://socialapp-backend.onrender.com (after deployment)
- **Database**: Internal PostgreSQL on Render
