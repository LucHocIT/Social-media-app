using System;
using System.Collections.Generic;

namespace SocialApp.Models;

public partial class User
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Bio { get; set; }

    public string? ProfilePictureUrl { get; set; }

    public string Role { get; set; } = "User"; // Default role is User, can be Admin

    public bool IsDeleted { get; set; } = false; // Field for soft delete

    public DateTime? DeletedAt { get; set; } // When the user was soft deleted

    public DateTime CreatedAt { get; set; }

    public DateTime? LastActive { get; set; }    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    
    public virtual ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();

    public virtual ICollection<Message> MessageReceivers { get; set; } = new List<Message>();

    public virtual ICollection<Message> MessageSenders { get; set; } = new List<Message>();

    public virtual ICollection<Notification> NotificationFromUsers { get; set; } = new List<Notification>();

    public virtual ICollection<Notification> NotificationUsers { get; set; } = new List<Notification>();

    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();

    public virtual ICollection<UserFollower> UserFollowerFollowers { get; set; } = new List<UserFollower>();

    public virtual ICollection<UserFollower> UserFollowerFollowings { get; set; } = new List<UserFollower>();
}
