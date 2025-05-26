using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialApp.DTOs;
using SocialApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocialApp.Services.Comment
{
    public class CommentService : ICommentService
    {
        private readonly SocialMediaDbContext _context;
        private readonly ILogger<CommentService> _logger;

        public CommentService(SocialMediaDbContext context, ILogger<CommentService> logger)
        {
            _context = context;
            _logger = logger;
        }
        
        public async Task<CommentResponseDTO?> CreateCommentAsync(CreateCommentDTO commentDto, int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null || user.IsDeleted)
                {
                    _logger.LogWarning("Attempted to create comment for non-existent or deleted user: {UserId}", userId);
                    return null;
                }

                // Check if post exists
                var post = await _context.Posts.FindAsync(commentDto.PostId);
                if (post == null)
                {
                    _logger.LogWarning("Attempted to comment on non-existent post: {PostId}", commentDto.PostId);
                    return null;
                }                // Check if parent comment exists if specified
                if (commentDto.ParentCommentId.HasValue)
                {
                    var parentComment = await _context.Comments.FindAsync(commentDto.ParentCommentId.Value);
                    if (parentComment == null)
                    {
                        _logger.LogWarning("Attempted to reply to non-existent comment: {CommentId}", commentDto.ParentCommentId.Value);
                        return null;
                    }
                }                var comment = new Models.Comment
                {
                    PostId = commentDto.PostId,
                    UserId = userId,
                    ParentCommentId = commentDto.ParentCommentId,
                    Content = commentDto.Content,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();

                // Add notification to post author if not the same as commenter
                if (post.UserId != userId)
                {                    var notification = new Notification
                    {
                        UserId = post.UserId,
                        FromUserId = userId,
                        PostId = post.Id,
                        CommentId = comment.Id,
                        Type = 1, // 1 = CommentOnPost
                        Content = "commented on your post",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();
                }                // Add notification to parent comment author if exists and not the same as commenter
                if (commentDto.ParentCommentId.HasValue)
                {
                    var parentComment = await _context.Comments.FindAsync(commentDto.ParentCommentId.Value);
                    if (parentComment != null && parentComment.UserId != userId)
                    {                        var notification = new Notification
                        {
                            UserId = parentComment.UserId,
                            FromUserId = userId,
                            PostId = post.Id,
                            CommentId = comment.Id,
                            Type = 2, // 2 = ReplyToComment
                            Content = "replied to your comment",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Notifications.Add(notification);
                        await _context.SaveChangesAsync();
                    }
                }

                // Return the newly created comment with user information
                return await GetCommentResponseDTOAsync(comment.Id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating comment");
                throw;
            }
        }        public async Task<CommentResponseDTO?> UpdateCommentAsync(int commentId, UpdateCommentDTO commentDto, int userId)
        {            try
            {
                var comment = await _context.Comments
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == commentId);

                if (comment == null)
                {
                    _logger.LogWarning("Comment {CommentId} not found", commentId);
                    return null;
                }

                // Check if user owns the comment
                if (comment.UserId != userId)
                {
                    _logger.LogWarning("User {UserId} tried to update comment {CommentId} without permission", userId, commentId);
                    return null;
                }                // Update comment
                comment.Content = commentDto.Content;
                comment.UpdatedAt = DateTime.UtcNow;

                _context.Comments.Update(comment);
                await _context.SaveChangesAsync();

                return await GetCommentResponseDTOAsync(comment.Id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating comment {CommentId}", commentId);
                throw;
            }
        }

        public async Task<bool> DeleteCommentAsync(int commentId, int userId)
        {
            try
            {                var comment = await _context.Comments
                    .Include(c => c.InverseParentComment) // Get replies
                    .FirstOrDefaultAsync(c => c.Id == commentId);

                if (comment == null)
                {
                    _logger.LogWarning("Comment {CommentId} not found", commentId);
                    return false;
                }

                // Check if user owns the comment or is an admin
                var user = await _context.Users.FindAsync(userId);
                if (comment.UserId != userId && user?.Role != "Admin")
                {
                    _logger.LogWarning("User {UserId} tried to delete comment {CommentId} without permission", userId, commentId);
                    return false;
                }

                // Delete associated reactions
                var reactions = await _context.Reactions
                    .Where(r => r.EntityId == commentId && r.EntityType == "Comment")
                    .ToListAsync();
                
                if (reactions.Any())
                {
                    _context.Reactions.RemoveRange(reactions);
                }

                // Delete associated notifications
                var notifications = await _context.Notifications
                    .Where(n => n.CommentId == commentId)
                    .ToListAsync();
                
                if (notifications.Any())
                {
                    _context.Notifications.RemoveRange(notifications);
                }                // If the comment has replies, recursively delete them
                if (comment.InverseParentComment.Any())
                {
                    foreach (var reply in comment.InverseParentComment.ToList())
                    {
                        await DeleteCommentAsync(reply.Id, userId);
                    }
                }

                // Delete the comment
                _context.Comments.Remove(comment);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting comment {CommentId}", commentId);
                throw;
            }
        }        public async Task<List<CommentResponseDTO>> GetCommentsByPostIdAsync(int postId)
        {
            try
            {                // Get only top-level comments (no parent) for the post
                var comments = await _context.Comments
                    .Where(c => c.PostId == postId && c.ParentCommentId == null)
                    .Include(c => c.User)
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync();

                var commentDtos = new List<CommentResponseDTO>();                foreach (var comment in comments)
                {
                    // Get reactions for this comment
                    var reactions = await _context.Reactions
                        .Where(r => r.EntityId == comment.Id && r.EntityType == "Comment")
                        .Include(r => r.User)
                        .ToListAsync();

                    // Get reply count for this comment
                    var replyCount = await _context.Comments
                        .CountAsync(c => c.ParentCommentId == comment.Id);

                var reactionCounts = reactions
                    .GroupBy(r => r.ReactionType)
                    .ToDictionary(g => g.Key, g => g.Count());                var commentDto = new CommentResponseDTO
                    {
                        Id = comment.Id,
                        PostId = comment.PostId,
                        ParentCommentId = comment.ParentCommentId,
                        UserId = comment.UserId,
                        Username = comment.User?.Username ?? string.Empty,
                        ProfilePictureUrl = comment.User?.ProfilePictureUrl,
                        Content = comment.Content,
                        CreatedAt = comment.CreatedAt,
                        UpdatedAt = comment.UpdatedAt,
                        ReactionsCount = reactions.Count,
                        ReactionCounts = reactionCounts,
                        HasReactedByCurrentUser = false,
                        CurrentUserReactionType = null,
                        RepliesCount = replyCount,
                        Replies = new List<CommentResponseDTO>() // Keep empty, will be loaded on demand
                    };                    commentDtos.Add(commentDto);
                }

                return commentDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comments for post {PostId}", postId);
                throw;
            }
        }        public async Task<CommentResponseDTO?> GetCommentByIdAsync(int commentId, int? currentUserId = null)
        {            try
            {
                var comment = await _context.Comments
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == commentId);

                if (comment == null)
                {
                    return null;
                }

                return await GetCommentResponseDTOAsync(commentId, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comment {CommentId}", commentId);
                throw;
            }
        }        public async Task<CommentResponseDTO?> AddOrToggleReactionAsync(CommentReactionDTO reactionDto, int userId)
        {            try
            {
                // Validate comment exists
                var comment = await _context.Comments.FindAsync(reactionDto.CommentId);
                if (comment == null)
                {
                    _logger.LogWarning("Attempted to react to non-existent comment: {CommentId}", reactionDto.CommentId);
                    return null;
                }                // Check if user exists
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Attempted reaction from non-existent user: {UserId}", userId);
                    return null;
                }

                // Check if reaction already exists
                var existingReaction = await _context.Reactions
                    .FirstOrDefaultAsync(r => 
                        r.UserId == userId && 
                        r.EntityId == reactionDto.CommentId &&
                        r.EntityType == "Comment");
                        
                // If the same reaction type exists, remove it (toggle behavior)
                if (existingReaction != null && existingReaction.ReactionType == reactionDto.ReactionType)
                {
                    _context.Reactions.Remove(existingReaction);
                    // Remove notification if exists
                    var notification = await _context.Notifications
                        .FirstOrDefaultAsync(n => 
                            n.FromUserId == userId && 
                            n.CommentId == reactionDto.CommentId &&
                            n.Type == 3); // 3 = ReactionOnComment
                            
                    if (notification != null)
                    {
                        _context.Notifications.Remove(notification);
                    }
                    
                    await _context.SaveChangesAsync();
                }
                // If a different reaction type exists, update it
                else if (existingReaction != null)
                {
                    existingReaction.ReactionType = reactionDto.ReactionType;
                    existingReaction.CreatedAt = DateTime.UtcNow;
                    _context.Reactions.Update(existingReaction);
                    await _context.SaveChangesAsync();                }
                // Otherwise, create a new reaction
                else
                {
                    var reaction = new Reaction
                    {
                        UserId = userId,
                        EntityId = reactionDto.CommentId,
                        EntityType = "Comment",
                        ReactionType = reactionDto.ReactionType,
                        CreatedAt = DateTime.UtcNow
                    };
                    
                    _context.Reactions.Add(reaction);
                      // Create notification if comment author is not the same as reactor
                    if (comment.UserId != userId)
                    {
                        var notification = new Notification
                        {
                            UserId = comment.UserId,
                            FromUserId = userId,
                            PostId = comment.PostId,
                            CommentId = comment.Id,
                            Type = 3, // 3 = ReactionOnComment
                            Content = $"reacted to your comment with {reactionDto.ReactionType}",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        };
                        
                        _context.Notifications.Add(notification);
                    }
                    
                    await _context.SaveChangesAsync();
                }

                return await GetCommentResponseDTOAsync(reactionDto.CommentId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing reaction for comment {CommentId}", reactionDto.CommentId);
                throw;
            }
        }        private async Task<CommentResponseDTO?> GetCommentResponseDTOAsync(int commentId, int? currentUserId)
        {                var comment = await _context.Comments
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == commentId);

            if (comment == null)
            {
                return null;
            }

            // Get reactions for this comment
            var reactions = await _context.Reactions
                .Where(r => r.EntityId == comment.Id && r.EntityType == "Comment")
                .Include(r => r.User)
                .ToListAsync();            string? currentUserReaction = null;
            if (currentUserId.HasValue)
            {
                currentUserReaction = reactions
                    .FirstOrDefault(r => r.UserId == currentUserId.Value)?.ReactionType;
            }

            var reactionCounts = reactions
                .GroupBy(r => r.ReactionType)
                .ToDictionary(g => g.Key, g => g.Count());
                
            return new CommentResponseDTO
            {
                Id = comment.Id,
                PostId = comment.PostId,
                ParentCommentId = comment.ParentCommentId,
                UserId = comment.UserId,
                Username = comment.User?.Username ?? string.Empty,
                ProfilePictureUrl = comment.User?.ProfilePictureUrl,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt,
                ReactionsCount = reactions.Count,
                ReactionCounts = reactionCounts,
                HasReactedByCurrentUser = currentUserReaction != null,
                CurrentUserReactionType = currentUserReaction
            };
        }        public async Task<List<CommentResponseDTO>> GetRepliesByCommentIdAsync(int commentId, int? currentUserId = null)
        {
            try
            {
                var replies = await _context.Comments
                    .Where(c => c.ParentCommentId == commentId)
                    .Include(c => c.User)
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync();
                
                if (replies == null || !replies.Any())
                {
                    return new List<CommentResponseDTO>();
                }
                
                var replyDtos = new List<CommentResponseDTO>();
                
                foreach (var reply in replies)
                {
                    var replyReplies = await _context.Comments
                        .Where(c => c.ParentCommentId == reply.Id)
                        .CountAsync();
                      var reactionCounts = await _context.Reactions
                        .Where(r => r.EntityId == reply.Id && r.EntityType == "Comment")
                        .GroupBy(r => r.ReactionType)
                        .Select(g => new { ReactionType = g.Key, Count = g.Count() })
                        .ToDictionaryAsync(k => k.ReactionType, v => v.Count);

                    string? currentUserReactionType = null;
                    bool hasReacted = false;
                    
                    if (currentUserId.HasValue)
                    {
                        var userReaction = await _context.Reactions
                            .FirstOrDefaultAsync(r => r.EntityId == reply.Id && r.EntityType == "Comment" && r.UserId == currentUserId);
                        
                        if (userReaction != null)
                        {
                            hasReacted = true;
                            currentUserReactionType = userReaction.ReactionType;
                        }
                    }

                    // Recursively load nested replies
                    var nestedReplies = await GetRepliesByCommentIdAsync(reply.Id, currentUserId);
                      
                    var replyDto = new CommentResponseDTO
                    {
                        Id = reply.Id,
                        Content = reply.Content,
                        CreatedAt = reply.CreatedAt,
                        UpdatedAt = reply.UpdatedAt,
                        UserId = reply.UserId,
                        Username = reply.User.Username,
                        ProfilePictureUrl = reply.User.ProfilePictureUrl,
                        IsVerified = reply.User.Role == "Admin" || reply.User.Role == "Moderator",
                        PostId = reply.PostId,
                        ParentCommentId = reply.ParentCommentId,
                        RepliesCount = replyReplies,
                        ReactionsCount = reactionCounts.Values.Sum(),
                        ReactionCounts = reactionCounts,
                        HasReactedByCurrentUser = hasReacted,
                        CurrentUserReactionType = currentUserReactionType,
                        Replies = nestedReplies
                    };
                    
                    replyDtos.Add(replyDto);
                }
                
                return replyDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving replies for comment {CommentId}", commentId);
                return new List<CommentResponseDTO>();
            }
        }
    }
}
