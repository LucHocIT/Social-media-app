using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;

namespace SocialApp.Services.Utils;

public interface ICloudinaryService
{
    Task<CloudinaryUploadResult?> UploadImageAsync(Stream fileStream, string fileName);
    Task<bool> DeleteImageAsync(string publicId);
}

public class CloudinaryUploadResult
{
    public string? Url { get; set; }
    public string? PublicId { get; set; }
    public string? Format { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class CloudinaryService : ICloudinaryService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryService> _logger;    public CloudinaryService(IConfiguration configuration, ILogger<CloudinaryService> logger)
    {
        _logger = logger;
        
        // Try to get Cloudinary settings from environment variables first
        var cloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME");
        var apiKey = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY");
        var apiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET");
        
        // Fall back to configuration if environment variables are not set
        if (string.IsNullOrEmpty(cloudName))
            cloudName = configuration["Cloudinary:CloudName"];
            
        if (string.IsNullOrEmpty(apiKey))
            apiKey = configuration["Cloudinary:ApiKey"];
            
        if (string.IsNullOrEmpty(apiSecret))
            apiSecret = configuration["Cloudinary:ApiSecret"];

        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            _logger.LogError("Cloudinary configuration is missing or incomplete");
            throw new InvalidOperationException("Cloudinary configuration is missing or incomplete");
        }

        // Set up Cloudinary instance
        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
    }    public async Task<CloudinaryUploadResult?> UploadImageAsync(Stream fileStream, string fileName)
    {
        try
        {
            // Prepare upload parameters
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, fileStream),
                Folder = "profiles",
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = true,
                Transformation = new Transformation()
                    .Width(500)
                    .Height(500)
                    .Crop("fill")
                    .Gravity("face")
            };

            // Upload image to Cloudinary
            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                _logger.LogError("Cloudinary upload error: {ErrorMessage}", uploadResult.Error.Message);
                return null;
            }

            return new CloudinaryUploadResult
            {
                Url = uploadResult.SecureUrl.ToString(),
                PublicId = uploadResult.PublicId,
                Format = uploadResult.Format,
                Width = uploadResult.Width,
                Height = uploadResult.Height
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image to Cloudinary");
            return null;
        }
    }public async Task<bool> DeleteImageAsync(string publicId)
    {
        try
        {
            var deleteParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deleteParams);

            if (result.Error != null)
            {
                _logger.LogError("Error deleting image from Cloudinary: {ErrorMessage}", result.Error.Message);
                return false;
            }

            return result.Result == "ok";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image from Cloudinary");
            return false;
        }
    }
}
