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
    }

    public async Task<PostResponseDTO?> CreatePostAsync(int userId, CreatePostDTO postDto)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || user.IsDeleted)
            {
                _logger.LogWarning("Attempted to create post for non-existent or deleted user: {UserId}", userId);
                return null;
            }            var post = new Models.Post
            {
                Content = postDto.Content,
                MediaUrl = postDto.MediaUrl,
                MediaType = postDto.MediaType,
                MediaPublicId = postDto.MediaPublicId,
                CreatedAt = DateTime.UtcNow,
                UserId = userId
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            return await GetPostByIdAsync(post.Id, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating post for user {UserId}", userId);
            throw;
        }
    }

    public async Task<PostResponseDTO?> UpdatePostAsync(int userId, int postId, UpdatePostDTO postDto)
    {
        try
        {
            var post = await _context.Posts
                .FirstOrDefaultAsync(p => p.Id == postId && p.UserId == userId);

            if (post == null)
            {
                _logger.LogWarning("Post {PostId} not found for user {UserId}", postId, userId);
                return null;
            }            post.Content = postDto.Content;
            post.MediaUrl = postDto.MediaUrl;
            post.MediaType = postDto.MediaType;
            post.MediaPublicId = postDto.MediaPublicId;
            post.UpdatedAt = DateTime.UtcNow;

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
            }

            // Delete associated likes
            var likes = await _context.Likes.Where(l => l.PostId == postId).ToListAsync();
            _context.Likes.RemoveRange(likes);

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
    }

    public async Task<PostResponseDTO?> GetPostByIdAsync(int postId, int? currentUserId = null)
    {
        try
        {
            var post = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Likes)
                .Include(p => p.Comments)
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
        {
            var query = _context.Posts
                .Include(p => p.User)
                .Include(p => p.Likes)
                .Include(p => p.Comments)
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
            };

            var query = _context.Posts
                .Include(p => p.User)
                .Include(p => p.Likes)
                .Include(p => p.Comments)
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
            throw;
        }
    }

    public async Task<bool> LikePostAsync(int userId, int postId)
    {
        try
        {
            // Check if the post exists
            var post = await _context.Posts.FindAsync(postId);
            if (post == null)
            {
                _logger.LogWarning("Post {PostId} not found", postId);
                return false;
            }

            // Check if the user has already liked the post
            var existingLike = await _context.Likes
                .FirstOrDefaultAsync(l => l.UserId == userId && l.PostId == postId);

            if (existingLike != null)
            {
                _logger.LogInformation("User {UserId} has already liked post {PostId}", userId, postId);
                return true; // Already liked
            }

            // Create a new like
            var like = new Like
            {
                UserId = userId,
                PostId = postId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Likes.Add(like);

            // Create a notification for the post owner if it's not the same as the user liking
            if (post.UserId != userId)
            {
                var notification = new Notification
                {
                    Type = 1, // 1 for like notification
                    Content = "liked your post",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    UserId = post.UserId, // Post owner receives the notification
                    FromUserId = userId, // User who liked the post
                    PostId = postId
                };

                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error liking post {PostId} by user {UserId}", postId, userId);
            throw;
        }
    }

    public async Task<bool> UnlikePostAsync(int userId, int postId)
    {
        try
        {
            var like = await _context.Likes
                .FirstOrDefaultAsync(l => l.UserId == userId && l.PostId == postId);

            if (like == null)
            {
                _logger.LogWarning("Like not found for user {UserId} on post {PostId}", userId, postId);
                return false;
            }

            _context.Likes.Remove(like);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unliking post {PostId} by user {UserId}", postId, userId);
            throw;
        }
    }

    public async Task<IEnumerable<PostResponseDTO>> GetLikedPostsByUserAsync(int userId, int pageNumber = 1, int pageSize = 10, int? currentUserId = null)
    {
        try
        {
            var likedPostIds = await _context.Likes
                .Where(l => l.UserId == userId)
                .Select(l => l.PostId)
                .ToListAsync();

            var posts = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Likes)
                .Include(p => p.Comments)
                .Where(p => likedPostIds.Contains(p.Id))
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return posts.Select(p => MapPostToResponseDTO(p, currentUserId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving liked posts for user {UserId}", userId);
            throw;
        }
    }    public async Task<UploadMediaResult> UploadPostMediaAsync(int userId, IFormFile media, string mediaType = "image")
    {
        try
        {
            if (media == null || media.Length == 0)
            {
                return new UploadMediaResult
                {
                    Success = false,
                    Message = "No file uploaded"
                };
            }
            
            // Validate media type parameter
            if (!IsValidMediaType(mediaType))
            {
                return new UploadMediaResult
                {
                    Success = false,
                    Message = "Invalid media type. Allowed values are 'image', 'video', and 'file'."
                };
            }

            // Get allowed MIME types based on the media type
            var allowedTypes = GetAllowedMimeTypes(mediaType);
            
            // Check if the content type is allowed for the selected media type
            if (!allowedTypes.Contains(media.ContentType.ToLower()))
            {
                return new UploadMediaResult
                {
                    Success = false,
                    Message = $"Invalid file type for {mediaType}. Allowed types: {string.Join(", ", allowedTypes)}"
                };
            }

            // Validate file size (limit varies by media type)
            long maxSize = GetMaxFileSizeForMediaType(mediaType);
            if (media.Length > maxSize)
            {
                return new UploadMediaResult
                {
                    Success = false,
                    Message = $"File size exceeds the maximum allowed ({maxSize / (1024 * 1024)}MB)."
                };
            }

            // Use appropriate CloudinaryService upload method based on media type
            using (var stream = media.OpenReadStream())
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
                    return new UploadMediaResult
                    {
                        Success = false,
                        Message = $"Failed to upload {mediaType}"
                    };
                }

                return new UploadMediaResult
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
                    MediaType = uploadResult.MediaType,
                    Message = $"{mediaType} uploaded successfully"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading {MediaType} for user {UserId}", mediaType, userId);
            return new UploadMediaResult
            {
                Success = false,
                Message = $"An error occurred: {ex.Message}"
            };
        }
    }

    // Helper method to map Post entity to PostResponseDTO
    private PostResponseDTO MapPostToResponseDTO(Models.Post post, int? currentUserId)
    {
        bool isLiked = false;

        if (currentUserId.HasValue)
        {
            isLiked = post.Likes.Any(l => l.UserId == currentUserId.Value);
        }        return new PostResponseDTO
        {
            Id = post.Id,
            Content = post.Content,
            MediaUrl = post.MediaUrl,
            MediaType = post.MediaType,            MediaMimeType = (post.MediaType != null && post.MediaUrl != null) 
                ? GetMimeTypeForMediaType(post.MediaType, post.MediaUrl)
                : null,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt,
            UserId = post.UserId,
            Username = post.User.Username,
            ProfilePictureUrl = post.User.ProfilePictureUrl,
            LikesCount = post.Likes.Count,
            CommentsCount = post.Comments.Count,
            IsLikedByCurrentUser = isLiked
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
