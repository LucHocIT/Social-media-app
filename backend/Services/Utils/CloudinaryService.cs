using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Security.Authentication;

namespace SocialApp.Services.Utils;

public interface ICloudinaryService
{
    Task<CloudinaryUploadResult?> UploadImageAsync(Stream fileStream, string fileName);
    Task<CloudinaryUploadResult?> UploadVideoAsync(Stream fileStream, string fileName);
    Task<CloudinaryUploadResult?> UploadFileAsync(Stream fileStream, string fileName);
    Task<bool> DeleteMediaAsync(string publicId);
}

public class CloudinaryUploadResult
{
    public string? Url { get; set; }
    public string? PublicId { get; set; }
    public string? Format { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long Duration { get; set; }  // For videos (in seconds)
    public long FileSize { get; set; }  // Size in bytes
    public string? ResourceType { get; set; } // "image", "video", or "raw"
    public string? MediaType { get; set; } // MIME type
}

public class CloudinaryService : ICloudinaryService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryService> _logger;

    public CloudinaryService(IConfiguration configuration, ILogger<CloudinaryService> logger)
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

        // Configure HttpClient with proper SSL/TLS settings
        var httpClientHandler = new HttpClientHandler
        {
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true // For development only
        };

        var httpClient = new HttpClient(httpClientHandler);
        
        // Set up Cloudinary instance with custom HttpClient
        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account)
        {
            Api = { 
                Timeout = 60000, // 60 seconds timeout
                HttpClient = httpClient
            }
        };
    }    public async Task<CloudinaryUploadResult?> UploadImageAsync(Stream fileStream, string fileName)
    {
        const int maxRetries = 3;
        int attemptCount = 0;
        
        // Create a copy of the stream that we can reuse for retries
        byte[] fileBytes;
        using (var memoryStream = new MemoryStream())
        {
            await fileStream.CopyToAsync(memoryStream);
            fileBytes = memoryStream.ToArray();
        }
        
        while (attemptCount < maxRetries)
        {
            attemptCount++;
            _logger.LogInformation("Attempting to upload image to Cloudinary (Attempt {AttemptCount}/{MaxRetries})", attemptCount, maxRetries);
            
            // Create a new memory stream for each attempt
            using var uploadStream = new MemoryStream(fileBytes);
            
            try
            {
                // Prepare upload parameters
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(fileName, uploadStream),
                    Folder = "images",
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = true,
                    Transformation = new Transformation()
                        .Width(1200)
                        .Height(1200)
                        .Crop("limit")
                        .Quality("auto")
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
                    
                    // Wait before the next retry with exponential backoff
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attemptCount - 1)));
                    continue;
                }

                _logger.LogInformation("Successfully uploaded image to Cloudinary: {PublicId}", uploadResult.PublicId);
                
                // Determine image MIME type based on format
                string mediaType = "image/jpeg"; // Default
                if (!string.IsNullOrEmpty(uploadResult.Format))
                {
                    switch (uploadResult.Format.ToLowerInvariant())
                    {
                        case "png": mediaType = "image/png"; break;
                        case "gif": mediaType = "image/gif"; break;
                        case "webp": mediaType = "image/webp"; break;
                        case "svg": mediaType = "image/svg+xml"; break;
                        case "bmp": mediaType = "image/bmp"; break;
                        case "tiff": mediaType = "image/tiff"; break;
                    }
                }
                
                return new CloudinaryUploadResult
                {
                    Url = uploadResult.SecureUrl.ToString(),
                    PublicId = uploadResult.PublicId,
                    Format = uploadResult.Format,
                    Width = uploadResult.Width,
                    Height = uploadResult.Height,
                    FileSize = uploadResult.Bytes,
                    ResourceType = "image",
                    MediaType = mediaType
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
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attemptCount - 1)));
            }
        }
        
        return null;
    }public async Task<CloudinaryUploadResult?> UploadVideoAsync(Stream fileStream, string fileName)
    {
        const int maxRetries = 3;
        int attemptCount = 0;
        
        // Create a copy of the stream that we can reuse for retries
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;
        
        while (attemptCount < maxRetries)
        {
            try
            {
                attemptCount++;
                _logger.LogInformation("Attempting to upload video to Cloudinary (Attempt {AttemptCount}/{MaxRetries})", attemptCount, maxRetries);
                
                // Reset memory stream position for each attempt
                memoryStream.Position = 0;
                
                // Prepare upload parameters
                var uploadParams = new VideoUploadParams
                {
                    File = new FileDescription(fileName, memoryStream),
                    Folder = "videos",
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = true,                    // Optional transformations for videos
                    EagerTransforms = new List<Transformation>()
                    {
                        new Transformation().Quality("auto").FetchFormat("mp4")
                    },
                    EagerAsync = true
                };

                // Upload video to Cloudinary
                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.Error != null)
                {
                    _logger.LogError("Cloudinary video upload error: {ErrorMessage}", uploadResult.Error.Message);
                    
                    // If this is our last attempt, return null
                    if (attemptCount >= maxRetries)
                    {
                        return null;
                    }
                    
                    // Wait before the next retry
                    await Task.Delay(1000 * attemptCount); // Exponential backoff
                    continue;
                }

                _logger.LogInformation("Successfully uploaded video to Cloudinary: {PublicId}", uploadResult.PublicId);
                return new CloudinaryUploadResult
                {
                    Url = uploadResult.SecureUrl.ToString(),
                    PublicId = uploadResult.PublicId,
                    Format = uploadResult.Format,                    Width = uploadResult.Width,
                    Height = uploadResult.Height,
                    Duration = (long)uploadResult.Duration,
                    FileSize = uploadResult.Bytes,
                    ResourceType = "video",
                    MediaType = uploadResult.Format
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading video to Cloudinary (Attempt {AttemptCount}/{MaxRetries})", attemptCount, maxRetries);
                
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
    }public async Task<CloudinaryUploadResult?> UploadFileAsync(Stream fileStream, string fileName)
    {
        const int maxRetries = 3;
        int attemptCount = 0;
        
        // Create a copy of the stream that we can reuse for retries
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;
        
        while (attemptCount < maxRetries)
        {
            try
            {
                attemptCount++;
                _logger.LogInformation("Attempting to upload file to Cloudinary (Attempt {AttemptCount}/{MaxRetries})", attemptCount, maxRetries);
                
                // Reset memory stream position for each attempt
                memoryStream.Position = 0;
                
                // Prepare upload parameters
                var uploadParams = new RawUploadParams
                {
                    File = new FileDescription(fileName, memoryStream),
                    Folder = "files",
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = true
                };

                // Upload file to Cloudinary
                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.Error != null)
                {
                    _logger.LogError("Cloudinary file upload error: {ErrorMessage}", uploadResult.Error.Message);
                    
                    // If this is our last attempt, return null
                    if (attemptCount >= maxRetries)
                    {
                        return null;
                    }
                    
                    // Wait before the next retry
                    await Task.Delay(1000 * attemptCount); // Exponential backoff
                    continue;
                }

                _logger.LogInformation("Successfully uploaded file to Cloudinary: {PublicId}", uploadResult.PublicId);
                
                // Determine file type from extension
                string mediaType = "application/octet-stream"; // Default
                string extension = Path.GetExtension(fileName).ToLowerInvariant();
                if (!string.IsNullOrEmpty(extension))
                {
                    // Common file types
                    switch (extension)
                    {
                        case ".pdf": mediaType = "application/pdf"; break;
                        case ".doc": case ".docx": mediaType = "application/msword"; break;
                        case ".xls": case ".xlsx": mediaType = "application/vnd.ms-excel"; break;
                        case ".ppt": case ".pptx": mediaType = "application/vnd.ms-powerpoint"; break;
                        case ".txt": mediaType = "text/plain"; break;
                        case ".zip": mediaType = "application/zip"; break;
                        case ".rar": mediaType = "application/x-rar-compressed"; break;
                    }
                }
                
                return new CloudinaryUploadResult
                {
                    Url = uploadResult.SecureUrl.ToString(),                    PublicId = uploadResult.PublicId,
                    Format = extension.TrimStart('.'),
                    FileSize = uploadResult.Bytes,
                    ResourceType = "raw",
                    MediaType = mediaType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to Cloudinary (Attempt {AttemptCount}/{MaxRetries})", attemptCount, maxRetries);
                
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
    }public async Task<bool> DeleteMediaAsync(string publicId)
    {
        try
        {
            var deleteParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deleteParams);

            if (result.Error != null)
            {
                _logger.LogError("Error deleting media from Cloudinary: {ErrorMessage}", result.Error.Message);
                return false;
            }

            return result.Result == "ok";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting media from Cloudinary");
            return false;
        }
    }
}
