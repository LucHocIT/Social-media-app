using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SocialApp.DTOs;
using SocialApp.Models;
using SocialApp.Services.Chat;
using SocialApp.Services.User;
using System.Security.Claims;

namespace SocialApp.Hubs;

[Authorize]
public class SimpleChatHub : Hub
{    private readonly SocialMediaDbContext _context;
    private readonly ISimpleChatService _simpleChatService;
    private readonly ILogger<SimpleChatHub> _logger;
    private readonly IUserBlockService _userBlockService;

    public SimpleChatHub(
        SocialMediaDbContext context, 
        ISimpleChatService simpleChatService,
        ILogger<SimpleChatHub> logger,
        IUserBlockService userBlockService)
    {
        _context = context;
        _simpleChatService = simpleChatService;
        _logger = logger;
        _userBlockService = userBlockService;
    }public override async Task OnConnectedAsync()
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
        if (!userId.HasValue) return;        try
        {
            // Kiểm tra quyền truy cập
            var conversation = await _context.ChatConversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && 
                             ((c.User1Id == userId.Value && c.IsUser1Active) || 
                              (c.User2Id == userId.Value && c.IsUser2Active)));

            if (conversation == null)
            {
                await Clients.Caller.SendAsync("Error", "Conversation not found or access denied");
                return;
            }

            // Check for block relationships
            var otherUserId = conversation.User1Id == userId.Value ? conversation.User2Id : conversation.User1Id;
            var areBlocking = await _userBlockService.AreUsersBlockingEachOtherAsync(userId.Value, otherUserId);
            if (areBlocking)
            {
                _logger.LogWarning("User {UserId} attempted to join conversation {ConversationId} but users are blocking each other", 
                    userId.Value, conversationId);
                await Clients.Caller.SendAsync("Error", "Cannot join conversation with blocked user");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"Conversation_{conversationId}");
            _logger.LogInformation($"User {userId.Value} joined conversation {conversationId}");
            
            // Thông báo user đã online trong conversation
            await Clients.Group($"Conversation_{conversationId}")
                .SendAsync("UserOnline", new { UserId = userId.Value });
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
        if (!userId.HasValue) return;        try
        {
            if (string.IsNullOrWhiteSpace(content) || content.Length > 1000)
            {
                await Clients.Caller.SendAsync("Error", "Invalid message content");
                return;
            }

            // Check conversation access and block status
            var conversation = await _context.ChatConversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && 
                             ((c.User1Id == userId.Value && c.IsUser1Active) || 
                              (c.User2Id == userId.Value && c.IsUser2Active)));

            if (conversation == null)
            {
                await Clients.Caller.SendAsync("Error", "Conversation not found or access denied");
                return;
            }

            // Check for block relationships
            var otherUserId = conversation.User1Id == userId.Value ? conversation.User2Id : conversation.User1Id;
            var areBlocking = await _userBlockService.AreUsersBlockingEachOtherAsync(userId.Value, otherUserId);
            if (areBlocking)
            {
                _logger.LogWarning("User {UserId} attempted to send message but users are blocking each other", userId.Value);
                await Clients.Caller.SendAsync("Error", "Cannot send message to blocked user");
                return;
            }

            var messageDto = new SendSimpleMessageDto
            {
                Content = content.Trim(),
                ReplyToMessageId = replyToMessageId
            };// Gửi tin nhắn qua service (không gửi SignalR vì Hub sẽ tự gửi)
            var message = await _simpleChatService.SendMessageAsync(conversationId, userId.Value, messageDto, sendSignalR: false);// Gửi tin nhắn đến tất cả members trong conversation
            await Clients.Group($"Conversation_{conversationId}")
                .SendAsync("ReceiveMessage", message);            // Send conversation update to other participants (not in current conversation view)
            // This updates the conversation list without showing toast notifications
            var conversationForUpdate = await _context.ChatConversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversationForUpdate != null)
            {
                var receiverId = conversationForUpdate.User1Id == userId.Value ? conversationForUpdate.User2Id : conversationForUpdate.User1Id;
                
                // Send conversation list update to the other user
                await Clients.Group($"User_{receiverId}")
                    .SendAsync("ConversationUpdated", new
                    {
                        ConversationId = conversationId,
                        LastMessage = message.Content,
                        LastMessageTime = message.SentAt,
                        SenderId = message.SenderId,
                        SenderName = message.SenderName,
                        UnreadCount = await GetUnreadCountForUser(conversationId, receiverId)
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
    }    /// <summary>
    /// Gửi location message tạm thời (không lưu vào database, chỉ qua SignalR)
    /// </summary>
    public async Task SendTemporaryLocationMessage(int conversationId, double latitude, double longitude, string address)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        try
        {
            if (string.IsNullOrWhiteSpace(address) || latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
            {
                await Clients.Caller.SendAsync("Error", "Invalid location data");
                return;
            }

            // Check conversation access and block status
            var conversation = await _context.ChatConversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .FirstOrDefaultAsync(c => c.Id == conversationId && 
                             ((c.User1Id == userId.Value && c.IsUser1Active) || 
                              (c.User2Id == userId.Value && c.IsUser2Active)));

            if (conversation == null)
            {
                await Clients.Caller.SendAsync("Error", "Conversation not found or access denied");
                return;
            }

            // Check for block relationships
            var otherUserId = conversation.User1Id == userId.Value ? conversation.User2Id : conversation.User1Id;
            var areBlocking = await _userBlockService.AreUsersBlockingEachOtherAsync(userId.Value, otherUserId);
            if (areBlocking)
            {
                _logger.LogWarning("User {UserId} attempted to send location message but users are blocking each other", userId.Value);
                await Clients.Caller.SendAsync("Error", "Cannot send location to blocked user");
                return;
            }

            // Tạo temporary location message object (không lưu vào database)
            var currentUser = conversation.User1Id == userId.Value ? conversation.User1 : conversation.User2;
            var temporaryLocationMessage = new
            {
                Id = -1, // Temporary ID để phân biệt với database messages
                ConversationId = conversationId,
                SenderId = userId.Value,
                SenderName = currentUser.Username ?? currentUser.Email,
                SenderAvatar = currentUser.ProfilePictureUrl,
                Content = address, // Address as content
                MessageType = "location",
                LocationData = new
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    Address = address
                },
                SentAt = DateTime.Now,
                IsTemporary = true,
                ExpiresAt = DateTime.Now.AddHours(1), // Hết hạn sau 1 giờ
                IsRead = false,
                IsEdited = false,
                IsDeleted = false,
                ReplyToMessage = (object?)null,
                Reactions = new object[0]
            };

            // Gửi location message đến tất cả members trong conversation
            await Clients.Group($"Conversation_{conversationId}")
                .SendAsync("ReceiveMessage", temporaryLocationMessage);

            // Send conversation update to other participants
            var receiverId = conversation.User1Id == userId.Value ? conversation.User2Id : conversation.User1Id;
            
            // Send conversation list update to the other user
            await Clients.Group($"User_{receiverId}")
                .SendAsync("ConversationUpdated", new
                {
                    ConversationId = conversationId,
                    LastMessage = "📍 Đã chia sẻ vị trí",
                    LastMessageTime = temporaryLocationMessage.SentAt,
                    SenderId = temporaryLocationMessage.SenderId,
                    SenderName = temporaryLocationMessage.SenderName,
                    UnreadCount = await GetUnreadCountForUser(conversationId, receiverId)
                });

            _logger.LogInformation($"User {userId.Value} sent temporary location message to conversation {conversationId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error sending temporary location message from user {userId.Value} to conversation {conversationId}");
            await Clients.Caller.SendAsync("Error", "Failed to send location");
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
