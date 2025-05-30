using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Services.Chat
{
    public interface IChatService
    {
        Task<ChatRoomDto> CreateChatRoomAsync(int currentUserId, CreateChatRoomDto createChatRoomDto);
        Task<ChatRoomDto?> GetChatRoomAsync(int chatRoomId, int currentUserId);
        Task<ChatRoomsResponseDto> GetUserChatRoomsAsync(int userId, int page = 1, int pageSize = 20);
        Task<ChatMessagesResponseDto> GetChatMessagesAsync(int chatRoomId, int currentUserId, int page = 1, int pageSize = 50);
        Task<ChatMessageDto> SendMessageAsync(int chatRoomId, int senderId, SendMessageDto sendMessageDto);
        Task<bool> AddMemberToChatRoomAsync(int chatRoomId, int currentUserId, AddMemberDto addMemberDto);
        Task<bool> RemoveMemberFromChatRoomAsync(int chatRoomId, int currentUserId, int memberUserId);
        Task<bool> LeaveChatRoomAsync(int chatRoomId, int userId);
        Task<bool> DeleteChatRoomAsync(int chatRoomId, int currentUserId);
        Task<ChatRoomDto?> GetOrCreatePrivateChatAsync(int currentUserId, int otherUserId);
        Task<bool> MarkMessagesAsReadAsync(int chatRoomId, int userId, List<int> messageIds);
        Task<List<UserSummaryDto>> SearchUsersForChatAsync(string searchTerm, int currentUserId);
        Task<bool> UpdateChatRoomAsync(int chatRoomId, int currentUserId, string name, string? description);
    }
}
