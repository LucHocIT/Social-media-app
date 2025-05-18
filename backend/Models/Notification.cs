using System;
using System.Collections.Generic;

namespace SocialApp.Models;

public partial class Notification
{
    public int Id { get; set; }

    public int Type { get; set; }

    public string Content { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public int UserId { get; set; }

    public int? FromUserId { get; set; }

    public int? PostId { get; set; }

    public int? CommentId { get; set; }

    public virtual Comment? Comment { get; set; }

    public virtual User? FromUser { get; set; }

    public virtual Post? Post { get; set; }

    public virtual User User { get; set; } = null!;
}
