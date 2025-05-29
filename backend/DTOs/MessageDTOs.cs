using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SocialApp.DTOs;

// DTO để gửi tin nhắn
public class SendMessageDTO
{
    [Required]
    public int ReceiverId { get; set; }
    
    public string? Content { get; set; }
    
    // Media support
    public List<IFormFile>? MediaFiles { get; set; }
    public List<string>? MediaTypes { get; set; } // image, video, file
}

// DTO để hiển thị tin nhắn đơn lẻ
public class MessageItemDTO
{
    public string Id { get; set; } = string.Empty;
    public string? Content { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsRead { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderAvatar { get; set; }
    public string MessageType { get; set; } = "text"; // text, image, video, file, system
    public string? SystemAction { get; set; }
    
    // Attachments
    public List<MessageAttachmentDTO>? Attachments { get; set; }
}

// DTO cho attachment
public class MessageAttachmentDTO
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string MediaUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Duration { get; set; }
    public DateTime UploadedAt { get; set; }
}

// DTO cho cuộc trò chuyện
public class ConversationDTO
{
    public int Id { get; set; }
    public int OtherUserId { get; set; }
    public string OtherUserName { get; set; } = string.Empty;
    public string? OtherUserAvatar { get; set; }
    public bool IsOtherUserOnline { get; set; }
    public DateTime? OtherUserLastSeen { get; set; }
    
    public DateTime LastMessageAt { get; set; }
    public string? LastMessageContent { get; set; }
    public int? LastMessageSenderId { get; set; }
    public bool IsLastMessageFromMe { get; set; }
    
    public int UnreadCount { get; set; }
}

// DTO cho danh sách tin nhắn trong conversation
public class ConversationMessagesDTO
{
    public int ConversationId { get; set; }
    public int OtherUserId { get; set; }
    public string OtherUserName { get; set; } = string.Empty;
    public string? OtherUserAvatar { get; set; }
    public bool IsOtherUserOnline { get; set; }
    public DateTime? OtherUserLastSeen { get; set; }
    
    public List<MessageItemDTO> Messages { get; set; } = new List<MessageItemDTO>();
    public bool HasMoreMessages { get; set; }
    public DateTime? OldestMessageTime { get; set; }
}

// DTO cho typing indicator
public class TypingIndicatorDTO
{
    public int ConversationId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public bool IsTyping { get; set; }
}

// DTO cho mark as read
public class MarkAsReadDTO
{
    [Required]
    public int ConversationId { get; set; }
    
    public string? LastReadMessageId { get; set; } // ID của message cuối đã đọc
}

// Response DTOs
public class SendMessageResponseDTO
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public MessageItemDTO? MessageData { get; set; }
}

public class ConversationListResponseDTO
{
    public List<ConversationDTO> Conversations { get; set; } = new List<ConversationDTO>();
    public int TotalCount { get; set; }
    public bool HasMore { get; set; }
}
