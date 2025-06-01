using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SocialApp.DTOs;
using SocialApp.Models;
using SocialApp.Services.Chat;
using System.Security.Claims;

namespace SocialApp.Hubs;

[Authorize]
public class SimpleChatHub : Hub
{
    private readonly SocialMediaDbContext _context;
    private readonly ISimpleChatService _simpleChatService;
    private readonly ILogger<SimpleChatHub> _logger;

    public SimpleChatHub(
        SocialMediaDbContext context, 
        ISimpleChatService simpleChatService,
        ILogger<SimpleChatHub> logger)
    {
        _context = context;
        _simpleChatService = simpleChatService;
        _logger = logger;
    }    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            // Cập nhật trạng thái online
            var user = await _context.Users.FindAsync(userId.Value);
            if (user != null)
            {
                user.LastActive = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            // Join vào group cá nhân để nhận tin nhắn
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId.Value}");
            
            // Thông báo cho tất cả user khác rằng user này đã online
            await Clients.All.SendAsync("UserOnline", userId.Value);
            
            _logger.LogInformation($"User {userId.Value} connected to chat");
        }

        await base.OnConnectedAsync();
    }    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            // Cập nhật thời gian offline
            var user = await _context.Users.FindAsync(userId.Value);
            if (user != null)
            {
                user.LastActive = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId.Value}");
            
            // Thông báo cho tất cả user khác rằng user này đã offline
            await Clients.All.SendAsync("UserOffline", userId.Value);
            
            _logger.LogInformation($"User {userId.Value} disconnected from chat");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join vào conversation để nhận tin nhắn realtime
    /// </summary>
    public async Task JoinConversation(int conversationId)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        try
        {
            // Kiểm tra quyền truy cập
            var hasAccess = await _context.ChatConversations
                .AnyAsync(c => c.Id == conversationId && 
                             ((c.User1Id == userId.Value && c.IsUser1Active) || 
                              (c.User2Id == userId.Value && c.IsUser2Active)));

            if (hasAccess)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Conversation_{conversationId}");
                _logger.LogInformation($"User {userId.Value} joined conversation {conversationId}");
                
                // Thông báo user đã online trong conversation
                await Clients.Group($"Conversation_{conversationId}")
                    .SendAsync("UserOnline", new { UserId = userId.Value });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error joining conversation {conversationId} for user {userId.Value}");
        }
    }

    /// <summary>
    /// Leave conversation
    /// </summary>
    public async Task LeaveConversation(int conversationId)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        try
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Conversation_{conversationId}");
            _logger.LogInformation($"User {userId.Value} left conversation {conversationId}");
            
            // Thông báo user đã offline trong conversation
            await Clients.Group($"Conversation_{conversationId}")
                .SendAsync("UserOffline", new { UserId = userId.Value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error leaving conversation {conversationId} for user {userId.Value}");
        }
    }

    /// <summary>
    /// Gửi tin nhắn realtime
    /// </summary>
    public async Task SendMessage(int conversationId, string content, int? replyToMessageId = null)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        try
        {
            if (string.IsNullOrWhiteSpace(content) || content.Length > 1000)
            {
                await Clients.Caller.SendAsync("Error", "Invalid message content");
                return;
            }

            var messageDto = new SendSimpleMessageDto
            {
                Content = content.Trim(),
                ReplyToMessageId = replyToMessageId
            };            // Gửi tin nhắn qua service (không gửi SignalR vì Hub sẽ tự gửi)
            var message = await _simpleChatService.SendMessageAsync(conversationId, userId.Value, messageDto, sendSignalR: false);// Gửi tin nhắn đến tất cả members trong conversation
            await Clients.Group($"Conversation_{conversationId}")
                .SendAsync("ReceiveMessage", message);

            // Send conversation update to other participants (not in current conversation view)
            // This updates the conversation list without showing toast notifications
            var conversation = await _context.ChatConversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation != null)
            {
                var otherUserId = conversation.User1Id == userId.Value ? conversation.User2Id : conversation.User1Id;
                
                // Send conversation list update to the other user
                await Clients.Group($"User_{otherUserId}")
                    .SendAsync("ConversationUpdated", new
                    {
                        ConversationId = conversationId,
                        LastMessage = message.Content,
                        LastMessageTime = message.SentAt,
                        SenderId = message.SenderId,
                        SenderName = message.SenderName,
                        UnreadCount = await GetUnreadCountForUser(conversationId, otherUserId)
                    });
            }

            _logger.LogInformation($"User {userId.Value} sent message to conversation {conversationId}");
        }
        catch (UnauthorizedAccessException)
        {
            await Clients.Caller.SendAsync("Error", "Access denied to conversation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error sending message from user {userId.Value} to conversation {conversationId}");
            await Clients.Caller.SendAsync("Error", "Failed to send message");
        }
    }

    /// <summary>
    /// Đánh dấu đã đọc tin nhắn
    /// </summary>
    public async Task MarkAsRead(int conversationId)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        try
        {
            var success = await _simpleChatService.MarkConversationAsReadAsync(conversationId, userId.Value);
            
            if (success)
            {
                // Thông báo đến conversation về việc đã đọc
                await Clients.Group($"Conversation_{conversationId}")
                    .SendAsync("MessageRead", new { UserId = userId.Value, ConversationId = conversationId });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error marking conversation {conversationId} as read for user {userId.Value}");
        }
    }

    /// <summary>
    /// Thông báo đang typing
    /// </summary>
    public async Task StartTyping(int conversationId)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        try
        {
            await Clients.Group($"Conversation_{conversationId}")
                .SendAsync("UserTyping", new { UserId = userId.Value, IsTyping = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error sending typing status for user {userId.Value} in conversation {conversationId}");
        }
    }

    /// <summary>
    /// Thông báo ngừng typing
    /// </summary>
    public async Task StopTyping(int conversationId)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        try
        {
            await Clients.Group($"Conversation_{conversationId}")
                .SendAsync("UserTyping", new { UserId = userId.Value, IsTyping = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error stopping typing status for user {userId.Value} in conversation {conversationId}");
        }
    }

    /// <summary>
    /// Broadcast message reaction to conversation members
    /// </summary>
    public async Task BroadcastReactionAdded(int conversationId, int messageId, string reactionType, int userId, string userName)
    {
        try
        {
            await Clients.Group($"Conversation_{conversationId}")
                .SendAsync("ReactionAdded", new 
                { 
                    MessageId = messageId,
                    ReactionType = reactionType,
                    UserId = userId,
                    UserName = userName,
                    Timestamp = DateTime.Now
                });
            
            _logger.LogInformation($"Broadcasted reaction added for message {messageId} in conversation {conversationId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error broadcasting reaction added for message {messageId}");
        }
    }

    /// <summary>
    /// Broadcast message reaction removal to conversation members
    /// </summary>
    public async Task BroadcastReactionRemoved(int conversationId, int messageId, string reactionType, int userId)
    {
        try
        {
            await Clients.Group($"Conversation_{conversationId}")
                .SendAsync("ReactionRemoved", new 
                { 
                    MessageId = messageId,
                    ReactionType = reactionType,
                    UserId = userId,
                    Timestamp = DateTime.Now
                });
            
            _logger.LogInformation($"Broadcasted reaction removed for message {messageId} in conversation {conversationId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error broadcasting reaction removed for message {messageId}");
        }
    }

    private int? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        {
            return userId;
        }
        return null;
    }

    private async Task<int> GetUnreadCountForUser(int conversationId, int userId)
    {
        var conversation = await _context.ChatConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null) return 0;

        DateTime? lastRead = conversation.User1Id == userId ? 
                            conversation.User1LastRead : 
                            conversation.User2LastRead;

        if (lastRead == null)
        {
            return await _context.SimpleMessages
                .CountAsync(m => m.ConversationId == conversationId && 
                               m.SenderId != userId && 
                               !m.IsDeleted);
        }

        return await _context.SimpleMessages
            .CountAsync(m => m.ConversationId == conversationId && 
                           m.SenderId != userId && 
                           m.SentAt > lastRead && 
                           !m.IsDeleted);
    }
}
