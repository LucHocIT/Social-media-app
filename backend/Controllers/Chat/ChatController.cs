using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialApp.DTOs;
using SocialApp.Models;
using SocialApp.Services.Chat;
using System.Security.Claims;

namespace SocialApp.Controllers.Chat
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatService chatService, ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }        [HttpPost("rooms")]
        public async Task<IActionResult> CreateChatRoom([FromBody] CreateChatRoomDto createChatRoomDto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized("User not authenticated");
                }

                // Prevent creating private chats through this endpoint
                if (createChatRoomDto.Type == ChatRoomType.Private)
                {
                    return BadRequest("Private chats must be created through the /api/chat/private/{otherUserId} endpoint to prevent duplicates");
                }

                var chatRoom = await _chatService.CreateChatRoomAsync(currentUserId.Value, createChatRoomDto);
                return Ok(chatRoom);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat room");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("rooms")]
        public async Task<IActionResult> GetUserChatRooms([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized("User not authenticated");
                }

                var chatRooms = await _chatService.GetUserChatRoomsAsync(currentUserId.Value, page, pageSize);
                return Ok(chatRooms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user chat rooms");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("rooms/{chatRoomId}")]
        public async Task<IActionResult> GetChatRoom(int chatRoomId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized("User not authenticated");
                }

                var chatRoom = await _chatService.GetChatRoomAsync(chatRoomId, currentUserId.Value);
                if (chatRoom == null)
                {
                    return NotFound("Chat room not found or access denied");
                }

                return Ok(chatRoom);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat room {ChatRoomId}", chatRoomId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("rooms/{chatRoomId}/messages")]
        public async Task<IActionResult> GetChatMessages(int chatRoomId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized("User not authenticated");
                }

                var messages = await _chatService.GetChatMessagesAsync(chatRoomId, currentUserId.Value, page, pageSize);
                return Ok(messages);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid("Access denied to chat room");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat messages for room {ChatRoomId}", chatRoomId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("rooms/{chatRoomId}/messages")]
        public async Task<IActionResult> SendMessage(int chatRoomId, [FromBody] SendMessageDto sendMessageDto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized("User not authenticated");
                }

                var message = await _chatService.SendMessageAsync(chatRoomId, currentUserId.Value, sendMessageDto);
                return Ok(message);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid("Access denied to chat room");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to room {ChatRoomId}", chatRoomId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("rooms/{chatRoomId}/members")]
        public async Task<IActionResult> AddMember(int chatRoomId, [FromBody] AddMemberDto addMemberDto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized("User not authenticated");
                }

                var success = await _chatService.AddMemberToChatRoomAsync(chatRoomId, currentUserId.Value, addMemberDto);
                if (!success)
                {
                    return BadRequest("Failed to add member. User may already be a member or you may not have permission.");
                }

                return Ok(new { message = "Member added successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding member to chat room {ChatRoomId}", chatRoomId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("rooms/{chatRoomId}/members/{memberUserId}")]
        public async Task<IActionResult> RemoveMember(int chatRoomId, int memberUserId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized("User not authenticated");
                }

                var success = await _chatService.RemoveMemberFromChatRoomAsync(chatRoomId, currentUserId.Value, memberUserId);
                if (!success)
                {
                    return BadRequest("Failed to remove member. You may not have permission or the user is not a member.");
                }

                return Ok(new { message = "Member removed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing member from chat room {ChatRoomId}", chatRoomId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("rooms/{chatRoomId}/leave")]
        public async Task<IActionResult> LeaveChatRoom(int chatRoomId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized("User not authenticated");
                }

                var success = await _chatService.LeaveChatRoomAsync(chatRoomId, currentUserId.Value);
                if (!success)
                {
                    return BadRequest("Failed to leave chat room. You may not be a member.");
                }

                return Ok(new { message = "Left chat room successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving chat room {ChatRoomId}", chatRoomId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("rooms/{chatRoomId}")]
        public async Task<IActionResult> DeleteChatRoom(int chatRoomId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized("User not authenticated");
                }

                var success = await _chatService.DeleteChatRoomAsync(chatRoomId, currentUserId.Value);
                if (!success)
                {
                    return BadRequest("Failed to delete chat room. You may not have permission.");
                }

                return Ok(new { message = "Chat room deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting chat room {ChatRoomId}", chatRoomId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("private/{otherUserId}")]
        public async Task<IActionResult> GetOrCreatePrivateChat(int otherUserId)
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
                    return BadRequest("Cannot create private chat with yourself");
                }

                var chatRoom = await _chatService.GetOrCreatePrivateChatAsync(currentUserId.Value, otherUserId);
                if (chatRoom == null)
                {
                    return BadRequest("Unable to create private chat. User may not exist.");
                }

                return Ok(chatRoom);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating private chat with user {OtherUserId}", otherUserId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("rooms/{chatRoomId}/messages/read")]
        public async Task<IActionResult> MarkMessagesAsRead(int chatRoomId, [FromBody] List<int> messageIds)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized("User not authenticated");
                }

                var success = await _chatService.MarkMessagesAsReadAsync(chatRoomId, currentUserId.Value, messageIds);
                if (!success)
                {
                    return BadRequest("Failed to mark messages as read. You may not be a member of this chat room.");
                }

                return Ok(new { message = "Messages marked as read successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read in room {ChatRoomId}", chatRoomId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("users/search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string searchTerm)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized("User not authenticated");
                }

                if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
                {
                    return BadRequest("Search term must be at least 2 characters long");
                }

                var users = await _chatService.SearchUsersForChatAsync(searchTerm, currentUserId.Value);
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users with term {SearchTerm}", searchTerm);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("rooms/{chatRoomId}")]
        public async Task<IActionResult> UpdateChatRoom(int chatRoomId, [FromBody] UpdateChatRoomDto updateDto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized("User not authenticated");
                }

                var success = await _chatService.UpdateChatRoomAsync(chatRoomId, currentUserId.Value, updateDto.Name, updateDto.Description);
                if (!success)
                {
                    return BadRequest("Failed to update chat room. You may not have permission.");
                }

                return Ok(new { message = "Chat room updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating chat room {ChatRoomId}", chatRoomId);
                return StatusCode(500, "Internal server error");
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }
    }

    public class UpdateChatRoomDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
