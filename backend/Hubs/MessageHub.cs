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

    #region Message Operations    /// <summary>
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

            _logger.LogInformation("Processing message from {SenderId} to {ReceiverId}", senderId.Value, messageDto.ReceiverId);

            var result = await _messageService.SendMessageAsync(senderId.Value, messageDto);            if (result.Success && result.MessageData != null)
            {
                // Get or create conversation info
                var conversationDto = await _messageService.GetOrCreateConversationAsync(senderId.Value, messageDto.ReceiverId);
                if (conversationDto != null)
                {
                    _logger.LogInformation("Message sent successfully in conversation {ConversationId}", conversationDto.Id);

                    // Ensure both users are in the conversation group
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Conversation_{conversationDto.Id}");
                    
                    // Try to add receiver's connections to the group as well
                    var receiverConnections = await _redisService.GetUserConnectionsAsync(messageDto.ReceiverId);
                    foreach (var connectionId in receiverConnections)
                    {
                        await Groups.AddToGroupAsync(connectionId, $"Conversation_{conversationDto.Id}");
                    }

                    // Create message object for frontend
                    var messageForFrontend = new
                    {
                        id = result.MessageData.Id,
                        content = result.MessageData.Content,
                        senderId = result.MessageData.SenderId,
                        receiverId = messageDto.ReceiverId,
                        conversationId = conversationDto.Id,
                        sentAt = result.MessageData.SentAt,
                        isRead = result.MessageData.IsRead,
                        messageType = result.MessageData.MessageType,
                        attachments = result.MessageData.Attachments
                    };

                    // Send to conversation group (both sender and receiver)
                    await Clients.Group($"Conversation_{conversationDto.Id}")
                        .SendAsync("ReceiveMessage", messageForFrontend);

                    // Also send to individual user groups as backup
                    await Clients.Group($"User_{senderId.Value}")
                        .SendAsync("MessageSent", messageForFrontend);
                    
                    await Clients.Group($"User_{messageDto.ReceiverId}")
                        .SendAsync("ReceiveMessage", messageForFrontend);

                    // Update conversation for both users
                    var conversationUpdate = new
                    {
                        id = conversationDto.Id,
                        lastMessage = messageForFrontend,
                        lastMessageTime = result.MessageData.SentAt,
                        lastMessageContent = result.MessageData.Content
                    };

                    await Clients.Group($"Conversation_{conversationDto.Id}")
                        .SendAsync("ConversationUpdated", conversationUpdate);
                        
                    _logger.LogInformation("Message broadcasted from {SenderId} to {ReceiverId} in conversation {ConversationId}", 
                        senderId.Value, messageDto.ReceiverId, conversationDto.Id);
                }
                else
                {
                    _logger.LogError("Failed to get conversation for message from {SenderId} to {ReceiverId}", senderId.Value, messageDto.ReceiverId);
                    await Clients.Caller.SendAsync("Error", "Failed to get conversation");
                }
            }
            else
            {
                _logger.LogError("Failed to send message from {SenderId} to {ReceiverId}: {Message}", senderId.Value, messageDto.ReceiverId, result.Message);
                await Clients.Caller.SendAsync("Error", result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message from {SenderId} to {ReceiverId}", GetCurrentUserId(), messageDto.ReceiverId);
            await Clients.Caller.SendAsync("Error", "Failed to send message");
        }
    }/// <summary>
    /// Join a specific conversation to receive realtime updates
    /// </summary>
    public async Task JoinConversation(int conversationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return;

            // Get conversation to verify user belongs to it
            var conversations = await _messageService.GetUserConversationsAsync(userId.Value, 1, 100);
            var conversation = conversations.FirstOrDefault(c => c.Id == conversationId);
            
            if (conversation != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Conversation_{conversationId}");
                _logger.LogInformation("User {UserId} joined conversation {ConversationId}", userId.Value, conversationId);
            }
            else
            {
                _logger.LogWarning("User {UserId} attempted to join conversation {ConversationId} they don't belong to", userId.Value, conversationId);
            }
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
    }    private async Task JoinUserConversationGroups(int userId)
    {
        try
        {
            // Get user's conversations and join their groups
            var conversations = await _messageService.GetUserConversationsAsync(userId, 1, 100);
            foreach (var conversation in conversations)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Conversation_{conversation.Id}");
                _logger.LogDebug("User {UserId} auto-joined conversation {ConversationId} on connect", userId, conversation.Id);
            }
            _logger.LogInformation("User {UserId} joined {Count} conversation groups", userId, conversations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining user conversation groups for user {UserId}", userId);
        }
    }

    #endregion
}
