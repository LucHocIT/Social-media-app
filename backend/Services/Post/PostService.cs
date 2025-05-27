using SocialApp.DTOs;
using SocialApp.Models;
using SocialApp.Services.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace SocialApp.Services.Post;

public class PostService : IPostService
{
    private readonly SocialMediaDbContext _context;
    private readonly ILogger<PostService> _logger;
    private readonly ICloudinaryService _cloudinaryService;

    public PostService(
        SocialMediaDbContext context,
        ILogger<PostService> logger,
        ICloudinaryService cloudinaryService)
    {
        _context = context;
        _logger = logger;
        _cloudinaryService = cloudinaryService;
    }    public async Task<PostResponseDTO?> CreatePostAsync(int userId, CreatePostDTO postDto)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || user.IsDeleted)
            {
                _logger.LogWarning("Attempted to create post for non-existent or deleted user: {UserId}", userId);
                return null;
            }

            var post = new Models.Post
            {
                Content = postDto.Content,
                MediaUrl = postDto.MediaUrl, // Legacy support
                MediaType = postDto.MediaType, // Legacy support
                MediaPublicId = postDto.MediaPublicId, // Legacy support
                Location = postDto.Location,
                CreatedAt = DateTime.UtcNow,
                UserId = userId
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            // Add multiple media files if provided
            if (postDto.MediaFiles != null && postDto.MediaFiles.Any())
            {
                var mediaEntities = new List<Models.PostMedia>();
                for (int i = 0; i < postDto.MediaFiles.Count; i++)
                {
                    var mediaDto = postDto.MediaFiles[i];
                    var mediaEntity = new Models.PostMedia
                    {
                        PostId = post.Id,
                        MediaUrl = mediaDto.MediaUrl,
                        MediaType = mediaDto.MediaType,
                        MediaPublicId = mediaDto.MediaPublicId,
                        MediaMimeType = mediaDto.MediaMimeType,
                        MediaFilename = mediaDto.MediaFilename,
                        MediaFileSize = mediaDto.MediaFileSize,
                        Width = mediaDto.Width,
                        Height = mediaDto.Height,
                        Duration = mediaDto.Duration,
                        OrderIndex = mediaDto.OrderIndex,
                        CreatedAt = DateTime.UtcNow
                    };
                    mediaEntities.Add(mediaEntity);
                }
                
                _context.PostMedias.AddRange(mediaEntities);
                await _context.SaveChangesAsync();
            }

            return await GetPostByIdAsync(post.Id, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating post for user {UserId}", userId);
            throw;
        }
    }    public async Task<PostResponseDTO?> UpdatePostAsync(int userId, int postId, UpdatePostDTO postDto)
    {
        try
        {
            var post = await _context.Posts
                .Include(p => p.MediaFiles)
                .FirstOrDefaultAsync(p => p.Id == postId && p.UserId == userId);

            if (post == null)
            {
                _logger.LogWarning("Post {PostId} not found for user {UserId}", postId, userId);
                return null;
            }

            post.Content = postDto.Content;
            post.MediaUrl = postDto.MediaUrl; // Legacy support
            post.MediaType = postDto.MediaType; // Legacy support
            post.MediaPublicId = postDto.MediaPublicId; // Legacy support
            post.Location = postDto.Location;
            post.UpdatedAt = DateTime.UtcNow;

            // Update multiple media files if provided
            if (postDto.MediaFiles != null)
            {
                // Remove existing media files
                _context.PostMedias.RemoveRange(post.MediaFiles);
                
                // Add new media files
                if (postDto.MediaFiles.Any())
                {
                    var mediaEntities = new List<Models.PostMedia>();
                    for (int i = 0; i < postDto.MediaFiles.Count; i++)
                    {
                        var mediaDto = postDto.MediaFiles[i];
                        var mediaEntity = new Models.PostMedia
                        {
                            PostId = post.Id,
                            MediaUrl = mediaDto.MediaUrl,
                            MediaType = mediaDto.MediaType,
                            MediaPublicId = mediaDto.MediaPublicId,
                            MediaMimeType = mediaDto.MediaMimeType,
                            MediaFilename = mediaDto.MediaFilename,
                            MediaFileSize = mediaDto.MediaFileSize,
                            Width = mediaDto.Width,
                            Height = mediaDto.Height,
                            Duration = mediaDto.Duration,
                            OrderIndex = mediaDto.OrderIndex,
                            CreatedAt = DateTime.UtcNow
                        };
                        mediaEntities.Add(mediaEntity);
                    }
                    
                    _context.PostMedias.AddRange(mediaEntities);
                }
            }

            await _context.SaveChangesAsync();
            return await GetPostByIdAsync(postId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating post {PostId} for user {UserId}", postId, userId);
            throw;
        }
    }

    public async Task<bool> DeletePostAsync(int userId, int postId)
    {
        try
        {
            var post = await _context.Posts
                .FirstOrDefaultAsync(p => p.Id == postId && p.UserId == userId);

            if (post == null)
            {
                _logger.LogWarning("Post {PostId} not found for user {UserId}", postId, userId);
                return false;
            }            // Delete associated reactions
            var reactions = await _context.Reactions.Where(r => r.PostId == postId).ToListAsync();
            _context.Reactions.RemoveRange(reactions);

            // Delete associated comments
            var comments = await _context.Comments.Where(c => c.PostId == postId).ToListAsync();
            _context.Comments.RemoveRange(comments);

            // Delete associated notifications
            var notifications = await _context.Notifications.Where(n => n.PostId == postId).ToListAsync();
            _context.Notifications.RemoveRange(notifications);

            // Delete the post
            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting post {PostId} for user {UserId}", postId, userId);
            throw;
        }
    }    public async Task<PostResponseDTO?> GetPostByIdAsync(int postId, int? currentUserId = null)
    {
        try
        {
            var post = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments)
                .Include(p => p.Reactions)
                .Include(p => p.MediaFiles)
                .FirstOrDefaultAsync(p => p.Id == postId);

            if (post == null)
            {
                _logger.LogWarning("Post {PostId} not found", postId);
                return null;
            }

            return MapPostToResponseDTO(post, currentUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving post {PostId}", postId);
            throw;
        }
    }

    public async Task<PostPagedResponseDTO> GetPostsAsync(PostFilterDTO filter, int? currentUserId = null)
    {
        try
        {            var query = _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments)
                .Include(p => p.Reactions)
                .Include(p => p.MediaFiles)
                .OrderByDescending(p => p.CreatedAt)
                .AsQueryable();

            // Filter by username if provided
            if (!string.IsNullOrEmpty(filter.Username))
            {
                query = query.Where(p => p.User.Username == filter.Username);
            }

            // Filter by following if requested and currentUserId is provided
            if (filter.OnlyFollowing == true && currentUserId.HasValue)
            {
                var followingIds = await _context.UserFollowers
                    .Where(uf => uf.FollowerId == currentUserId.Value)
                    .Select(uf => uf.FollowingId)
                    .ToListAsync();

                query = query.Where(p => followingIds.Contains(p.UserId) || p.UserId == currentUserId.Value);
            }

            // Get total count
            var totalCount = await query.CountAsync();

            // Calculate paging
            var pageSize = Math.Min(Math.Max(filter.PageSize, 1), 50); // Limit page size between 1 and 50
            var pageNumber = Math.Max(filter.PageNumber, 1);
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // Apply paging
            var posts = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Map to DTOs
            var postDtos = posts.Select(p => MapPostToResponseDTO(p, currentUserId)).ToList();

            return new PostPagedResponseDTO
            {
                Posts = postDtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                HasNextPage = pageNumber < totalPages,
                HasPreviousPage = pageNumber > 1
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving posts");
            throw;
        }
    }

    public async Task<PostPagedResponseDTO> GetPostsByUserAsync(int userId, int pageNumber = 1, int pageSize = 10, int? currentUserId = null)
    {
        try
        {
            var filter = new PostFilterDTO
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };            var query = _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments)
                .Include(p => p.Reactions)
                .Include(p => p.MediaFiles)
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var posts = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var postDtos = posts.Select(p => MapPostToResponseDTO(p, currentUserId)).ToList();

            return new PostPagedResponseDTO
            {
                Posts = postDtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                HasNextPage = pageNumber < totalPages,
                HasPreviousPage = pageNumber > 1
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving posts for user {UserId}", userId);
            throw;        }
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
            var failedUploads = new List<string>();            for (int i = 0; i < mediaFiles.Count; i++)
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
                        }                        var result = new UploadMediaResult
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

    // Helper method to map Post entity to PostResponseDTO
    private PostResponseDTO MapPostToResponseDTO(Models.Post post, int? currentUserId)
    {        bool hasReacted = false;
        string? currentUserReactionType = null;
        var reactionCounts = new Dictionary<string, int>();
        
        // Get reaction counts by type
        if (post.Reactions != null && post.Reactions.Any())
        {
            reactionCounts = post.Reactions
                .GroupBy(r => r.ReactionType)
                .ToDictionary(g => g.Key, g => g.Count());
                
            // Check if current user has reacted
            if (currentUserId.HasValue)
            {
                var userReaction = post.Reactions.FirstOrDefault(r => r.UserId == currentUserId.Value);
                if (userReaction != null)
                {
                    hasReacted = true;
                    currentUserReactionType = userReaction.ReactionType;
                }
            }
        }        return new PostResponseDTO
        {
            Id = post.Id,
            Content = post.Content,
            MediaUrl = post.MediaUrl, // Legacy support
            MediaType = post.MediaType, // Legacy support
            MediaMimeType = (post.MediaType != null && post.MediaUrl != null) 
                ? GetMimeTypeForMediaType(post.MediaType, post.MediaUrl)
                : null,
            // Map multiple media files
            MediaFiles = post.MediaFiles?.Select(m => new PostMediaDTO
            {
                MediaUrl = m.MediaUrl,
                MediaType = m.MediaType,
                MediaPublicId = m.MediaPublicId,
                MediaMimeType = m.MediaMimeType,
                MediaFilename = m.MediaFilename,
                MediaFileSize = m.MediaFileSize,
                Width = m.Width,
                Height = m.Height,
                Duration = m.Duration,
                OrderIndex = m.OrderIndex
            }).OrderBy(m => m.OrderIndex).ToList() ?? new List<PostMediaDTO>(),
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt,
            UserId = post.UserId,
            Location = post.Location,
            Username = post.User.Username,
            ProfilePictureUrl = post.User.ProfilePictureUrl,
            CommentsCount = post.Comments?.Count ?? 0,
            HasReactedByCurrentUser = hasReacted,
            CurrentUserReactionType = currentUserReactionType,
            ReactionCounts = reactionCounts
        };
    }

    // Helper method to validate the media type parameter
    private bool IsValidMediaType(string mediaType)
    {
        if (string.IsNullOrEmpty(mediaType))
            return false;
            
        var validTypes = new[] { "image", "video", "file" };
        return validTypes.Contains(mediaType.ToLower());
    }
    
    // Helper method to get allowed MIME types for each media type
    private string[] GetAllowedMimeTypes(string mediaType)
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
    
    // Helper method to get max file size for each media type
    private long GetMaxFileSizeForMediaType(string mediaType)
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

    // Helper method to get MIME type from media type and URL
    private string GetMimeTypeForMediaType(string mediaType, string url)
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
}
