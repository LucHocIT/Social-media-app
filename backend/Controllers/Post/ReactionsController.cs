using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialApp.DTOs;
using SocialApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SocialApp.Controllers.Post
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReactionsController : ControllerBase
    {
        private readonly SocialMediaDbContext _context;
        private readonly ILogger<ReactionsController> _logger;

        public ReactionsController(
            SocialMediaDbContext context,
            ILogger<ReactionsController> logger)
        {
            _context = context;
            _logger = logger;
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

                if (reactionDto.PostId == null && reactionDto.CommentId == null)
                {
                    return BadRequest(new { message = "Either PostId or CommentId must be provided" });
                }

                int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

                // Check if the user has already reacted to this post/comment
                var existingReaction = await _context.Reactions
                    .FirstOrDefaultAsync(r => 
                        r.UserId == currentUserId && 
                        ((reactionDto.PostId != null && r.PostId == reactionDto.PostId) || 
                        (reactionDto.CommentId != null && r.CommentId == reactionDto.CommentId)));

                if (existingReaction != null)
                {
                    // Update the existing reaction if it's different
                    if (existingReaction.ReactionType != reactionDto.ReactionType)
                    {
                        existingReaction.ReactionType = reactionDto.ReactionType;
                        await _context.SaveChangesAsync();
                    }

                    // Get user info for the response
                    var user = await _context.Users.FindAsync(currentUserId);
                    if (user == null)
                    {
                        return NotFound(new { message = "User not found" });
                    }

                    return Ok(new ReactionResponseDTO
                    {
                        Id = existingReaction.Id,
                        UserId = existingReaction.UserId,
                        Username = user.Username,
                        ProfilePictureUrl = user.ProfilePictureUrl,
                        PostId = existingReaction.PostId,
                        CommentId = existingReaction.CommentId,
                        ReactionType = existingReaction.ReactionType,
                        CreatedAt = existingReaction.CreatedAt
                    });
                }

                // Create new reaction
                var reaction = new Reaction
                {
                    UserId = currentUserId,
                    PostId = reactionDto.PostId,
                    CommentId = reactionDto.CommentId,
                    ReactionType = reactionDto.ReactionType,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Reactions.Add(reaction);
                await _context.SaveChangesAsync();

                // Get user info for the response
                var currentUser = await _context.Users.FindAsync(currentUserId);
                if (currentUser == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                return Ok(new ReactionResponseDTO
                {
                    Id = reaction.Id,
                    UserId = reaction.UserId,
                    Username = currentUser.Username,
                    ProfilePictureUrl = currentUser.ProfilePictureUrl,
                    PostId = reaction.PostId,
                    CommentId = reaction.CommentId,
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

        [HttpGet("comment/{commentId}")]
        public async Task<ActionResult<ReactionSummaryDTO>> GetCommentReactions(int commentId)
        {
            try
            {
                // Check if the comment exists
                var comment = await _context.Comments.FindAsync(commentId);
                if (comment == null)
                {
                    return NotFound(new { message = "Comment not found" });
                }

                // Get all reactions for the comment
                var reactions = await _context.Reactions
                    .Where(r => r.CommentId == commentId)
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
                _logger.LogError(ex, "Error getting comment reactions");
                return StatusCode(500, new { message = "An error occurred while retrieving reactions" });
            }
        }
    }
}
