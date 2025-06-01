using Microsoft.EntityFrameworkCore;
using SocialApp.DTOs;
using SocialApp.Models;
using SocialApp.Services.User;

namespace SocialApp.Services.Chat;

public class ConversationService : IConversationService
{
    private readonly SocialMediaDbContext _context;
    private readonly ILogger<ConversationService> _logger;
    private readonly IUserBlockService _userBlockService;

    public ConversationService(
        SocialMediaDbContext context, 
        ILogger<ConversationService> logger,
        IUserBlockService userBlockService)
    {
        _context = context;
        _logger = logger;
        _userBlockService = userBlockService;
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
            var unreadCount = await GetUnreadCountAsync(conv.Id, userId);

            conversationDtos.Add(new SimpleConversationDto
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
        // Check for block relationships first
        var areBlocking = await _userBlockService.AreUsersBlockingEachOtherAsync(currentUserId, otherUserId);
        if (areBlocking)
        {
            _logger.LogWarning("Cannot create conversation between users {CurrentUserId} and {OtherUserId} - users are blocking each other", 
                currentUserId, otherUserId);
            return null; // Cannot chat with blocked users
        }

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
                           existingConversation.User2 : existingConversation.User1;

            return new SimpleConversationDto
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
                               newConversation.User2 : newConversation.User1;

        return new SimpleConversationDto
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

    public async Task<bool> MarkConversationAsReadAsync(int conversationId, int userId)
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
    }

    public async Task<int> GetUnreadCountAsync(int conversationId, int userId)
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
