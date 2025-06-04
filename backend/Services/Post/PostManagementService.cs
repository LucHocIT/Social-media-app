using SocialApp.DTOs;
using SocialApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocialApp.Services.Post;

public class PostManagementService : IPostManagementService
{    private readonly SocialMediaDbContext _context;
    private readonly ILogger<PostManagementService> _logger;
    private readonly IPostQueryService _postQueryService;
    private readonly IPostMediaService _postMediaService;

    public PostManagementService(
        SocialMediaDbContext context,
        ILogger<PostManagementService> logger,
        IPostQueryService postQueryService,
        IPostMediaService postMediaService)
    {
        _context = context;
        _logger = logger;
        _postQueryService = postQueryService;
        _postMediaService = postMediaService;
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
                Location = postDto.Location,
                PrivacyLevel = postDto.PrivacyLevel,
                CreatedAt = DateTime.Now,
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
                        CreatedAt = DateTime.Now
                    };
                    mediaEntities.Add(mediaEntity);
                }
                
                _context.PostMedias.AddRange(mediaEntities);
                await _context.SaveChangesAsync();
            }
            // Add legacy media support as a PostMedia entry if needed
            else if (!string.IsNullOrEmpty(postDto.MediaUrl) && !string.IsNullOrEmpty(postDto.MediaType))
            {
                var legacyMedia = new Models.PostMedia
                {
                    PostId = post.Id,
                    MediaUrl = postDto.MediaUrl,
                    MediaType = postDto.MediaType,
                    MediaPublicId = postDto.MediaPublicId,
                    MediaMimeType = _postMediaService.GetMimeTypeForMediaType(postDto.MediaType, postDto.MediaUrl),
                    OrderIndex = 0,
                    CreatedAt = DateTime.Now
                };
                
                _context.PostMedias.Add(legacyMedia);
                await _context.SaveChangesAsync();
            }

            return await _postQueryService.GetPostByIdAsync(post.Id, userId);
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
                .Include(p => p.MediaFiles)
                .FirstOrDefaultAsync(p => p.Id == postId && p.UserId == userId);

            if (post == null)
            {
                _logger.LogWarning("Post {PostId} not found for user {UserId}", postId, userId);
                return null;
            }

            post.Content = postDto.Content;
            post.Location = postDto.Location;
            post.PrivacyLevel = postDto.PrivacyLevel;
            post.UpdatedAt = DateTime.Now;

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
                            CreatedAt = DateTime.Now
                        };
                        mediaEntities.Add(mediaEntity);
                    }
                    _context.PostMedias.AddRange(mediaEntities);
                }
            }

            await _context.SaveChangesAsync();
            return await _postQueryService.GetPostByIdAsync(postId, userId);
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

            // Delete associated reactions
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
        }        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting post {PostId} for user {UserId}", postId, userId);
            throw;
        }
    }
}
