using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialApp.DTOs;
using SocialApp.Models;
using SocialApp.Services.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SocialApp.Controllers.Post
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReactionsController : ControllerBase    {
        private readonly SocialMediaDbContext _context;
        private readonly ILogger<ReactionsController> _logger;
        private readonly INotificationService _notificationService;

        public ReactionsController(
            SocialMediaDbContext context,
            ILogger<ReactionsController> logger,
            INotificationService notificationService)
        {
            _context = context;
            _logger = logger;
            _notificationService = notificationService;
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ReactionResponseDTO>> AddReaction([FromBody] CreateReactionDTO reactionDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Handle null or empty reactionType as a request to remove the reaction
                if (string.IsNullOrEmpty(reactionDto.ReactionType))
                {
                    return await RemoveReactionByPost(reactionDto.PostId);
                }

                int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

                // Check if the user has already reacted to this post
                var existingReaction = await _context.Reactions
                    .FirstOrDefaultAsync(r => r.UserId == currentUserId && r.PostId == reactionDto.PostId);                if (existingReaction != null)
                {
                    // If reaction type is the same, user is toggling it off
                    if (existingReaction.ReactionType == reactionDto.ReactionType)
                    {
                        // User is toggling off (clicking same reaction)
                        _context.Reactions.Remove(existingReaction);
                        await _context.SaveChangesAsync();
                        
                        return NoContent();
                    }
                    // Update the existing reaction if it's different
                    else
                    {
                        existingReaction.ReactionType = reactionDto.ReactionType;
                        await _context.SaveChangesAsync();
                    }

                    // Get user info for the response
                    var user = await _context.Users.FindAsync(currentUserId);
                    if (user == null)
                    {
                        return NotFound(new { message = "User not found" });
                    }                    return Ok(new ReactionResponseDTO
                    {
                        Id = existingReaction.Id,
                        UserId = existingReaction.UserId,
                        Username = user.Username,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        ProfilePictureUrl = user.ProfilePictureUrl,
                        PostId = existingReaction.PostId ?? 0,
                        ReactionType = existingReaction.ReactionType,
                        CreatedAt = existingReaction.CreatedAt
                    });
                }                // Create new reaction
                var reaction = new Reaction
                {
                    UserId = currentUserId,
                    PostId = reactionDto.PostId,
                    ReactionType = reactionDto.ReactionType,
                    CreatedAt = DateTime.Now
                };                _context.Reactions.Add(reaction);
                await _context.SaveChangesAsync();

                // Tạo thông báo cho chủ bài viết (nếu không phải chính mình)
                try
                {
                    await _notificationService.CreateLikeNotificationAsync(reactionDto.PostId, currentUserId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create like notification for post {PostId} by user {UserId}", reactionDto.PostId, currentUserId);
                    // Không throw exception để không ảnh hưởng đến việc tạo reaction
                }

                // Get user info for the response
                var currentUser = await _context.Users.FindAsync(currentUserId);
                if (currentUser == null)
                {
                    return NotFound(new { message = "User not found" });
                }                return Ok(new ReactionResponseDTO
                {
                    Id = reaction.Id,
                    UserId = reaction.UserId,
                    Username = currentUser.Username,
                    FirstName = currentUser.FirstName,
                    LastName = currentUser.LastName,
                    ProfilePictureUrl = currentUser.ProfilePictureUrl,
                    PostId = reaction.PostId ?? 0,
                    ReactionType = reaction.ReactionType,
                    CreatedAt = reaction.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding reaction");
                return StatusCode(500, new { message = "An error occurred while adding the reaction" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> RemoveReaction(int id)
        {
            try
            {
                int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

                var reaction = await _context.Reactions.FindAsync(id);
                if (reaction == null)
                {
                    return NotFound(new { message = "Reaction not found" });
                }

                if (reaction.UserId != currentUserId)
                {
                    return Forbid();
                }

                _context.Reactions.Remove(reaction);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing reaction");
                return StatusCode(500, new { message = "An error occurred while removing the reaction" });
            }
        }

        [HttpGet("post/{postId}")]
        public async Task<ActionResult<ReactionSummaryDTO>> GetPostReactions(int postId)
        {
            try
            {
                // Check if the post exists
                var post = await _context.Posts.FindAsync(postId);
                if (post == null)
                {
                    return NotFound(new { message = "Post not found" });
                }

                // Get all reactions for the post
                var reactions = await _context.Reactions
                    .Where(r => r.PostId == postId)
                    .ToListAsync();

                var summary = new ReactionSummaryDTO
                {
                    TotalCount = reactions.Count,
                    ReactionCounts = reactions.GroupBy(r => r.ReactionType)
                        .ToDictionary(g => g.Key, g => g.Count())
                };

                // Check if the current user has reacted
                if (User.Identity?.IsAuthenticated ?? false)
                {
                    int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                    var userReaction = reactions.FirstOrDefault(r => r.UserId == currentUserId);
                    
                    if (userReaction != null)
                    {
                        summary.HasReactedByCurrentUser = true;
                        summary.CurrentUserReactionType = userReaction.ReactionType;
                    }
                }

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting post reactions");
                return StatusCode(500, new { message = "An error occurred while retrieving reactions" });
            }
        }

        [HttpDelete("post/{postId}")]
        [Authorize]
        public async Task<ActionResult> RemoveReactionByPost(int postId)
        {
            try
            {
                int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

                var reaction = await _context.Reactions
                    .FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == currentUserId);
                    
                if (reaction == null)
                {
                    return NotFound(new { message = "Reaction not found" });
                }

                _context.Reactions.Remove(reaction);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing reaction from post {PostId}", postId);
                return StatusCode(500, new { message = "An error occurred while removing the reaction" });
            }
        }

        [HttpGet("history/{postId}")]
        [Authorize]
        public async Task<ActionResult<List<ReactionResponseDTO>>> GetReactionHistory(int postId)
        {
            try
            {
                int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                
                // Check if post exists
                var post = await _context.Posts.FindAsync(postId);
                if (post == null)
                {
                    return NotFound(new { message = "Post not found" });
                }
                
                // Get all reactions for the post
                var reactions = await _context.Reactions
                    .Include(r => r.User)
                    .Where(r => r.PostId == postId)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();
                  var result = reactions.Select(r => new ReactionResponseDTO
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    Username = r.User.Username,
                    FirstName = r.User.FirstName,
                    LastName = r.User.LastName,
                    ProfilePictureUrl = r.User.ProfilePictureUrl,
                    PostId = r.PostId ?? 0,
                    ReactionType = r.ReactionType,
                    CreatedAt = r.CreatedAt
                }).ToList();
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reaction history for post {PostId}", postId);
                return StatusCode(500, new { message = "An error occurred while getting reaction history" });
            }
        }
    }
}
