using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SocialApp.Models
{
    public class ChatMessageReadStatus
    {
        [Key]
        public int Id { get; set; }

        public int MessageId { get; set; }
        [ForeignKey("MessageId")]
        public ChatMessage Message { get; set; } = null!;

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        public DateTime ReadAt { get; set; } = DateTime.UtcNow;
    }
}
