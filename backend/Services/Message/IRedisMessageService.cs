using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Services.Message;

public interface IRedisMessageService
{
    // User online status
    Task SetUserOnlineAsync(int userId, string connectionId);
    Task SetUserOfflineAsync(int userId, string connectionId);
    Task<bool> IsUserOnlineAsync(int userId);
    Task<List<string>> GetUserConnectionsAsync(int userId);
    
    // Typing indicators
    Task SetTypingAsync(int conversationId, int userId, bool isTyping);
    Task<List<int>> GetTypingUsersAsync(int conversationId);
    
    // Message caching (recent messages for fast loading)
    Task CacheRecentMessagesAsync(int conversationId, List<MessageItemDTO> messages);
    Task<List<MessageItemDTO>?> GetCachedMessagesAsync(int conversationId);
    Task InvalidateMessageCacheAsync(int conversationId);
    
    // Unread count caching
    Task SetUnreadCountAsync(int userId, int conversationId, int count);
    Task<int> GetUnreadCountAsync(int userId, int conversationId);
    Task IncrementUnreadCountAsync(int userId, int conversationId);
    Task ClearUnreadCountAsync(int userId, int conversationId);
    
    // Conversation list caching
    Task CacheUserConversationsAsync(int userId, List<ConversationDTO> conversations);
    Task<List<ConversationDTO>?> GetCachedUserConversationsAsync(int userId);
    Task InvalidateUserConversationsCacheAsync(int userId);
}
