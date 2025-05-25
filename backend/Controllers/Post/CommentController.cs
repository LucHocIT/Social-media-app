using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialApp.DTOs;
using SocialApp.Services.Comment;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialApp.Controllers.Post
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CommentController : ControllerBase
    {
        private readonly ICommentService _commentService;

        public CommentController(ICommentService commentService)
        {
            _commentService = commentService;
        }

        [HttpGet("{postId}")]
        public async Task<ActionResult<IEnumerable<CommentResponseDTO>>> GetCommentsByPostId(int postId)
        {
            var result = await _commentService.GetCommentsByPostIdAsync(postId);
            return Ok(result);
        }        [HttpPost]
        public async Task<ActionResult<CommentResponseDTO>> CreateComment(CreateCommentDTO dto)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
                return Unauthorized();

            var result = await _commentService.CreateCommentAsync(dto, userId);
            return Ok(result);
        }        [HttpPut("{id}")]
        public async Task<ActionResult<CommentResponseDTO>> UpdateComment(int id, UpdateCommentDTO dto)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
                return Unauthorized();

            var result = await _commentService.UpdateCommentAsync(id, dto, userId);
            if (result == null)
                return NotFound("Comment not found or you are not authorized to update this comment");

            return Ok(result);
        }        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteComment(int id)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
                return Unauthorized();

            var result = await _commentService.DeleteCommentAsync(id, userId);
            if (!result)
                return NotFound("Comment not found or you are not authorized to delete this comment");

            return NoContent();
        }        [HttpPost("reaction")]
        public async Task<ActionResult> AddReaction(CommentReactionDTO dto)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
                return Unauthorized();
                
            var result = await _commentService.AddOrToggleReactionAsync(dto, userId);
            return Ok(result);
        }        [HttpGet("replies/{commentId}")]
        public async Task<ActionResult<IEnumerable<CommentResponseDTO>>> GetRepliesByCommentId(int commentId)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            var result = await _commentService.GetRepliesByCommentIdAsync(commentId, userId);
            return Ok(result);
        }
    }
}
