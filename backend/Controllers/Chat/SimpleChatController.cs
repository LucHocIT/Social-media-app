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
    }    /// <summary>
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

            // Validate: Either content or media must be provided
            bool hasContent = !string.IsNullOrWhiteSpace(messageDto.Content);
            bool hasMedia = !string.IsNullOrEmpty(messageDto.MediaUrl);
            
            if (!hasContent && !hasMedia)
            {
                return BadRequest("Either message content or media must be provided");
            }

            if (hasContent && messageDto.Content!.Length > 1000)
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
    }    /// <summary>
    /// Upload media for chat message
    /// </summary>
    /// <param name="uploadDto">The upload data containing media file and type</param>
    /// <returns>Upload result with media URL and metadata</returns>
    [HttpPost("upload-media")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadChatMedia([FromForm] UploadChatMediaDto uploadDto)    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Unauthorized("User not authenticated");
            }

            if (uploadDto?.MediaFile == null || uploadDto.MediaFile.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            // Validate media type
            if (!IsValidMediaType(uploadDto.MediaType))
            {
                return BadRequest("Invalid media type. Allowed values are 'image', 'video', and 'file'.");
            }

            // Validate file size
            long maxSize = GetMaxFileSizeForMediaType(uploadDto.MediaType);
            if (uploadDto.MediaFile.Length > maxSize)
            {
                return BadRequest($"File size exceeds the maximum allowed ({maxSize / (1024 * 1024)}MB).");
            }

            // Validate MIME type
            var allowedTypes = GetAllowedMimeTypes(uploadDto.MediaType);
            if (!allowedTypes.Contains(uploadDto.MediaFile.ContentType.ToLower()))
            {
                return BadRequest($"Invalid file type for {uploadDto.MediaType}. Allowed types: {string.Join(", ", allowedTypes)}");
            }

            var result = await _simpleChatService.UploadChatMediaAsync(currentUserId.Value, uploadDto.MediaFile, uploadDto.MediaType);
            
            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(new
            {
                success = true,
                mediaUrl = result.MediaUrl,
                mediaType = result.MediaType,
                publicId = result.PublicId,
                mimeType = result.MimeType,
                filename = result.Filename,
                fileSize = result.FileSize,
                message = "Media uploaded successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading chat media");
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
    }    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        {
            return userId;
        }
        return null;
    }

    private bool IsValidMediaType(string mediaType)
    {
        if (string.IsNullOrEmpty(mediaType))
            return false;
            
        var validTypes = new[] { "image", "video", "file" };
        return validTypes.Contains(mediaType.ToLower());
    }
    
    private string[] GetAllowedMimeTypes(string mediaType)
    {
        switch (mediaType.ToLower())
        {
            case "image":
                return new[] { 
                    "image/jpeg", "image/png", "image/gif", "image/webp", 
                    "image/svg+xml", "image/bmp", "image/tiff" 
                };
            case "video":
                return new[] { 
                    "video/mp4", "video/mpeg", "video/quicktime", "video/x-msvideo", 
                    "video/x-ms-wmv", "video/webm", "video/x-flv" 
                };
            case "file":
                return new[] { 
                    "application/pdf", "application/msword", "application/vnd.ms-excel",
                    "application/vnd.ms-powerpoint", "text/plain", "application/zip",
                    "application/x-rar-compressed", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "application/vnd.openxmlformats-officedocument.presentationml.presentation"
                };
            default:
                return new string[0];
        }
    }
    
    private long GetMaxFileSizeForMediaType(string mediaType)
    {
        switch (mediaType.ToLower())
        {
            case "image":
                return 10 * 1024 * 1024; // 10 MB for images
            case "video":
                return 100 * 1024 * 1024; // 100 MB for videos
            case "file":
                return 25 * 1024 * 1024; // 25 MB for other files
            default:
                return 5 * 1024 * 1024; // 5 MB default
        }
    }
}
