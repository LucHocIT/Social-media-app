using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SocialApp.Models;

public partial class UserBlock
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// User ID who is doing the blocking
    /// </summary>
    public int BlockerId { get; set; }

    /// <summary>
    /// User ID who is being blocked
    /// </summary>
    public int BlockedUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Optional reason for blocking
    /// </summary>
    [StringLength(500)]
    public string? Reason { get; set; }

    // Navigation properties
    [ForeignKey("BlockerId")]
    public virtual User Blocker { get; set; } = null!;

    [ForeignKey("BlockedUserId")]
    public virtual User BlockedUser { get; set; } = null!;
}
