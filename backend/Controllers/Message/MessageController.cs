using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SocialApp.DTOs;
using SocialApp.Services.Message;

namespace SocialApp.Controllers.Message;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessageController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly ILogger<MessageController> _logger;

    public MessageController(IMessageService messageService, ILogger<MessageController> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    #region Conversations

    /// <summary>
    /// Get list of user's conversations
    /// </summary>
    [HttpGet("conversations")]
    public async Task<ActionResult<ConversationListResponseDTO>> GetConversations(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            var conversations = await _messageService.GetUserConversationsAsync(userId.Value, page, pageSize);
            
            return Ok(new ConversationListResponseDTO
            {
                Conversations = conversations,
                TotalCount = conversations.Count,
                HasMore = conversations.Count == pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversations for user");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get or create conversation with specific user
    /// </summary>
    [HttpPost("conversations")]
    public async Task<ActionResult<ConversationDTO>> GetOrCreateConversation([FromBody] int otherUserId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            if (userId.Value == otherUserId)
                return BadRequest("Cannot create conversation with yourself");

            var conversation = await _messageService.GetOrCreateConversationAsync(userId.Value, otherUserId);
            if (conversation == null)
                return BadRequest("Could not create conversation");

            return Ok(conversation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating conversation with user {OtherUserId}", otherUserId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete a conversation
    /// </summary>
    [HttpDelete("conversations/{conversationId}")]
    public async Task<IActionResult> DeleteConversation(int conversationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            await _messageService.DeleteConversationAsync(userId.Value, conversationId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting conversation {ConversationId}", conversationId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Messages

    /// <summary>
    /// Get messages in a conversation
    /// </summary>
    [HttpGet("conversations/{conversationId}/messages")]
    public async Task<ActionResult<ConversationMessagesDTO>> GetMessages(
        int conversationId,
        [FromQuery] DateTime? before = null,
        [FromQuery] int limit = 50)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            var messages = await _messageService.GetConversationMessagesAsync(
                userId.Value, conversationId, before, limit);

            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages for conversation {ConversationId}", conversationId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Send a message (alternative to SignalR)
    /// </summary>
    [HttpPost("send")]
    public async Task<ActionResult<SendMessageResponseDTO>> SendMessage([FromForm] SendMessageDTO messageDto)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            var result = await _messageService.SendMessageAsync(userId.Value, messageDto);
            
            if (result.Success)
                return Ok(result);
            else
                return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Mark messages as read
    /// </summary>
    [HttpPost("conversations/{conversationId}/mark-read")]
    public async Task<IActionResult> MarkAsRead(int conversationId, [FromBody] string? lastReadMessageId = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            var success = await _messageService.MarkMessagesAsReadAsync(userId.Value, conversationId, lastReadMessageId);
            
            if (success)
                return Ok();
            else
                return BadRequest("Could not mark messages as read");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking messages as read");
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region User Status

    /// <summary>
    /// Get total unread message count
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            var count = await _messageService.GetUnreadMessageCountAsync(userId.Value);
            return Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Check if users are online
    /// </summary>
    [HttpPost("users/online-status")]
    public async Task<ActionResult<Dictionary<int, bool>>> GetUsersOnlineStatus([FromBody] List<int> userIds)
    {
        try
        {
            var onlineStatuses = new Dictionary<int, bool>();
            
            foreach (var userId in userIds)
            {
                var isOnline = await _messageService.IsUserOnlineAsync(userId);
                onlineStatuses[userId] = isOnline;
            }

            return Ok(onlineStatuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking online status");
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Typing Indicators

    /// <summary>
    /// Get typing users in conversation
    /// </summary>
    [HttpGet("conversations/{conversationId}/typing")]
    public async Task<ActionResult<List<int>>> GetTypingUsers(int conversationId)
    {
        try
        {
            var typingUsers = await _messageService.GetTypingUsersAsync(conversationId);
            return Ok(typingUsers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting typing users");
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Helper Methods

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out int userId))
        {
            return userId;
        }
        return null;
    }

    #endregion
}
