using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SocialApp.Services.Utils;

public interface ICloudinaryService
{
    Task<CloudinaryUploadResult?> UploadImageAsync(Stream fileStream, string fileName);
    Task<CloudinaryUploadResult?> UploadImageWithTransformationAsync(Stream fileStream, string fileName, Dictionary<string, string> transformations);
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
    private readonly HttpClient _httpClient;

    public CloudinaryService(IConfiguration configuration, ILogger<CloudinaryService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("CloudinaryClient");
        
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
          // Set up Cloudinary instance with enhanced configuration
        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
        
        // Configure Cloudinary settings for better reliability
        _cloudinary.Api.Timeout = 300000; // 5 minutes timeout for large file uploads
    }public async Task<CloudinaryUploadResult?> UploadImageAsync(Stream fileStream, string fileName)
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
            }            catch (Exception ex)
            {
                bool shouldRetry = ShouldRetryOnException(ex);
                _logger.LogError(ex, "Error uploading image to Cloudinary (Attempt {AttemptCount}/{MaxRetries}). Will retry: {ShouldRetry}", 
                    attemptCount, maxRetries, shouldRetry);
                
                // If this is our last attempt, return null
                if (attemptCount >= maxRetries)
                {
                    return null;
                }
                
                // Wait before the next retry with exponential backoff plus jitter
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attemptCount - 1) + Random.Shared.NextDouble());
                await Task.Delay(delay);
            }
        }
        
        return null;
    }public async Task<CloudinaryUploadResult?> UploadImageWithTransformationAsync(Stream fileStream, string fileName, Dictionary<string, string> transformations)
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
            _logger.LogInformation("Attempting to upload image with transformations to Cloudinary (Attempt {AttemptCount}/{MaxRetries})", attemptCount, maxRetries);
            
            // Create a new memory stream for each attempt
            using var uploadStream = new MemoryStream(fileBytes);
            
            try
            {
                // Prepare upload parameters with custom transformations
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(fileName, uploadStream),
                    Folder = "profiles", // Đặt folder profiles cho ảnh đại diện
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = true
                };
                
                // Add custom transformations if provided
                var transformation = new Transformation();
                foreach (var param in transformations)
                {
                    transformation.Add(param.Key, param.Value);
                }
                
                // Apply the transformations
                uploadParams.Transformation = transformation;

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

                _logger.LogInformation("Successfully uploaded transformed image to Cloudinary: {PublicId}", uploadResult.PublicId);

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
            }            catch (Exception ex)
            {
                bool shouldRetry = ShouldRetryOnException(ex);
                _logger.LogError(ex, "Error uploading transformed image to Cloudinary (Attempt {AttemptCount}/{MaxRetries}). Will retry: {ShouldRetry}", 
                    attemptCount, maxRetries, shouldRetry);
                
                // If this is our last attempt, return null
                if (attemptCount >= maxRetries)
                {
                    return null;
                }
                
                // Wait before the next retry with exponential backoff plus jitter
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attemptCount - 1) + Random.Shared.NextDouble());
                await Task.Delay(delay);
            }
        }
        
        return null; // If we reach here, all attempts failed
    }    public async Task<CloudinaryUploadResult?> UploadVideoAsync(Stream fileStream, string fileName)
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
            _logger.LogInformation("Attempting to upload video to Cloudinary (Attempt {AttemptCount}/{MaxRetries})", attemptCount, maxRetries);
            
            // Create a new memory stream for each attempt
            using var uploadStream = new MemoryStream(fileBytes);
            
            try
            {                // Prepare upload parameters
                var uploadParams = new VideoUploadParams
                {
                    File = new FileDescription(fileName, uploadStream),
                    Folder = "videos",
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = true,
                    // Generate thumbnail and optimize video
                    EagerTransforms = new List<Transformation>()
                    {
                        new Transformation().Quality("auto"),
                        new Transformation().Width(300).Height(200).Crop("fill") // Thumbnail
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

                _logger.LogInformation("Successfully uploaded video to Cloudinary: {PublicId}", uploadResult.PublicId);                // Determine video MIME type based on format
                string mediaType = "video/mp4"; // Default
                if (!string.IsNullOrEmpty(uploadResult.Format))
                {
                    switch (uploadResult.Format.ToLowerInvariant())
                    {
                        case "mp4": mediaType = "video/mp4"; break;
                        case "avi": mediaType = "video/avi"; break;
                        case "mov": mediaType = "video/quicktime"; break;
                        case "wmv": mediaType = "video/x-ms-wmv"; break;
                        case "flv": mediaType = "video/x-flv"; break;
                        case "webm": mediaType = "video/webm"; break;
                        case "mkv": mediaType = "video/x-matroska"; break;
                    }
                }

                return new CloudinaryUploadResult
                {
                    Url = uploadResult.SecureUrl.ToString(),
                    PublicId = uploadResult.PublicId,
                    Format = uploadResult.Format,
                    Width = uploadResult.Width,
                    Height = uploadResult.Height,
                    Duration = (long)uploadResult.Duration,
                    FileSize = uploadResult.Bytes,
                    ResourceType = "video",
                    MediaType = mediaType
                };
            }            catch (Exception ex)
            {
                bool shouldRetry = ShouldRetryOnException(ex);
                _logger.LogError(ex, "Error uploading video to Cloudinary (Attempt {AttemptCount}/{MaxRetries}). Will retry: {ShouldRetry}", 
                    attemptCount, maxRetries, shouldRetry);
                
                // If this is our last attempt, return null
                if (attemptCount >= maxRetries)
                {
                    return null;
                }
                
                // Wait before the next retry with exponential backoff plus jitter
                var delay = TimeSpan.FromMilliseconds(1000 * attemptCount + Random.Shared.Next(0, 500));
                await Task.Delay(delay);
            }
        }
          return null;
    }

    public async Task<CloudinaryUploadResult?> UploadFileAsync(Stream fileStream, string fileName)
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
            _logger.LogInformation("Attempting to upload file to Cloudinary (Attempt {AttemptCount}/{MaxRetries})", attemptCount, maxRetries);
            
            // Create a new memory stream for each attempt
            using var uploadStream = new MemoryStream(fileBytes);
            
            try
            {
                  // Prepare upload parameters
                var uploadParams = new RawUploadParams
                {
                    File = new FileDescription(fileName, uploadStream),
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
            }            catch (Exception ex)
            {
                bool shouldRetry = ShouldRetryOnException(ex);
                LogConnectionDiagnostics(ex, attemptCount);
                _logger.LogError(ex, "Error uploading file to Cloudinary (Attempt {AttemptCount}/{MaxRetries}). Will retry: {ShouldRetry}", 
                    attemptCount, maxRetries, shouldRetry);
                
                // If this is our last attempt, return null
                if (attemptCount >= maxRetries)
                {
                    return null;
                }
                
                // Wait before the next retry with exponential backoff
                // Add jitter to prevent thundering herd
                var delay = TimeSpan.FromMilliseconds(1000 * attemptCount + Random.Shared.Next(0, 500));
                await Task.Delay(delay);
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

    // Helper method to determine if an exception warrants a retry
    private static bool ShouldRetryOnException(Exception ex)
    {
        return ex switch
        {
            HttpRequestException httpEx when httpEx.Message.Contains("SSL connection") => true,
            HttpRequestException httpEx when httpEx.Message.Contains("timeout") => true,
            HttpRequestException httpEx when httpEx.Message.Contains("connection was forcibly closed") => true,
            IOException ioEx when ioEx.Message.Contains("transport connection") => true,
            System.Net.Sockets.SocketException => true,
            TaskCanceledException => true,
            TimeoutException => true,
            _ => true // Retry on any other exception for now
        };
    }

    // Helper method to log detailed connection diagnostics
    private void LogConnectionDiagnostics(Exception ex, int attemptCount)
    {
        var innerEx = ex.InnerException;
        _logger.LogWarning("Cloudinary connection attempt {AttemptCount} failed. Exception Type: {ExceptionType}", 
            attemptCount, ex.GetType().Name);
        
        if (innerEx != null)
        {
            _logger.LogWarning("Inner exception: {InnerExceptionType} - {InnerMessage}", 
                innerEx.GetType().Name, innerEx.Message);
            
            if (innerEx.InnerException != null)
            {
                _logger.LogWarning("Deepest inner exception: {DeepestExceptionType} - {DeepestMessage}", 
                    innerEx.InnerException.GetType().Name, innerEx.InnerException.Message);
            }
        }

        // Log network connectivity suggestions
        if (ex.Message.Contains("SSL connection") || ex.Message.Contains("forcibly closed"))
        {
            _logger.LogInformation("SSL connection issue detected. This might be due to:");
            _logger.LogInformation("1. Network firewall blocking HTTPS connections");
            _logger.LogInformation("2. Corporate proxy settings");
            _logger.LogInformation("3. DNS resolution issues");
            _logger.LogInformation("4. TLS version compatibility");
        }
    }
}
