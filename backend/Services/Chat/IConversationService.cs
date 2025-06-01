using SocialApp.DTOs;

namespace SocialApp.Services.Chat;

public interface IConversationService
{
    // Lấy danh sách cuộc trò chuyện của user
    Task<ConversationsListDto> GetUserConversationsAsync(int userId);
    
    // Tạo hoặc lấy cuộc trò chuyện giữa 2 user
    Task<SimpleConversationDto?> GetOrCreateConversationAsync(int currentUserId, int otherUserId);
    
    // Đánh dấu đã đọc tin nhắn
    Task<bool> MarkConversationAsReadAsync(int conversationId, int userId);
    
    // Kiểm tra quan hệ bạn bè (follow 2 chiều)
    Task<bool> AreFriendsAsync(int userId1, int userId2);
    
    // Xóa cuộc trò chuyện (chỉ ẩn khỏi danh sách)
    Task<bool> HideConversationAsync(int conversationId, int userId);
    
    // Lấy số tin nhắn chưa đọc cho user trong conversation
    Task<int> GetUnreadCountAsync(int conversationId, int userId);
}
