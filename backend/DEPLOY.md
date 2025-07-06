# Deploy Backend to Render

## Prerequisites
1. Push your code to a GitHub repository
2. Have a Render account

## Steps to Deploy

### 1. Create a new Web Service on Render
1. Go to [Render Dashboard](https://dashboard.render.com/)
2. Click "New" -> "Web Service"
3. Connect your GitHub repository
4. Select your repository and branch (usually `main`)

### 2. Configure the Web Service
- **Name**: `socialapp-backend`
- **Runtime**: `Docker`
- **Region**: `Singapore` (or closest to your users)
- **Branch**: `main`
- **Root Directory**: `backend`
- **Dockerfile Path**: `./Dockerfile`

### 3. Environment Variables
Add these environment variables in Render:

**Required:**
- `ASPNETCORE_ENVIRONMENT` = `Production`
- `ASPNETCORE_URLS` = `http://+:5000`
- `ConnectionStrings__DefaultConnection` = (PostgreSQL connection string from Render database)
- `Jwt__Key` = (Generate a strong secret key)

**Email Settings:**
- `EmailSettings__Username` = (Your Gmail address)
- `EmailSettings__Password` = (Your Gmail app password)
- `EmailSettings__SenderEmail` = (Your Gmail address)

**Optional Services:**
- `Cloudinary__CloudName` = (Your Cloudinary cloud name)
- `Cloudinary__ApiKey` = (Your Cloudinary API key)
- `Cloudinary__ApiSecret` = (Your Cloudinary API secret)
- `Authentication__Facebook__AppId` = (Your Facebook App ID)
- `Authentication__Facebook__AppSecret` = (Your Facebook App Secret)
- `Authentication__Google__ClientId` = (Your Google Client ID)
- `Authentication__Google__ClientSecret` = (Your Google Client Secret)

### 4. Create Database
1. In Render dashboard, create a new PostgreSQL database
2. Use the database connection string for `ConnectionStrings__DefaultConnection`

### 5. Update Frontend
Update your frontend to use the new backend URL:
- Production Backend URL: `https://socialapp-backend.onrender.com`

### 6. CORS Configuration
Make sure your backend allows requests from your frontend domain:
- Frontend URL: `https://socailapp-j7s9.onrender.com`

## Important Notes
- Free tier services may sleep after 15 minutes of inactivity
- Database will be automatically created and migrated on first run
- All environment variables should be set in Render dashboard
- Make sure to use HTTPS URLs in production

## Troubleshooting
- Check logs in Render dashboard for errors
- Verify all environment variables are set
- Ensure database connection string is correct
- Check CORS configuration if frontend can't connect
