using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SocialApp.Models;

[Table("SimpleMessages")]
public class SimpleMessage
{
    [Key]
    public int Id { get; set; }

    public int ConversationId { get; set; }
    
    public int SenderId { get; set; }
    
    [Required]
    [StringLength(1000)] // Giới hạn độ dài tin nhắn
    public string Content { get; set; } = string.Empty;
    
    public DateTime SentAt { get; set; } = DateTime.Now;
    
    // Chỉ lưu thông tin cơ bản, không lưu reactions, replies phức tạp
    public bool IsDeleted { get; set; } = false;
    
    // Optional: Cho phép reply đơn giản
    public int? ReplyToMessageId { get; set; }

    // Navigation properties
    [ForeignKey("ConversationId")]
    public virtual ChatConversation Conversation { get; set; } = null!;

    [ForeignKey("SenderId")]
    public virtual User Sender { get; set; } = null!;

    [ForeignKey("ReplyToMessageId")]
    public virtual SimpleMessage? ReplyToMessage { get; set; }
}

// Enum đơn giản cho loại tin nhắn
public enum SimpleMessageType
{
    Text = 1,
    Image = 2
}
