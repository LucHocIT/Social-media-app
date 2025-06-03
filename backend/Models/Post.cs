using System;
using System.Collections.Generic;

namespace SocialApp.Models;

public partial class Post
{
    public int Id { get; set; }

    public string? Content { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }    public int UserId { get; set; }    public string? Location { get; set; }

    // Privacy level: 0 = Public, 1 = Private, 2 = Secret
    public int PrivacyLevel { get; set; } = 0;

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    
    public virtual ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    
    public virtual ICollection<PostMedia> MediaFiles { get; set; } = new List<PostMedia>();

    public virtual User User { get; set; } = null!;
}
