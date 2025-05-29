using System;
using System.Collections.Generic;

namespace SocialApp.Models;

public class Conversation
{
    public int Id { get; set; }
    
    public int User1Id { get; set; }
    public int User2Id { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    
    // Lưu tin nhắn cuối cùng để hiển thị preview
    public string? LastMessageContent { get; set; }
    public int? LastMessageSenderId { get; set; }
    
    // Đếm tin nhắn chưa đọc cho mỗi user
    public int UnreadCountUser1 { get; set; } = 0;
    public int UnreadCountUser2 { get; set; } = 0;
    
    // Trạng thái online của users
    public bool IsUser1Online { get; set; } = false;
    public bool IsUser2Online { get; set; } = false;
    public DateTime? User1LastSeen { get; set; }
    public DateTime? User2LastSeen { get; set; }
    
    // Navigation properties
    public virtual User User1 { get; set; } = null!;
    public virtual User User2 { get; set; } = null!;
    public virtual User? LastMessageSender { get; set; }
    
    public virtual ICollection<MessageBatch> MessageBatches { get; set; } = new List<MessageBatch>();
    
    // Helper methods
    public int GetOtherUserId(int currentUserId)
    {
        return currentUserId == User1Id ? User2Id : User1Id;
    }
    
    public int GetUnreadCount(int userId)
    {
        return userId == User1Id ? UnreadCountUser1 : UnreadCountUser2;
    }
    
    public void SetUnreadCount(int userId, int count)
    {
        if (userId == User1Id)
            UnreadCountUser1 = count;
        else
            UnreadCountUser2 = count;
    }
    
    public bool IsUserOnline(int userId)
    {
        return userId == User1Id ? IsUser1Online : IsUser2Online;
    }
    
    public void SetUserOnlineStatus(int userId, bool isOnline)
    {
        if (userId == User1Id)
        {
            IsUser1Online = isOnline;
            if (!isOnline) User1LastSeen = DateTime.UtcNow;
        }
        else
        {
            IsUser2Online = isOnline;
            if (!isOnline) User2LastSeen = DateTime.UtcNow;
        }
    }
}
