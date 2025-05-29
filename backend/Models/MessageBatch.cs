using System;
using System.Collections.Generic;
using System.Text.Json;

namespace SocialApp.Models;

public class MessageBatch
{
    public int Id { get; set; }

    public int ConversationId { get; set; }

    // Nhóm tin nhắn theo thời gian (mỗi batch ~1 giờ hoặc khi có khoảng cách lớn)
    public DateTime BatchStartTime { get; set; }
    public DateTime BatchEndTime { get; set; }

    // Lưu danh sách tin nhắn dưới dạng JSON
    public string MessagesData { get; set; } = "[]";

    // Thống kê nhanh
    public int MessageCount { get; set; } = 0;
    public int SenderId { get; set; } // User gửi tin nhắn cuối trong batch này

    // Navigation properties
    public virtual Conversation Conversation { get; set; } = null!;
    public virtual User Sender { get; set; } = null!;
    public virtual ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();

    // Helper methods để work với Messages JSON
    public List<MessageItem> GetMessages()
    {
        try
        {
            return JsonSerializer.Deserialize<List<MessageItem>>(MessagesData) ?? new List<MessageItem>();
        }
        catch
        {
            return new List<MessageItem>();
        }
    }

    public void SetMessages(List<MessageItem> messages)
    {
        MessagesData = JsonSerializer.Serialize(messages);
        MessageCount = messages.Count;
        if (messages.Any())
        {
            BatchStartTime = messages.First().SentAt;
            BatchEndTime = messages.Last().SentAt;
            SenderId = messages.Last().SenderId;
        }
    }

    public void AddMessage(MessageItem message)
    {
        var messages = GetMessages();
        messages.Add(message);
        SetMessages(messages);
    }
}

// Class đại diện cho một tin nhắn trong batch
public class MessageItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString(); // Unique ID trong batch
    public string? Content { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsRead { get; set; }
    public int SenderId { get; set; }
    public string MessageType { get; set; } = "text"; // text, image, video, file, system

    // Reference đến attachments nếu có
    public List<int>? AttachmentIds { get; set; }

    // Cho tin nhắn hệ thống (user joined, left, etc.)
    public string? SystemAction { get; set; }
}
