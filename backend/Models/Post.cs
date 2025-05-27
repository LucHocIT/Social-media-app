using System;
using System.Collections.Generic;

namespace SocialApp.Models;

public partial class Post
{
    public int Id { get; set; }

    public string? Content { get; set; }

    public string? MediaUrl { get; set; }
    
    public string? MediaType { get; set; } // "image", "video", "file"
    
    public string? MediaPublicId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int UserId { get; set; }

    public string? Location { get; set; }    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    
    public virtual ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    
    public virtual ICollection<PostMedia> MediaFiles { get; set; } = new List<PostMedia>();

    public virtual User User { get; set; } = null!;
}
