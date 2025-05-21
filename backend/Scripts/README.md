// filepath: e:\SocialApp\backend\Scripts\README.md
# Migration Scripts

This folder contains utility scripts for migrating data in the application.

## Migrating Profile Pictures to Cloudinary

The `MigrateProfilePictures.cs` script helps migrate existing profile pictures from the local wwwroot folder to Cloudinary cloud storage.

### How to Run

To run the migration script, you can add the following code to your `Program.cs` file temporarily:

```csharp
// After building the app
var app = builder.Build();

// Run migration script
if (args.Length > 0 && args[0] == "migrate-profile-pictures")
{
    await SocialApp.Scripts.MigrateProfilePictures.RunAsync(app);
    return;
}

// Continue with normal app configuration...
```

Then run the application with the "migrate-profile-pictures" argument:

```
dotnet run migrate-profile-pictures
```

### Important Notes

1. Make sure your Cloudinary configuration is properly set up in `appsettings.json` before running the migration
2. The script logs all activities, so you can check the application logs to track progress
3. The migration script handles errors gracefully and continues even if individual migrations fail
4. After migration is complete, remove the migration code from `Program.cs`

### Rollback Option

If you need to rollback to using local storage again:

1. Keep a backup of your wwwroot/uploads/profiles directory before removal
2. You can create a similar script that downloads images from Cloudinary and stores them locally
