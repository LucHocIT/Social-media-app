using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

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
        
        // Configure TLS settings for secure connections
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
        
        // Configure SSL certificate validation
        ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => {
            _logger.LogInformation("SSL Certificate validation. Policy Errors: {PolicyErrors}", sslPolicyErrors);
            return true; // Accept all certificates (in production, you might want to be more selective)
        };
        
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
        _cloudinary = new Cloudinary(account) {
            Api = { Timeout = 60000 } // Increase timeout to 60 seconds
        };
    }    public async Task<CloudinaryUploadResult?> UploadImageAsync(Stream fileStream, string fileName)
    {
        const int maxRetries = 3;
        int attemptCount = 0;
        
        while (attemptCount < maxRetries)
        {
            try
            {
                attemptCount++;
                _logger.LogInformation("Attempting to upload image to Cloudinary (Attempt {AttemptCount}/{MaxRetries})", attemptCount, maxRetries);
                
                // Reset stream position if it's not at the beginning and is seekable
                if (fileStream.Position != 0 && fileStream.CanSeek)
                {
                    fileStream.Seek(0, SeekOrigin.Begin);
                }
                
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
                    
                    // If this is our last attempt, return null
                    if (attemptCount >= maxRetries)
                    {
                        return null;
                    }
                    
                    // Wait before the next retry
                    await Task.Delay(1000 * attemptCount); // Exponential backoff
                    continue;
                }

                _logger.LogInformation("Successfully uploaded image to Cloudinary: {PublicId}", uploadResult.PublicId);
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
                _logger.LogError(ex, "Error uploading image to Cloudinary (Attempt {AttemptCount}/{MaxRetries})", attemptCount, maxRetries);
                
                // If this is our last attempt, return null
                if (attemptCount >= maxRetries)
                {
                    return null;
                }
                
                // Wait before the next retry with exponential backoff
                await Task.Delay(1000 * attemptCount);
            }
        }
        
        return null;
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
