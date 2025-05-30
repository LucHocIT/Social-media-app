using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SocialApp.Models
{
    public class ChatRoomMember
    {
        [Key]
        public int Id { get; set; }

        public int ChatRoomId { get; set; }
        [ForeignKey("ChatRoomId")]
        public ChatRoom ChatRoom { get; set; } = null!;

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastReadAt { get; set; }

        public ChatMemberRole Role { get; set; } = ChatMemberRole.Member;
        public bool IsActive { get; set; } = true;

        // For notifications
        public bool IsMuted { get; set; } = false;
    }

    public enum ChatMemberRole
    {
        Member = 1,
        Admin = 2,
        Owner = 3
    }
}
