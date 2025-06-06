using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SocialApp.Models;

[Table("MessageReactions")]
public class MessageReaction
{
    [Key]
    public int Id { get; set; }

    public int MessageId { get; set; }
    
    public int UserId { get; set; }
      [StringLength(50)]
    public string ReactionType { get; set; } = "like"; // like, love, haha, wow, sad, angry, thumbs_up, thumbs_down
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation properties
    [ForeignKey("MessageId")]
    public virtual SimpleMessage Message { get; set; } = null!;

    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}
