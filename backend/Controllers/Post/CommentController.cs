using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialApp.DTOs;
using SocialApp.Services.Comment;
using SocialApp.Services.Notification;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SocialApp.Controllers.Post
{
    [ApiController]
    [Route("api/comments")]
    public class CommentController : ControllerBase    {        private readonly ICommentService _commentService;
        private readonly ICommentReportService _commentReportService;
        private readonly INotificationService _notificationService;

        public CommentController(
            ICommentService commentService,
            ICommentReportService commentReportService,
            INotificationService notificationService)
        {
            _commentService = commentService;
            _commentReportService = commentReportService;
            _notificationService = notificationService;
        }

        #region Comment Management

        [HttpGet("post/{postId}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<CommentResponseDTO>>> GetCommentsByPostId(int postId)
        {
            var result = await _commentService.GetCommentsByPostIdAsync(postId);
            return Ok(result);
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<CommentResponseDTO>> CreateComment(CreateCommentDTO dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
                return Unauthorized();            var result = await _commentService.CreateCommentAsync(dto, userId);
            
            // Tạo thông báo cho chủ bài viết và chủ comment gốc (nếu là reply)
            if (result != null)
            {
                try
                {
                    if (dto.ParentCommentId.HasValue)
                    {
                        // Đây là reply to comment
                        await _notificationService.CreateCommentReplyNotificationAsync(dto.ParentCommentId.Value, result.Id, userId);
                    }
                    else
                    {
                        // Đây là comment mới cho bài viết
                        await _notificationService.CreateCommentNotificationAsync(dto.PostId, result.Id, userId);
                    }
                }
                catch
                {
                    // Log nhưng không throw exception để không ảnh hưởng đến việc tạo comment
                    // Notification failure shouldn't affect comment creation
                }
            }
            
            return Ok(result);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<CommentResponseDTO>> UpdateComment(int id, UpdateCommentDTO dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
                return Unauthorized();

            var result = await _commentService.UpdateCommentAsync(id, dto, userId);
            if (result == null)
                return NotFound("Comment not found or you are not authorized to update this comment");

            return Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> DeleteComment(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
                return Unauthorized();

            var result = await _commentService.DeleteCommentAsync(id, userId);
            if (!result)
                return NotFound("Comment not found or you are not authorized to delete this comment");

            return NoContent();
        }        [HttpPost("reaction")]
        [Authorize]
        public async Task<ActionResult> AddReaction(CommentReactionDTO dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
                return Unauthorized();
                
            var result = await _commentService.AddOrToggleReactionAsync(dto, userId);
            
            // Tạo thông báo cho chủ comment (nếu không phải chính mình và là thêm reaction mới)
            if (result != null)
            {
                try
                {
                    await _notificationService.CreateCommentLikeNotificationAsync(dto.CommentId, userId);
                }
                catch
                {
                    // Log nhưng không throw exception để không ảnh hưởng đến việc tạo reaction
                    // Notification failure shouldn't affect reaction creation
                }
            }
            
            return Ok(result);
        }

        [HttpGet("replies/{commentId}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<CommentResponseDTO>>> GetRepliesByCommentId(int commentId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            var result = await _commentService.GetRepliesByCommentIdAsync(commentId, userId);
            return Ok(result);
        }

        #endregion

        #region Comment Reporting

        [HttpPost("report")]
        [Authorize]
        public async Task<ActionResult<CommentReportResponseDTO>> ReportComment(CreateCommentReportDTO reportDto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
                return Unauthorized();
                
            var result = await _commentReportService.CreateCommentReportAsync(reportDto, userId);
            
            if (result == null)
            {
                return BadRequest("Failed to report comment. It's possible you have already reported this comment.");
            }
            
            return Ok(result);
        }
        
        [HttpPut("report/{reportId}")]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<ActionResult<CommentReportResponseDTO>> UpdateReportStatus(int reportId, UpdateCommentReportStatusDTO statusDto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
                return Unauthorized();
                
            var result = await _commentReportService.UpdateCommentReportStatusAsync(reportId, statusDto, userId);
            
            if (result == null)
            {
                return NotFound("Report not found or you don't have permission to update it.");
            }
            
            return Ok(result);
        }
        
        [HttpGet("reports")]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<ActionResult<List<CommentReportResponseDTO>>> GetReportsByStatus([FromQuery] string status = "Pending")
        {
            var result = await _commentReportService.GetCommentReportsByStatusAsync(status);
            return Ok(result);
        }
        
        [HttpGet("report/{reportId}")]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<ActionResult<CommentReportResponseDTO>> GetReportById(int reportId)
        {
            var result = await _commentReportService.GetCommentReportByIdAsync(reportId);
            
            if (result == null)
            {
                return NotFound("Report not found.");
            }
            
            return Ok(result);
        }

        #endregion
    }
}
