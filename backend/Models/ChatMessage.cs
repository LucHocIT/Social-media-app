using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SocialApp.Models
{
    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }

        public int ChatRoomId { get; set; }
        [ForeignKey("ChatRoomId")]
        public ChatRoom ChatRoom { get; set; } = null!;

        public int SenderId { get; set; }
        [ForeignKey("SenderId")]
        public User Sender { get; set; } = null!;

        [Required]
        public string Content { get; set; } = string.Empty;

        public ChatMessageType MessageType { get; set; } = ChatMessageType.Text;

        // For file attachments
        public string? AttachmentUrl { get; set; }
        public string? AttachmentType { get; set; }
        public string? AttachmentName { get; set; }

        // For reply functionality
        public int? ReplyToMessageId { get; set; }
        [ForeignKey("ReplyToMessageId")]
        public ChatMessage? ReplyToMessage { get; set; }

        public DateTime SentAt { get; set; } = DateTime.Now;
        public DateTime? EditedAt { get; set; }
        public bool IsDeleted { get; set; } = false;

        // Navigation properties
        public ICollection<ChatMessage> Replies { get; set; } = new List<ChatMessage>();
        public ICollection<ChatMessageReaction> Reactions { get; set; } = new List<ChatMessageReaction>();
        public ICollection<ChatMessageReadStatus> ReadStatuses { get; set; } = new List<ChatMessageReadStatus>();
    }

    public enum ChatMessageType
    {
        Text = 1,
        Image = 2,
        File = 3,
        System = 4  // For system messages like "User joined", "User left", etc.
    }
}
