using SocialApp.DTOs;

namespace SocialApp.Services.Chat;

public interface ISimpleChatService
{
    // Lấy danh sách cuộc trò chuyện của user
    Task<ConversationsListDto> GetUserConversationsAsync(int userId);
    
    // Tạo hoặc lấy cuộc trò chuyện giữa 2 user
    Task<SimpleConversationDto?> GetOrCreateConversationAsync(int currentUserId, int otherUserId);
    
    // Lấy tin nhắn trong cuộc trò chuyện (có phân trang)
    Task<ConversationMessagesResponseDto> GetConversationMessagesAsync(int conversationId, int currentUserId, int page = 1, int pageSize = 50);
    
    // Gửi tin nhắn
    Task<SimpleMessageDto> SendMessageAsync(int conversationId, int senderId, SendSimpleMessageDto messageDto);
    
    // Đánh dấu đã đọc tin nhắn
    Task<bool> MarkConversationAsReadAsync(int conversationId, int userId);
    
    // Kiểm tra quan hệ bạn bè (follow 2 chiều)
    Task<bool> AreFriendsAsync(int userId1, int userId2);
    
    // Xóa cuộc trò chuyện (chỉ ẩn khỏi danh sách)
    Task<bool> HideConversationAsync(int conversationId, int userId);
}
