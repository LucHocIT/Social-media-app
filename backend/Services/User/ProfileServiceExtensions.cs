using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SocialApp.DTOs;
using SocialApp.Models;
using SocialApp.Services.Utils;
using System.IO;

namespace SocialApp.Services.User;

public partial class ProfileService : IProfileService
{
    private readonly ICloudinaryService _cloudinaryService;
    
    // Helper method to extract Cloudinary public ID from URL
    private string? ExtractCloudinaryPublicId(string? cloudinaryUrl)
    {
        if (string.IsNullOrEmpty(cloudinaryUrl) || !cloudinaryUrl.Contains("cloudinary.com"))
        {
            return null;
        }
        
        try
        {
            // Example: https://res.cloudinary.com/mycloudname/image/upload/v1234567890/profiles/abc123.jpg
            var uri = new Uri(cloudinaryUrl);
            var pathSegments = uri.AbsolutePath.Split('/');
            
            if (pathSegments.Length >= 4)
            {
                // Get the folder and filename (without extension)
                var folder = pathSegments[pathSegments.Length - 2];
                var filename = Path.GetFileNameWithoutExtension(pathSegments[pathSegments.Length - 1]);
                
                // Return in format "folder/filename"
                return $"{folder}/{filename}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting public ID from Cloudinary URL");
        }
        
        return null;
    }
    
    public async Task<UploadProfilePictureResult> UploadProfilePictureAsync(int userId, IFormFile profilePicture)
    {
        var result = new UploadProfilePictureResult { Success = false };
        
        if (profilePicture == null)
        {
            result.Message = "No file uploaded";
            return result;
        }

        try
        {
            // Validate file type
            string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
            string fileExtension = Path.GetExtension(profilePicture.FileName).ToLower();
            
            if (!allowedExtensions.Contains(fileExtension))
            {
                result.Message = "Only jpg, jpeg, png, and gif files are allowed";
                return result;
            }

            // Get current profile picture URL to delete the old one if it exists
            var user = await _context.Users
                .Where(u => u.Id == userId && !u.IsDeleted)
                .FirstOrDefaultAsync();
                
            if (user == null)
            {
                result.Message = "User not found";
                return result;
            }

            // Upload image to Cloudinary
            using (var stream = profilePicture.OpenReadStream())
            {
                var uploadResult = await _cloudinaryService.UploadImageAsync(stream, profilePicture.FileName);
                
                if (uploadResult == null || string.IsNullOrEmpty(uploadResult.Url))
                {
                    result.Message = "Failed to upload image to Cloudinary";
                    return result;
                }
                
                // Delete old image from Cloudinary if exists
                if (!string.IsNullOrEmpty(user.ProfilePictureUrl) && 
                    user.ProfilePictureUrl.Contains("cloudinary.com"))
                {
                    // Extract public ID and delete old image from Cloudinary
                    var publicId = ExtractCloudinaryPublicId(user.ProfilePictureUrl);
                    if (!string.IsNullOrEmpty(publicId))
                    {
                        await _cloudinaryService.DeleteImageAsync(publicId);
                    }
                }
                
                // Update user profile with new image URL
                user.ProfilePictureUrl = uploadResult.Url;
                user.LastActive = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                
                result.Success = true;
                result.Message = "Profile picture uploaded successfully";
                result.ProfilePictureUrl = uploadResult.Url;
                result.PublicId = uploadResult.PublicId;
                result.Width = uploadResult.Width;
                result.Height = uploadResult.Height;
                result.Format = uploadResult.Format;
                
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading profile picture for user {UserId}", userId);
            result.Message = "An error occurred while uploading the file";
            return result;
        }
    }
    
    public async Task<bool> RemoveProfilePictureAsync(int userId)
    {
        try
        {
            // Get current profile picture URL
            var user = await _context.Users
                .Where(u => u.Id == userId && !u.IsDeleted)
                .FirstOrDefaultAsync();
                
            if (user == null)
            {
                return false;
            }
                
            if (!string.IsNullOrEmpty(user.ProfilePictureUrl) && 
                user.ProfilePictureUrl.Contains("cloudinary.com"))
            {
                // Extract public ID and delete from Cloudinary
                var publicId = ExtractCloudinaryPublicId(user.ProfilePictureUrl);
                if (!string.IsNullOrEmpty(publicId))
                {
                    await _cloudinaryService.DeleteImageAsync(publicId);
                }
            }

            user.ProfilePictureUrl = null;
            user.LastActive = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing profile picture for user {UserId}", userId);
            return false;
        }
    }
    
    public async Task<bool> UpdateProfilePictureWithUrlAsync(int userId, ProfilePictureDTO pictureDto)
    {
        if (string.IsNullOrEmpty(pictureDto.PictureUrl))
        {
            return false;
        }
        
        return await UpdateProfilePictureAsync(userId, pictureDto.PictureUrl);
    }
    
    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        try
        {
            // Get the user
            var user = await _context.Users
                .Where(u => u.Id == userId && !u.IsDeleted)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return false;
            }

            // Verify the current password
            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                return false;
            }

            // Update the password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.LastActive = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", userId);
            return false;
        }
    }
}
