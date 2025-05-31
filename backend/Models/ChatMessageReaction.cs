using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SocialApp.Models
{
    public class ChatMessageReaction
    {
        [Key]
        public int Id { get; set; }

        public int MessageId { get; set; }
        [ForeignKey("MessageId")]
        public ChatMessage Message { get; set; } = null!;

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string ReactionType { get; set; } = string.Empty; // "like", "love", "laugh", etc.

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
