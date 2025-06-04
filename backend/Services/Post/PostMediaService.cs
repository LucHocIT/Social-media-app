using SocialApp.DTOs;
using SocialApp.Services.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace SocialApp.Services.Post;

public class PostMediaService : IPostMediaService
{
    private readonly ILogger<PostMediaService> _logger;
    private readonly ICloudinaryService _cloudinaryService;

    public PostMediaService(
        ILogger<PostMediaService> logger,
        ICloudinaryService cloudinaryService)
    {
        _logger = logger;
        _cloudinaryService = cloudinaryService;
    }

    public async Task<MultipleUploadMediaResult> UploadMultipleMediaAsync(int userId, List<IFormFile> mediaFiles, List<string> mediaTypes)
    {
        try
        {
            if (mediaFiles == null || !mediaFiles.Any())
            {
                return new MultipleUploadMediaResult
                {
                    Success = false,
                    Message = "No media files provided"
                };
            }

            if (mediaTypes.Count != mediaFiles.Count)
            {
                return new MultipleUploadMediaResult
                {
                    Success = false,
                    Message = "Media types count must match media files count"
                };
            }

            var results = new List<UploadMediaResult>();
            var failedUploads = new List<string>();

            for (int i = 0; i < mediaFiles.Count; i++)
            {
                var mediaFile = mediaFiles[i];
                var mediaType = mediaTypes[i];

                // Validate media file inline
                if (mediaFile == null || mediaFile.Length == 0)
                {
                    failedUploads.Add($"{mediaFile?.FileName ?? $"File {i+1}"}: No file uploaded");
                    continue;
                }
                
                // Validate media type parameter
                if (!IsValidMediaType(mediaType))
                {
                    failedUploads.Add($"{mediaFile.FileName}: Invalid media type. Allowed values are 'image', 'video', and 'file'.");
                    continue;
                }

                // Get allowed MIME types based on the media type
                var allowedTypes = GetAllowedMimeTypes(mediaType);
                
                // Check if the content type is allowed for the selected media type
                if (!allowedTypes.Contains(mediaFile.ContentType.ToLower()))
                {
                    failedUploads.Add($"{mediaFile.FileName}: Invalid file type for {mediaType}. Allowed types: {string.Join(", ", allowedTypes)}");
                    continue;
                }

                // Validate file size (limit varies by media type)
                long maxSize = GetMaxFileSizeForMediaType(mediaType);
                if (mediaFile.Length > maxSize)
                {
                    failedUploads.Add($"{mediaFile.FileName}: File size exceeds the maximum allowed ({maxSize / (1024 * 1024)}MB).");
                    continue;
                }

                // Upload to Cloudinary
                try
                {
                    using (var stream = mediaFile.OpenReadStream())
                    {
                        var fileName = $"{mediaType}_{userId}_{Guid.NewGuid()}";
                        CloudinaryUploadResult? uploadResult = null;
                        
                        switch (mediaType.ToLower())
                        {
                            case "image":
                                uploadResult = await _cloudinaryService.UploadImageAsync(stream, fileName);
                                break;
                            case "video":
                                uploadResult = await _cloudinaryService.UploadVideoAsync(stream, fileName);
                                break;
                            case "file":
                                uploadResult = await _cloudinaryService.UploadFileAsync(stream, fileName);
                                break;
                        }

                        if (uploadResult == null)
                        {
                            failedUploads.Add($"{mediaFile.FileName}: Failed to upload {mediaType}");
                            continue;
                        }

                        var result = new UploadMediaResult
                        {
                            Success = true,
                            MediaUrl = uploadResult.Url,
                            PublicId = uploadResult.PublicId,
                            Width = uploadResult.Width,
                            Height = uploadResult.Height,
                            Format = uploadResult.Format,
                            Duration = uploadResult.Duration,
                            FileSize = uploadResult.FileSize,
                            ResourceType = uploadResult.ResourceType,
                            MediaType = mediaType, // Use the simplified media type ("image", "video", "file")
                            MediaFilename = mediaFile.FileName,
                            Message = $"{mediaType} uploaded successfully"
                        };
                        results.Add(result);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading {MediaType} file {FileName} for user {UserId}", mediaType, mediaFile.FileName, userId);
                    failedUploads.Add($"{mediaFile.FileName}: {ex.Message}");
                }
            }

            var allSuccess = results.Count == mediaFiles.Count;
            var message = allSuccess 
                ? $"Successfully uploaded {results.Count} media files"
                : $"Uploaded {results.Count} out of {mediaFiles.Count} files. Failed: {string.Join(", ", failedUploads)}";

            return new MultipleUploadMediaResult
            {
                Success = allSuccess,
                Message = message,
                Results = results
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading multiple media files for user {UserId}", userId);
            return new MultipleUploadMediaResult
            {
                Success = false,
                Message = $"An error occurred: {ex.Message}",
                Results = new List<UploadMediaResult>()
            };
        }
    }

    public string GetMimeTypeForMediaType(string mediaType, string url)
    {
        if (string.IsNullOrEmpty(mediaType) || string.IsNullOrEmpty(url))
            return "application/octet-stream"; // Default MIME type
            
        // Extract file extension from URL
        string extension = System.IO.Path.GetExtension(url).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension))
            return "application/octet-stream";
        
        extension = extension.TrimStart('.');
            
        switch (mediaType?.ToLower())
        {
            case "image":
                switch (extension)
                {
                    case "jpg": case "jpeg": return "image/jpeg";
                    case "png": return "image/png";
                    case "gif": return "image/gif";
                    case "webp": return "image/webp";
                    case "svg": return "image/svg+xml";
                    case "bmp": return "image/bmp";
                    case "tiff": return "image/tiff";
                    default: return "image/jpeg"; // Default image type
                }
                
            case "video":
                switch (extension)
                {
                    case "mp4": return "video/mp4";
                    case "mpeg": return "video/mpeg";
                    case "mov": return "video/quicktime";
                    case "avi": return "video/x-msvideo";
                    case "wmv": return "video/x-ms-wmv";
                    case "webm": return "video/webm";
                    case "flv": return "video/x-flv";
                    default: return "video/mp4"; // Default video type
                }
                
            case "file":
                switch (extension)
                {
                    case "pdf": return "application/pdf";
                    case "doc": case "docx": return "application/msword";
                    case "xls": case "xlsx": return "application/vnd.ms-excel";
                    case "ppt": case "pptx": return "application/vnd.ms-powerpoint";
                    case "txt": return "text/plain";
                    case "zip": return "application/zip";
                    case "rar": return "application/x-rar-compressed";
                    default: return "application/octet-stream";
                }
                
            default:
                return "application/octet-stream";
        }
    }

    public bool IsValidMediaType(string mediaType)
    {
        if (string.IsNullOrEmpty(mediaType))
            return false;
            
        var validTypes = new[] { "image", "video", "file" };
        return validTypes.Contains(mediaType.ToLower());
    }

    public string[] GetAllowedMimeTypes(string mediaType)
    {
        switch (mediaType.ToLower())
        {
            case "image":
                return new[] { 
                    "image/jpeg", "image/png", "image/gif", "image/webp", 
                    "image/svg+xml", "image/bmp", "image/tiff" 
                };
            case "video":
                return new[] { 
                    "video/mp4", "video/mpeg", "video/quicktime", "video/x-msvideo", 
                    "video/x-ms-wmv", "video/webm", "video/x-flv" 
                };
            case "file":
                return new[] { 
                    "application/pdf", "application/msword", "application/vnd.ms-excel",
                    "application/vnd.ms-powerpoint", "text/plain", "application/zip",
                    "application/x-rar-compressed", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "application/vnd.openxmlformats-officedocument.presentationml.presentation"
                };
            default:
                return Array.Empty<string>();
        }
    }

    public long GetMaxFileSizeForMediaType(string mediaType)
    {
        switch (mediaType.ToLower())
        {
            case "image":
                return 10 * 1024 * 1024; // 10 MB for images
            case "video":
                return 100 * 1024 * 1024; // 100 MB for videos
            case "file":
                return 25 * 1024 * 1024; // 25 MB for other files
            default:
                return 5 * 1024 * 1024; // 5 MB default
        }
    }
}
