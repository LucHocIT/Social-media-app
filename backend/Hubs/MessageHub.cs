using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using SocialApp.Services.Message;
using SocialApp.DTOs;

namespace SocialApp.Hubs;

[Authorize]
public class MessageHub : Hub
{
    private readonly IMessageService _messageService;
    private readonly IRedisMessageService _redisService;
    private readonly ILogger<MessageHub> _logger;

    public MessageHub(
        IMessageService messageService,
        IRedisMessageService redisService,
        ILogger<MessageHub> logger)
    {
        _messageService = messageService;
        _redisService = redisService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                // Add user to their personal group
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId.Value}");

                // Set user online status
                await _messageService.UpdateUserOnlineStatusAsync(userId.Value, true, Context.ConnectionId);

                // Join conversation groups for this user
                await JoinUserConversationGroups(userId.Value);

                _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", userId.Value, Context.ConnectionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnConnectedAsync for connection {ConnectionId}", Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _messageService.UpdateUserOnlineStatusAsync(userId.Value, false, Context.ConnectionId);
                _logger.LogInformation("User {UserId} disconnected with connection {ConnectionId}", userId.Value, Context.ConnectionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnDisconnectedAsync for connection {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    #region Message Operations

    /// <summary>
    /// Send a message to another user
    /// </summary>
    public async Task SendMessage(SendMessageDTO messageDto)
    {
        try
        {
            var senderId = GetCurrentUserId();
            if (!senderId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            var result = await _messageService.SendMessageAsync(senderId.Value, messageDto);

            if (result.Success && result.MessageData != null)
            {
                // Send to sender (confirmation)
                await Clients.Caller.SendAsync("MessageSent", result.MessageData);

                // Send to receiver if they're online
                var receiverConnections = await _redisService.GetUserConnectionsAsync(messageDto.ReceiverId);
                if (receiverConnections.Any())
                {
                    await Clients.Clients(receiverConnections).SendAsync("MessageReceived", result.MessageData);
                }

                // Notify conversation group about new message
                var conversationDto = await _messageService.GetOrCreateConversationAsync(senderId.Value, messageDto.ReceiverId);
                if (conversationDto != null)
                {
                    await Clients.Group($"Conversation_{conversationDto.Id}")
                        .SendAsync("ConversationUpdated", conversationDto);
                }
            }
            else
            {
                await Clients.Caller.SendAsync("Error", result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            await Clients.Caller.SendAsync("Error", "Failed to send message");
        }
    }

    /// <summary>
    /// Join a specific conversation to receive realtime updates
    /// </summary>
    public async Task JoinConversation(int conversationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return;

            // Verify user is part of this conversation
            var conversation = await _messageService.GetOrCreateConversationAsync(userId.Value, 0); // This needs improvement
                                                                                                    // For now, just join the group - in production, verify user belongs to conversation

            await Groups.AddToGroupAsync(Context.ConnectionId, $"Conversation_{conversationId}");
            _logger.LogInformation("User {UserId} joined conversation {ConversationId}", userId.Value, conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining conversation {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// Leave a conversation group
    /// </summary>
    public async Task LeaveConversation(int conversationId)
    {
        try
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Conversation_{conversationId}");

            var userId = GetCurrentUserId();
            _logger.LogInformation("User {UserId} left conversation {ConversationId}", userId, conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving conversation {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// Mark messages as read in a conversation
    /// </summary>
    public async Task MarkAsRead(MarkAsReadDTO markAsReadDto)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return;

            var success = await _messageService.MarkMessagesAsReadAsync(
                userId.Value,
                markAsReadDto.ConversationId,
                markAsReadDto.LastReadMessageId);

            if (success)
            {
                // Notify sender that their messages were read
                await Clients.Group($"Conversation_{markAsReadDto.ConversationId}")
                    .SendAsync("MessagesMarkedAsRead", new
                    {
                        ConversationId = markAsReadDto.ConversationId,
                        ReadByUserId = userId.Value,
                        LastReadMessageId = markAsReadDto.LastReadMessageId,
                        ReadAt = DateTime.UtcNow
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking messages as read");
        }
    }

    #endregion

    #region Typing Indicators

    /// <summary>
    /// Set typing status in a conversation
    /// </summary>
    public async Task SetTyping(int conversationId, bool isTyping)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return;

            await _messageService.SetTypingStatusAsync(userId.Value, conversationId, isTyping);

            // Notify others in the conversation
            await Clients.GroupExcept($"Conversation_{conversationId}", Context.ConnectionId)
                .SendAsync("TypingStatusChanged", new TypingIndicatorDTO
                {
                    ConversationId = conversationId,
                    UserId = userId.Value,
                    UserName = Context.User?.Identity?.Name ?? "Unknown",
                    IsTyping = isTyping
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting typing status");
        }
    }

    #endregion

    #region User Status

    /// <summary>
    /// Get online status of users
    /// </summary>
    public async Task GetUserOnlineStatus(List<int> userIds)
    {
        try
        {
            var onlineStatuses = new Dictionary<int, bool>();

            foreach (var userId in userIds)
            {
                var isOnline = await _messageService.IsUserOnlineAsync(userId);
                onlineStatuses[userId] = isOnline;
            }

            await Clients.Caller.SendAsync("UserOnlineStatuses", onlineStatuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user online statuses");
        }
    }

    #endregion

    #region Helper Methods

    private int? GetCurrentUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out int userId))
        {
            return userId;
        }
        return null;
    }

    private async Task JoinUserConversationGroups(int userId)
    {
        try
        {
            // Get user's conversations and join their groups
            var conversations = await _messageService.GetUserConversationsAsync(userId, 1, 50);
            foreach (var conversation in conversations)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Conversation_{conversation.Id}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining user conversation groups for user {UserId}", userId);
        }
    }

    #endregion
}
