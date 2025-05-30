using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SocialApp.Models
{
    public class ChatRoom
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public ChatRoomType Type { get; set; } = ChatRoomType.Private;

        public int CreatedByUserId { get; set; }
        [ForeignKey("CreatedByUserId")]
        public User CreatedByUser { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public ICollection<ChatRoomMember> Members { get; set; } = new List<ChatRoomMember>();
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }    public enum ChatRoomType
    {
        Private = 0,    // 1-on-1 chat
        Group = 1       // Group chat
    }
}
