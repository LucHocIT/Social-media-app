using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialApp.DTOs;
using SocialApp.Services.Comment;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SocialApp.Controllers.Post
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommentReportController : ControllerBase
    {
        private readonly ICommentReportService _commentReportService;

        public CommentReportController(ICommentReportService commentReportService)
        {
            _commentReportService = commentReportService;
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<CommentReportResponseDTO>> ReportComment(CreateCommentReportDTO reportDto)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var result = await _commentReportService.CreateCommentReportAsync(reportDto, userId);
            
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
            var result = await _commentReportService.UpdateCommentReportStatusAsync(reportId, statusDto, userId);
            
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
            var result = await _commentReportService.GetCommentReportsByStatusAsync(status);
            return Ok(result);
        }
        
        [HttpGet("{reportId}")]
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
    }
}
