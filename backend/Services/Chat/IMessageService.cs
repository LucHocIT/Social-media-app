using SocialApp.DTOs;

namespace SocialApp.Services.Chat;

public interface IMessageService
{
    // Lấy tin nhắn trong cuộc trò chuyện (có phân trang)
    Task<ConversationMessagesResponseDto> GetConversationMessagesAsync(int conversationId, int currentUserId, int page = 1, int pageSize = 50);
    
    // Gửi tin nhắn
    Task<SimpleMessageDto> SendMessageAsync(int conversationId, int senderId, SendSimpleMessageDto messageDto, bool sendSignalR = true);
    
    // Upload media cho chat message
    Task<UploadChatMediaResult> UploadChatMediaAsync(int userId, IFormFile mediaFile, string mediaType);
    
    // Xóa tin nhắn
    Task<bool> DeleteMessageAsync(int messageId, int userId);
}
