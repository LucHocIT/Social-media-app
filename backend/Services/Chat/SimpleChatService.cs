using Microsoft.EntityFrameworkCore;
using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Services.Chat;

public class SimpleChatService : ISimpleChatService
{
    private readonly SocialMediaDbContext _context;
    private readonly ILogger<SimpleChatService> _logger;

    public SimpleChatService(SocialMediaDbContext context, ILogger<SimpleChatService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ConversationsListDto> GetUserConversationsAsync(int userId)
    {
        var conversations = await _context.ChatConversations
            .Where(c => (c.User1Id == userId && c.IsUser1Active) || (c.User2Id == userId && c.IsUser2Active))
            .Include(c => c.User1)
            .Include(c => c.User2)
            .OrderByDescending(c => c.LastMessageTime ?? c.CreatedAt)
            .ToListAsync();

        var conversationDtos = new List<SimpleConversationDto>();

        foreach (var conv in conversations)
        {
            var otherUser = conv.User1Id == userId ? conv.User2 : conv.User1;
            var unreadCount = await GetUnreadCountAsync(conv.Id, userId);            conversationDtos.Add(new SimpleConversationDto
            {
                Id = conv.Id,
                OtherUserId = otherUser.Id,
                OtherUserName = $"{otherUser.FirstName} {otherUser.LastName}".Trim(),
                OtherUserAvatar = otherUser.ProfilePictureUrl,
                LastMessage = conv.LastMessage,
                LastMessageTime = conv.LastMessageTime,
                UnreadCount = unreadCount,
                IsOtherUserOnline = otherUser.LastActive.HasValue && 
                                   otherUser.LastActive.Value > DateTime.Now.AddMinutes(-1),
                OtherUserLastActive = otherUser.LastActive
            });
        }

        return new ConversationsListDto
        {
            Conversations = conversationDtos,
            TotalCount = conversationDtos.Count
        };
    }

    public async Task<SimpleConversationDto?> GetOrCreateConversationAsync(int currentUserId, int otherUserId)
    {
        // Kiểm tra quan hệ bạn bè
        if (!await AreFriendsAsync(currentUserId, otherUserId))
        {
            return null; // Chỉ cho phép chat giữa bạn bè
        }

        // Tìm cuộc trò chuyện hiện có
        var existingConversation = await _context.ChatConversations
            .Include(c => c.User1)
            .Include(c => c.User2)
            .FirstOrDefaultAsync(c => 
                (c.User1Id == currentUserId && c.User2Id == otherUserId) ||
                (c.User1Id == otherUserId && c.User2Id == currentUserId));

        if (existingConversation != null)
        {
            // Kích hoạt lại nếu user đã ẩn
            if (existingConversation.User1Id == currentUserId && !existingConversation.IsUser1Active)
            {
                existingConversation.IsUser1Active = true;
                existingConversation.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }
            else if (existingConversation.User2Id == currentUserId && !existingConversation.IsUser2Active)
            {
                existingConversation.IsUser2Active = true;
                existingConversation.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            var otherUser = existingConversation.User1Id == currentUserId ? 
                           existingConversation.User2 : existingConversation.User1;            return new SimpleConversationDto
            {
                Id = existingConversation.Id,
                OtherUserId = otherUser.Id,
                OtherUserName = $"{otherUser.FirstName} {otherUser.LastName}".Trim(),
                OtherUserAvatar = otherUser.ProfilePictureUrl,
                LastMessage = existingConversation.LastMessage,
                LastMessageTime = existingConversation.LastMessageTime,
                UnreadCount = await GetUnreadCountAsync(existingConversation.Id, currentUserId),
                IsOtherUserOnline = otherUser.LastActive.HasValue && 
                                   otherUser.LastActive.Value > DateTime.Now.AddMinutes(-1),
                OtherUserLastActive = otherUser.LastActive
            };
        }

        // Tạo cuộc trò chuyện mới
        var newConversation = new ChatConversation
        {
            User1Id = Math.Min(currentUserId, otherUserId), // Đảm bảo thứ tự nhất quán
            User2Id = Math.Max(currentUserId, otherUserId),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _context.ChatConversations.Add(newConversation);
        await _context.SaveChangesAsync();

        // Load user info cho response
        await _context.Entry(newConversation)
            .Reference(c => c.User1)
            .LoadAsync();
        await _context.Entry(newConversation)
            .Reference(c => c.User2)
            .LoadAsync();

        var responseOtherUser = newConversation.User1Id == currentUserId ? 
                               newConversation.User2 : newConversation.User1;        return new SimpleConversationDto
        {
            Id = newConversation.Id,
            OtherUserId = responseOtherUser.Id,
            OtherUserName = $"{responseOtherUser.FirstName} {responseOtherUser.LastName}".Trim(),
            OtherUserAvatar = responseOtherUser.ProfilePictureUrl,
            UnreadCount = 0,
            IsOtherUserOnline = responseOtherUser.LastActive.HasValue && 
                               responseOtherUser.LastActive.Value > DateTime.Now.AddMinutes(-1),
            OtherUserLastActive = responseOtherUser.LastActive
        };
    }

    public async Task<ConversationMessagesResponseDto> GetConversationMessagesAsync(int conversationId, int currentUserId, int page = 1, int pageSize = 50)
    {
        // Kiểm tra quyền truy cập
        var conversation = await _context.ChatConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && 
                                 ((c.User1Id == currentUserId && c.IsUser1Active) || 
                                  (c.User2Id == currentUserId && c.IsUser2Active)));

        if (conversation == null)
        {
            throw new UnauthorizedAccessException("Access denied to conversation");
        }

        // Lấy tin nhắn với phân trang (mới nhất trước)
        var query = _context.SimpleMessages
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage)
            .OrderByDescending(m => m.SentAt);

        var totalCount = await query.CountAsync();
        
        var messages = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();        var messageDtos = messages.Select(m => new SimpleMessageDto
        {
            Id = m.Id,
            SenderId = m.SenderId,
            SenderName = $"{m.Sender.FirstName} {m.Sender.LastName}".Trim(),
            SenderAvatar = m.Sender.ProfilePictureUrl,
            Content = m.Content,
            SentAt = m.SentAt,
            IsMine = m.SenderId == currentUserId,
            ReplyToMessageId = m.ReplyToMessageId,
            ReplyToContent = m.ReplyToMessage?.Content
        }).ToList();

        // Debug logging
        foreach (var dto in messageDtos)
        {
            _logger.LogInformation("Message from {SenderName} (ID: {SenderId}): Avatar = {Avatar}", 
                dto.SenderName, dto.SenderId, dto.SenderAvatar ?? "NULL");
        }

        // Reverse để hiển thị cũ nhất trước
        messageDtos.Reverse();

        return new ConversationMessagesResponseDto
        {
            Messages = messageDtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            HasMore = page * pageSize < totalCount
        };
    }

    public async Task<SimpleMessageDto> SendMessageAsync(int conversationId, int senderId, SendSimpleMessageDto messageDto)
    {
        // Kiểm tra quyền truy cập
        var conversation = await _context.ChatConversations
            .Include(c => c.User1)
            .Include(c => c.User2)
            .FirstOrDefaultAsync(c => c.Id == conversationId && 
                                 ((c.User1Id == senderId && c.IsUser1Active) || 
                                  (c.User2Id == senderId && c.IsUser2Active)));

        if (conversation == null)
        {
            throw new UnauthorizedAccessException("Access denied to conversation");
        }

        // Tạo tin nhắn mới
        var message = new SimpleMessage
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = messageDto.Content.Trim(),
            ReplyToMessageId = messageDto.ReplyToMessageId,
            SentAt = DateTime.Now
        };

        _context.SimpleMessages.Add(message);

        // Cập nhật thông tin cuộc trò chuyện
        conversation.LastMessage = message.Content.Length > 100 ? 
                                  message.Content.Substring(0, 100) + "..." : 
                                  message.Content;
        conversation.LastMessageTime = message.SentAt;
        conversation.LastMessageSenderId = senderId;
        conversation.MessageCount++;
        conversation.UpdatedAt = DateTime.Now;

        // Đảm bảo cả 2 user đều có thể thấy cuộc trò chuyện
        conversation.IsUser1Active = true;
        conversation.IsUser2Active = true;        await _context.SaveChangesAsync();

        // Load sender info và reply message info
        await _context.Entry(message)
            .Reference(m => m.Sender)
            .LoadAsync();
            
        // Load reply message nếu có
        if (message.ReplyToMessageId.HasValue)
        {
            await _context.Entry(message)
                .Reference(m => m.ReplyToMessage)
                .LoadAsync();
        }

        // Debug logging
        _logger.LogInformation("New message from {SenderName} (ID: {SenderId}): Avatar = {Avatar}", 
            $"{message.Sender.FirstName} {message.Sender.LastName}".Trim(), 
            message.Sender.Id, 
            message.Sender.ProfilePictureUrl ?? "NULL");

        return new SimpleMessageDto
        {
            Id = message.Id,
            SenderId = message.SenderId,
            SenderName = $"{message.Sender.FirstName} {message.Sender.LastName}".Trim(),
            SenderAvatar = message.Sender.ProfilePictureUrl,
            Content = message.Content,
            SentAt = message.SentAt,
            IsMine = true,
            ReplyToMessageId = message.ReplyToMessageId,
            ReplyToContent = message.ReplyToMessage?.Content
        };
    }    public async Task<bool> MarkConversationAsReadAsync(int conversationId, int userId)
    {
        var conversation = await _context.ChatConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && 
                                 ((c.User1Id == userId && c.IsUser1Active) || 
                                  (c.User2Id == userId && c.IsUser2Active)));

        if (conversation == null) return false;

        var now = DateTime.Now;
        
        _logger.LogInformation($"Marking conversation {conversationId} as read for user {userId} at {now}");
        
        if (conversation.User1Id == userId)
        {
            conversation.User1LastRead = now;
        }
        else
        {
            conversation.User2LastRead = now;
        }

        conversation.UpdatedAt = now;
        await _context.SaveChangesAsync();
        
        _logger.LogInformation($"Successfully marked conversation {conversationId} as read for user {userId}");
        
        return true;
    }

    public async Task<bool> AreFriendsAsync(int userId1, int userId2)
    {
        // Kiểm tra follow 2 chiều
        var follow1 = await _context.UserFollowers
            .AnyAsync(f => f.FollowerId == userId1 && f.FollowingId == userId2);
            
        var follow2 = await _context.UserFollowers
            .AnyAsync(f => f.FollowerId == userId2 && f.FollowingId == userId1);

        return follow1 && follow2;
    }

    public async Task<bool> HideConversationAsync(int conversationId, int userId)
    {
        var conversation = await _context.ChatConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && 
                                 (c.User1Id == userId || c.User2Id == userId));

        if (conversation == null) return false;

        if (conversation.User1Id == userId)
        {
            conversation.IsUser1Active = false;
        }
        else
        {
            conversation.IsUser2Active = false;
        }

        conversation.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        
        return true;
    }    private async Task<int> GetUnreadCountAsync(int conversationId, int userId)
    {
        var conversation = await _context.ChatConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null) return 0;

        DateTime? lastRead = conversation.User1Id == userId ? 
                            conversation.User1LastRead : 
                            conversation.User2LastRead;

        _logger.LogInformation($"Getting unread count for conversation {conversationId}, user {userId}. LastRead: {lastRead}");

        if (lastRead == null)
        {
            // Chưa đọc tin nhắn nào, đếm tất cả tin nhắn từ người khác
            var unreadCount = await _context.SimpleMessages
                .CountAsync(m => m.ConversationId == conversationId && 
                               m.SenderId != userId && 
                               !m.IsDeleted);
            
            _logger.LogInformation($"No lastRead time, unread count: {unreadCount}");
            return unreadCount;
        }

        // Đếm tin nhắn từ người khác sau lần đọc cuối
        // Sử dụng >= thay vì > để đảm bảo không bỏ sót tin nhắn do precision issues
        var count = await _context.SimpleMessages
            .CountAsync(m => m.ConversationId == conversationId && 
                           m.SenderId != userId && 
                           m.SentAt > lastRead && 
                           !m.IsDeleted);
        
        _logger.LogInformation($"Unread count after lastRead {lastRead}: {count}");
        return count;
    }
}
