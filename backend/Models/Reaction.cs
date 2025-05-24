using System;
using System.Collections.Generic;

namespace SocialApp.Models;

public partial class Reaction
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public int UserId { get; set; }

    public int? PostId { get; set; }
    
    // New fields for generic reactions
    public int EntityId { get; set; } // Can be PostId, CommentId, etc.
    
    public string EntityType { get; set; } = "Post"; // Can be "Post", "Comment", etc.
    
    public string ReactionType { get; set; } = "like"; // Default reaction is like, can be: like, love, haha, wow, sad, angry

    public virtual Post? Post { get; set; }

    public virtual User User { get; set; } = null!;
}
