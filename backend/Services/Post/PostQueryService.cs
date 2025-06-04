using SocialApp.DTOs;
using SocialApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocialApp.Services.Post;

public class PostQueryService : IPostQueryService
{
    private readonly SocialMediaDbContext _context;
    private readonly ILogger<PostQueryService> _logger;

    public PostQueryService(
        SocialMediaDbContext context,
        ILogger<PostQueryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PostResponseDTO?> GetPostByIdAsync(int postId, int? currentUserId = null)
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

            // Apply privacy filtering - private posts should only be visible to:
            // 1. The post author themselves
            // 2. Users who are following the post author
            // Secret posts (level 2) should only be visible to the author
            if (post.PrivacyLevel == 1) // Private
            {
                if (currentUserId.HasValue)
                {
                    if (currentUserId.Value != post.UserId)
                    {
                        // Check if current user is following the post author
                        var isFollowing = await _context.UserFollowers
                            .AnyAsync(uf => uf.FollowerId == currentUserId.Value && uf.FollowingId == post.UserId);
                          
                        if (!isFollowing)
                        {
                            // Not following and not the author, so can't view private post
                            _logger.LogWarning("User {CurrentUserId} attempted to access private post {PostId} without permission", currentUserId.Value, postId);
                            return null;
                        }
                    }
                    // If currentUserId == post.UserId, they can see their own private post
                }
                else
                {
                    // Anonymous users can't see private posts
                    _logger.LogWarning("Anonymous user attempted to access private post {PostId}", postId);
                    return null;
                }
            }
            else if (post.PrivacyLevel == 2) // Secret - only author can see
            {
                if (!currentUserId.HasValue || currentUserId.Value != post.UserId)
                {
                    // Only the author can see secret posts
                    _logger.LogWarning("User {CurrentUserId} attempted to access secret post {PostId}", currentUserId, postId);
                    return null;
                }
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

            // Apply privacy filtering
            if (currentUserId.HasValue)
            {
                var currentUserFollowingIds = await _context.UserFollowers
                    .Where(uf => uf.FollowerId == currentUserId.Value)
                    .Select(uf => uf.FollowingId)
                    .ToListAsync();
                  
                query = query.Where(p => 
                    p.PrivacyLevel == 0 || // Public posts are visible to everyone
                    p.UserId == currentUserId.Value || // Own posts are always visible
                    (p.PrivacyLevel == 1 && currentUserFollowingIds.Contains(p.UserId)) // Private posts are visible if following the author
                    // Secret posts (level 2) are only visible to the author, handled by p.UserId == currentUserId.Value above
                );
            }
            else
            {
                // Anonymous users can only see public posts
                query = query.Where(p => p.PrivacyLevel == 0);
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

            IQueryable<Models.Post> query = _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments)
                .Include(p => p.Reactions)
                .Include(p => p.MediaFiles)
                .Where(p => p.UserId == userId);

            // Apply privacy filtering
            if (currentUserId.HasValue)
            {
                if (currentUserId.Value != userId)
                {
                    // Check if current user is following the profile user
                    var isFollowing = await _context.UserFollowers
                        .AnyAsync(uf => uf.FollowerId == currentUserId.Value && uf.FollowingId == userId);
                      
                    if (!isFollowing)
                    {
                        // Not following, so can only see public posts
                        query = query.Where(p => p.PrivacyLevel == 0);
                    }
                }
                // If currentUserId == userId, they can see all their own posts (both public and private)
            }
            else
            {
                // Anonymous users can only see public posts
                query = query.Where(p => p.PrivacyLevel == 0);
            }

            // Apply ordering after filtering
            query = query.OrderByDescending(p => p.CreatedAt);

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

    // Helper method to map Post entity to PostResponseDTO
    private PostResponseDTO MapPostToResponseDTO(Models.Post post, int? currentUserId)
    {        
        bool hasReacted = false;
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
        }
        
        // Get the first media file for legacy support in the response
        var firstMedia = post.MediaFiles?.OrderBy(m => m.OrderIndex).FirstOrDefault();
            
        return new PostResponseDTO
        {
            Id = post.Id,
            Content = post.Content,
            // Legacy support - use the first media item if available
            MediaUrl = firstMedia?.MediaUrl,
            MediaType = firstMedia?.MediaType,
            MediaMimeType = firstMedia?.MediaMimeType,
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
            PrivacyLevel = post.PrivacyLevel,
            Username = post.User.Username,
            FirstName = post.User.FirstName,
            LastName = post.User.LastName,
            ProfilePictureUrl = post.User.ProfilePictureUrl,
            CommentsCount = post.Comments?.Count ?? 0,
            HasReactedByCurrentUser = hasReacted,
            CurrentUserReactionType = currentUserReactionType,
            ReactionCounts = reactionCounts
        };
    }
}
