using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialApp.DTOs;
using SocialApp.Services.Comment;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SocialApp.Controllers.Post
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommentReportController : ControllerBase
    {
        private readonly ICommentService _commentService;

        public CommentReportController(ICommentService commentService)
        {
            _commentService = commentService;
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<CommentReportResponseDTO>> ReportComment(CreateCommentReportDTO reportDto)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var result = await _commentService.CreateCommentReportAsync(reportDto, userId);
            
            if (result == null)
            {
                return BadRequest("Failed to report comment. It's possible you have already reported this comment.");
            }
            
            return Ok(result);
        }
        
        [HttpPut("{reportId}")]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<ActionResult<CommentReportResponseDTO>> UpdateReportStatus(int reportId, UpdateCommentReportStatusDTO statusDto)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var result = await _commentService.UpdateCommentReportStatusAsync(reportId, statusDto, userId);
            
            if (result == null)
            {
                return NotFound("Report not found or you don't have permission to update it.");
            }
            
            return Ok(result);
        }
        
        [HttpGet]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<ActionResult<List<CommentReportResponseDTO>>> GetReportsByStatus([FromQuery] string status = "Pending")
        {
            var result = await _commentService.GetCommentReportsByStatusAsync(status);
            return Ok(result);
        }
        
        [HttpGet("{reportId}")]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<ActionResult<CommentReportResponseDTO>> GetReportById(int reportId)
        {
            var result = await _commentService.GetCommentReportByIdAsync(reportId);
            
            if (result == null)
            {
                return NotFound("Report not found.");
            }
            
            return Ok(result);
        }
    }
}
