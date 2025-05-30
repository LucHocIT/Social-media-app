using SocialApp.Models;

namespace SocialApp.DTOs
{
    // Request DTOs
    public class CreateChatRoomDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ChatRoomType Type { get; set; } = ChatRoomType.Private;
        public List<int> MemberUserIds { get; set; } = new List<int>();
    }

    public class SendMessageDto
    {
        public string Content { get; set; } = string.Empty;
        public ChatMessageType MessageType { get; set; } = ChatMessageType.Text;
        public string? AttachmentUrl { get; set; }
        public string? AttachmentType { get; set; }
        public string? AttachmentName { get; set; }
        public int? ReplyToMessageId { get; set; }
    }

    public class AddMemberDto
    {
        public int UserId { get; set; }
        public ChatMemberRole Role { get; set; } = ChatMemberRole.Member;
    }

    public class MessageReactionDto
    {
        public string ReactionType { get; set; } = string.Empty;
    }

    // Response DTOs
    public class ChatRoomDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ChatRoomType Type { get; set; }
        public int CreatedByUserId { get; set; }
        public UserSummaryDto CreatedByUser { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public bool IsActive { get; set; }
        public List<ChatRoomMemberDto> Members { get; set; } = new List<ChatRoomMemberDto>();
        public ChatMessageDto? LastMessage { get; set; }
        public int UnreadCount { get; set; }
    }

    public class ChatRoomMemberDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public UserSummaryDto User { get; set; } = null!;
        public DateTime JoinedAt { get; set; }
        public DateTime? LastReadAt { get; set; }
        public ChatMemberRole Role { get; set; }
        public bool IsActive { get; set; }
        public bool IsMuted { get; set; }
    }

    public class ChatMessageDto
    {
        public int Id { get; set; }
        public int ChatRoomId { get; set; }
        public int SenderId { get; set; }
        public UserSummaryDto Sender { get; set; } = null!;
        public string Content { get; set; } = string.Empty;
        public ChatMessageType MessageType { get; set; }
        public string? AttachmentUrl { get; set; }
        public string? AttachmentType { get; set; }
        public string? AttachmentName { get; set; }
        public int? ReplyToMessageId { get; set; }
        public ChatMessageDto? ReplyToMessage { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public bool IsDeleted { get; set; }
        public List<ChatMessageReactionDto> Reactions { get; set; } = new List<ChatMessageReactionDto>();
        public List<ChatMessageReadStatusDto> ReadStatuses { get; set; } = new List<ChatMessageReadStatusDto>();
        public bool IsRead { get; set; }
    }

    public class ChatMessageReactionDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public UserSummaryDto User { get; set; } = null!;
        public string ReactionType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ChatMessageReadStatusDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public UserSummaryDto User { get; set; } = null!;
        public DateTime ReadAt { get; set; }
    }

    public class UserSummaryDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? ProfilePictureUrl { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastActive { get; set; }
    }

    // Pagination DTOs
    public class ChatMessagesResponseDto
    {
        public List<ChatMessageDto> Messages { get; set; } = new List<ChatMessageDto>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasNext { get; set; }
        public bool HasPrevious { get; set; }
    }

    public class ChatRoomsResponseDto
    {
        public List<ChatRoomDto> ChatRooms { get; set; } = new List<ChatRoomDto>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasNext { get; set; }
        public bool HasPrevious { get; set; }
    }
}
