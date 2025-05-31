using System;
using System.Collections.Generic;

namespace SocialApp.DTOs;

// Request DTOs
public class SendSimpleMessageDto
{
    public string Content { get; set; } = string.Empty;
    public int? ReplyToMessageId { get; set; }
}

public class GetConversationMessagesDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50; // Tối đa 50 tin nhắn mỗi lần
}

// Response DTOs
public class SimpleConversationDto
{
    public int Id { get; set; }
    public int OtherUserId { get; set; }
    public string OtherUserName { get; set; } = string.Empty;
    public string? OtherUserAvatar { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageTime { get; set; }
    public int UnreadCount { get; set; }
    public bool IsOtherUserOnline { get; set; }
    public DateTime? OtherUserLastActive { get; set; }
}

public class SimpleMessageDto
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderAvatar { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsMine { get; set; }
    public int? ReplyToMessageId { get; set; }
    public string? ReplyToContent { get; set; }
}

public class ConversationMessagesResponseDto
{
    public List<SimpleMessageDto> Messages { get; set; } = new List<SimpleMessageDto>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}

public class ConversationsListDto
{
    public List<SimpleConversationDto> Conversations { get; set; } = new List<SimpleConversationDto>();
    public int TotalCount { get; set; }
}
