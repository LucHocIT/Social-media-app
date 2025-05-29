using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialApp.DTOs;
using SocialApp.Services.Message;
using System.Security.Claims;

namespace SocialApp.Controllers.Message;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ConversationController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly ILogger<ConversationController> _logger;

    public ConversationController(IMessageService messageService, ILogger<ConversationController> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetUserConversations([FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Invalid user token");
            }

            var conversations = await _messageService.GetUserConversationsAsync(userId, page, limit);
            return Ok(conversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user conversations");
            return StatusCode(500, new { Error = "Failed to get conversations" });
        }
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartConversation([FromBody] StartConversationDTO dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Invalid user token");
            }

            if (userId == dto.OtherUserId)
            {
                return BadRequest("Cannot start conversation with yourself");
            }

            var conversation = await _messageService.GetOrCreateConversationAsync(userId, dto.OtherUserId);
            return Ok(conversation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting conversation");
            return StatusCode(500, new { Error = "Failed to start conversation" });
        }
    }

    [HttpGet("{conversationId}/messages")]
    public async Task<IActionResult> GetConversationMessages(
        int conversationId,
        [FromQuery] DateTime? before = null,
        [FromQuery] int limit = 50)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Invalid user token");
            }

            var messages = await _messageService.GetConversationMessagesAsync(userId, conversationId, before, limit);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversation messages");
            return StatusCode(500, new { Error = "Failed to get messages" });
        }
    }

    [HttpPost("{conversationId}/mark-read")]
    public async Task<IActionResult> MarkAsRead(int conversationId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Invalid user token");
            }            var success = await _messageService.MarkMessagesAsReadAsync(userId, conversationId);
            if (!success)
            {
                return BadRequest("Failed to mark conversation as read");
            }

            return Ok(new { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking conversation as read");
            return StatusCode(500, new { Error = "Failed to mark as read" });
        }
    }

    [HttpDelete("{conversationId}")]
    public async Task<IActionResult> DeleteConversation(int conversationId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Invalid user token");
            }

            await _messageService.DeleteConversationAsync(userId, conversationId);
            return Ok(new { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting conversation");
            return StatusCode(500, new { Error = "Failed to delete conversation" });
        }
    }
}

public class StartConversationDTO
{
    public int OtherUserId { get; set; }
}
