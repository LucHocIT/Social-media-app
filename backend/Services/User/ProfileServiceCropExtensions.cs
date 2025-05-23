using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SocialApp.DTOs;
using SocialApp.Services.Utils;
using System.Text;

namespace SocialApp.Services.User;

public partial class ProfileService
{
    public async Task<UploadProfilePictureResult> UploadCroppedProfilePictureAsync(int userId, IFormFile profilePicture, string cropDataJson)
    {
        var result = new UploadProfilePictureResult { Success = false };
        
        if (profilePicture == null)
        {
            result.Message = "No file uploaded";
            return result;
        }

        try
        {
            // Parse crop data from JSON
            CropData? cropData = null;
            if (!string.IsNullOrEmpty(cropDataJson))
            {
                try
                {
                    cropData = JsonConvert.DeserializeObject<CropData>(cropDataJson);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing crop data JSON");
                    result.Message = "Invalid crop data format";
                    return result;
                }
            }

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

            // Upload image to Cloudinary with crop transformation
            using (var stream = profilePicture.OpenReadStream())
            {
                CloudinaryUploadResult? uploadResult;
                
                if (cropData != null)
                {
                    // Apply crop transformation
                    var transformationParams = new Dictionary<string, string>();
                    
                    // Add crop transformation
                    if (cropData.Crop != null)
                    {
                        // For circular crop
                        if (cropData.CircularCrop)
                        {
                            transformationParams.Add("radius", "max");
                            transformationParams.Add("gravity", "auto");
                        }
                        
                        // Crop coordinates and dimensions
                        transformationParams.Add("crop", "crop");
                        transformationParams.Add("x", cropData.Crop.X.ToString());
                        transformationParams.Add("y", cropData.Crop.Y.ToString());
                        transformationParams.Add("width", cropData.Crop.Width.ToString());
                        transformationParams.Add("height", cropData.Crop.Height.ToString());
                    }
                    
                    // Add rotation if specified
                    if (cropData.Rotate != 0)
                    {
                        transformationParams.Add("angle", cropData.Rotate.ToString());
                    }
                    
                    // Add scaling if specified
                    if (cropData.Scale != 1.0)
                    {
                        transformationParams.Add("zoom", cropData.Scale.ToString());
                    }
                    
                    uploadResult = await _cloudinaryService.UploadImageWithTransformationAsync(
                        stream, 
                        profilePicture.FileName,
                        transformationParams
                    );
                }
                else
                {
                    // Upload without transformation
                    uploadResult = await _cloudinaryService.UploadImageAsync(stream, profilePicture.FileName);
                }
                
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
                        await _cloudinaryService.DeleteMediaAsync(publicId);
                    }
                }
                
                // Update user profile with new image URL
                user.ProfilePictureUrl = uploadResult.Url;
                user.LastActive = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                
                result.Success = true;
                result.Message = "Profile picture uploaded and cropped successfully";
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
            _logger.LogError(ex, "Error uploading cropped profile picture for user {UserId}", userId);
            result.Message = "An error occurred while uploading the file";
            return result;
        }
    }
}

// Class to hold crop data from frontend
public class CropData
{
    public CropInfo? Crop { get; set; }
    public double Scale { get; set; } = 1.0;
    public int Rotate { get; set; } = 0;
    public double Aspect { get; set; } = 1.0;
    public bool CircularCrop { get; set; } = true;
}

public class CropInfo
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Unit { get; set; } = "%";
}
