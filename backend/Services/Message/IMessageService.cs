using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Services.Message;

public interface IMessageService
{
    // Conversation management
    Task<ConversationDTO?> GetOrCreateConversationAsync(int user1Id, int user2Id);
    Task<List<ConversationDTO>> GetUserConversationsAsync(int userId, int page = 1, int pageSize = 20);
    
    // Message operations
    Task<SendMessageResponseDTO> SendMessageAsync(int senderId, SendMessageDTO messageDto);
    Task<ConversationMessagesDTO> GetConversationMessagesAsync(int userId, int conversationId, DateTime? before = null, int limit = 50);
    Task<bool> MarkMessagesAsReadAsync(int userId, int conversationId, string? lastReadMessageId = null);
    
    // User status
    Task UpdateUserOnlineStatusAsync(int userId, bool isOnline, string? connectionId = null);
    Task<bool> IsUserOnlineAsync(int userId);
    
    // Typing indicators
    Task SetTypingStatusAsync(int userId, int conversationId, bool isTyping);
    Task<List<int>> GetTypingUsersAsync(int conversationId);
    
    // Utilities
    Task<int> GetUnreadMessageCountAsync(int userId);
    Task DeleteConversationAsync(int userId, int conversationId);
}
