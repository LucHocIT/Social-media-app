using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SocialApp.Models;
using SocialApp.Services.Utils;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SocialApp.Scripts
{
    public class MigrateProfilePictures
    {
        public static async Task RunAsync(IHost host)
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;
            var dbContext = services.GetRequiredService<SocialMediaDbContext>();
            var cloudinaryService = services.GetRequiredService<ICloudinaryService>();
            var logger = services.GetRequiredService<ILogger<MigrateProfilePictures>>();

            try
            {
                logger.LogInformation("Starting profile picture migration to Cloudinary");
                
                // Get all users with profile pictures that are not already on Cloudinary
                var users = await dbContext.Users
                    .Where(u => !u.IsDeleted && 
                           u.ProfilePictureUrl != null && 
                           !u.ProfilePictureUrl.Contains("cloudinary.com"))
                    .ToListAsync();
                
                logger.LogInformation("Found {Count} users with profile pictures to migrate", users.Count);
                
                int successCount = 0;
                int errorCount = 0;
                
                foreach (var user in users)
                {
                    try
                    {
                        // Get the file path
                        string localPath = Path.Combine(
                            Directory.GetCurrentDirectory(), 
                            "wwwroot", 
                            user.ProfilePictureUrl.TrimStart('/'));
                        
                        if (!File.Exists(localPath))
                        {
                            logger.LogWarning("Local file not found for user {UserId}: {Path}", 
                                user.Id, localPath);
                            errorCount++;
                            continue;
                        }
                        
                        // Upload to Cloudinary
                        using (var fileStream = new FileStream(localPath, FileMode.Open))
                        {
                            var filename = Path.GetFileName(localPath);
                            var uploadResult = await cloudinaryService.UploadImageAsync(fileStream, filename);
                            
                            if (uploadResult == null || string.IsNullOrEmpty(uploadResult.Url))
                            {
                                logger.LogError("Failed to upload image to Cloudinary for user {UserId}", 
                                    user.Id);
                                errorCount++;
                                continue;
                            }
                            
                            // Update user profile
                            user.ProfilePictureUrl = uploadResult.Url;
                            await dbContext.SaveChangesAsync();
                            
                            successCount++;
                            logger.LogInformation("Successfully migrated profile picture for user {UserId}", 
                                user.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error migrating profile picture for user {UserId}", user.Id);
                        errorCount++;
                    }
                }
                
                logger.LogInformation("Profile picture migration completed. Success: {SuccessCount}, Errors: {ErrorCount}", 
                    successCount, errorCount);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error running profile picture migration");
            }
        }
    }
}
