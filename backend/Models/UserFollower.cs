using System;
using System.Collections.Generic;

namespace SocialApp.Models;

public partial class UserFollower
{
    public int Id { get; set; }

    public int FollowerId { get; set; }

    public int FollowingId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User Follower { get; set; } = null!;

    public virtual User Following { get; set; } = null!;
}
