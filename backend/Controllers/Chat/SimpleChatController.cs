using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialApp.DTOs;
using SocialApp.Services.Chat;
using System.Security.Claims;

namespace SocialApp.Controllers.Chat;

[ApiController]
[Route("api/simple-chat")]
[Authorize]
public class SimpleChatController : ControllerBase
{
    private readonly ISimpleChatService _simpleChatService;
    private readonly ILogger<SimpleChatController> _logger;

    public SimpleChatController(ISimpleChatService simpleChatService, ILogger<SimpleChatController> logger)
    {
        _simpleChatService = simpleChatService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách cuộc trò chuyện của user hiện tại
    /// </summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Unauthorized("User not authenticated");
            }

            var conversations = await _simpleChatService.GetUserConversationsAsync(currentUserId.Value);
            return Ok(conversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversations for user");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Tạo hoặc lấy cuộc trò chuyện với user khác
    /// </summary>
    [HttpPost("conversations/with/{otherUserId}")]
    public async Task<IActionResult> GetOrCreateConversation(int otherUserId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Unauthorized("User not authenticated");
            }

            if (currentUserId.Value == otherUserId)
            {
                return BadRequest("Cannot create conversation with yourself");
            }

            var conversation = await _simpleChatService.GetOrCreateConversationAsync(currentUserId.Value, otherUserId);
            
            if (conversation == null)
            {
                return BadRequest("You can only chat with friends (mutual followers)");
            }

            return Ok(conversation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating conversation with user {OtherUserId}", otherUserId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Lấy tin nhắn trong cuộc trò chuyện
    /// </summary>
    [HttpGet("conversations/{conversationId}/messages")]
    public async Task<IActionResult> GetMessages(int conversationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Unauthorized("User not authenticated");
            }

            // Giới hạn pageSize để tránh tải quá nhiều dữ liệu
            pageSize = Math.Min(pageSize, 100);

            var messages = await _simpleChatService.GetConversationMessagesAsync(conversationId, currentUserId.Value, page, pageSize);
            return Ok(messages);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid("Access denied to conversation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages for conversation {ConversationId}", conversationId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gửi tin nhắn
    /// </summary>
    [HttpPost("conversations/{conversationId}/messages")]
    public async Task<IActionResult> SendMessage(int conversationId, [FromBody] SendSimpleMessageDto messageDto)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Unauthorized("User not authenticated");
            }

            if (string.IsNullOrWhiteSpace(messageDto.Content))
            {
                return BadRequest("Message content cannot be empty");
            }

            if (messageDto.Content.Length > 1000)
            {
                return BadRequest("Message content too long (max 1000 characters)");
            }

            var message = await _simpleChatService.SendMessageAsync(conversationId, currentUserId.Value, messageDto);
            return Ok(message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid("Access denied to conversation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to conversation {ConversationId}", conversationId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Đánh dấu cuộc trò chuyện đã đọc
    /// </summary>
    [HttpPost("conversations/{conversationId}/read")]
    public async Task<IActionResult> MarkAsRead(int conversationId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Unauthorized("User not authenticated");
            }

            var success = await _simpleChatService.MarkConversationAsReadAsync(conversationId, currentUserId.Value);
            
            if (!success)
            {
                return BadRequest("Failed to mark conversation as read");
            }

            return Ok(new { message = "Marked as read successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking conversation {ConversationId} as read", conversationId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Ẩn cuộc trò chuyện
    /// </summary>
    [HttpDelete("conversations/{conversationId}")]
    public async Task<IActionResult> HideConversation(int conversationId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Unauthorized("User not authenticated");
            }

            var success = await _simpleChatService.HideConversationAsync(conversationId, currentUserId.Value);
            
            if (!success)
            {
                return BadRequest("Failed to hide conversation");
            }

            return Ok(new { message = "Conversation hidden successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hiding conversation {ConversationId}", conversationId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Kiểm tra quan hệ bạn bè với user khác
    /// </summary>
    [HttpGet("friends/{otherUserId}/check")]
    public async Task<IActionResult> CheckFriendship(int otherUserId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Unauthorized("User not authenticated");
            }

            var areFriends = await _simpleChatService.AreFriendsAsync(currentUserId.Value, otherUserId);
            return Ok(new { areFriends });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking friendship with user {OtherUserId}", otherUserId);
            return StatusCode(500, "Internal server error");
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        {
            return userId;
        }
        return null;
    }
}
