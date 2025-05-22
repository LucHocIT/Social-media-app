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
            }

            var post = new Models.Post
            {
                Content = postDto.Content,
                MediaUrl = postDto.MediaUrl,
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
            }

            post.Content = postDto.Content;
            post.MediaUrl = postDto.MediaUrl;
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
    }
    public async Task<UploadMediaResult> UploadPostMediaAsync(int userId, IFormFile media)
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

            // Validate file type (you might want to restrict to images/videos only)
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp", "video/mp4" };
            if (!allowedTypes.Contains(media.ContentType.ToLower()))
            {
                return new UploadMediaResult
                {
                    Success = false,
                    Message = "Invalid file type. Only JPEG, PNG, GIF, WebP and MP4 are allowed."
                };
            }

            // Validate file size (e.g., limit to 10MB)
            var maxSize = 10 * 1024 * 1024; // 10MB
            if (media.Length > maxSize)
            {
                return new UploadMediaResult
                {
                    Success = false,
                    Message = "File size exceeds the maximum allowed (10MB)."
                };
            }

            // Use CloudinaryService to upload the image
            using (var stream = media.OpenReadStream())
            {
                var uploadResult = await _cloudinaryService.UploadImageAsync(stream, $"post_{userId}_{Guid.NewGuid()}");

                if (uploadResult == null)
                {
                    return new UploadMediaResult
                    {
                        Success = false,
                        Message = "Failed to upload media"
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
                    Message = "Media uploaded successfully"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading media for user {UserId}", userId);
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
        }

        return new PostResponseDTO
        {
            Id = post.Id,
            Content = post.Content,
            MediaUrl = post.MediaUrl,
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
}
