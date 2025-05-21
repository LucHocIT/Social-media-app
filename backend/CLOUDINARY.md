# Cloudinary Integration

## Overview

The application now uses Cloudinary to store profile pictures instead of saving them locally in the wwwroot folder. This change provides the following benefits:

1. **Better scalability** - Images are stored in the cloud instead of on the application server
2. **Improved performance** - Cloudinary handles image optimization and CDN delivery
3. **Enhanced functionality** - Automatic image transformations and optimizations

## Configuration

To use Cloudinary, you must set up the following configuration in your `appsettings.json` and `appsettings.Development.json` files:

```json
"Cloudinary": {
  "CloudName": "your_cloud_name",
  "ApiKey": "your_api_key",
  "ApiSecret": "your_api_secret"
}
```

You can get these values by signing up for a free Cloudinary account at [https://cloudinary.com/](https://cloudinary.com/).

## Implementation Details

- Profile pictures are now stored in the "profiles" folder in your Cloudinary account
- Images are automatically resized to 500x500 pixels with face detection for better cropping
- When a user updates their profile picture, the old image is automatically deleted from Cloudinary

## Migrating Existing Images

If you have existing profile pictures stored in wwwroot, you'll need to migrate them to Cloudinary. This can be done by:

1. Writing a migration script to upload all existing images to Cloudinary
2. Updating the database records to point to the new Cloudinary URLs

For assistance with migration, please contact the development team.

## Technical Notes

The implementation uses the CloudinaryDotNet NuGet package to interact with the Cloudinary API. The core services are:

- `CloudinaryService.cs` - Handles image upload and deletion operations
- `ProfileController.cs` - Updated to use Cloudinary for profile picture management
