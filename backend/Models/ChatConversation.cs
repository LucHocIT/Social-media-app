using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SocialApp.Models;

[Table("ChatConversations")]
public class ChatConversation
{
    [Key]
    public int Id { get; set; }

    public int User1Id { get; set; }
    public int User2Id { get; set; }

    [StringLength(500)]
    public string? LastMessage { get; set; }

    public DateTime? LastMessageTime { get; set; }
    public int? LastMessageSenderId { get; set; }

    // Thay vì lưu tất cả tin nhắn, chỉ lưu conversation metadata
    public int MessageCount { get; set; } = 0;
    
    // Lưu thời gian last read của mỗi user để tính unread count
    public DateTime? User1LastRead { get; set; }
    public DateTime? User2LastRead { get; set; }

    public bool IsUser1Active { get; set; } = true;
    public bool IsUser2Active { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // Navigation properties
    [ForeignKey("User1Id")]
    public virtual User User1 { get; set; } = null!;

    [ForeignKey("User2Id")]
    public virtual User User2 { get; set; } = null!;

    [ForeignKey("LastMessageSenderId")]
    public virtual User? LastMessageSender { get; set; }

    // Chỉ tải tin nhắn khi cần thiết thông qua query riêng
    public virtual ICollection<SimpleMessage> Messages { get; set; } = new List<SimpleMessage>();
}
