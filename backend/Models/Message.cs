using System;
using System.Collections.Generic;

namespace SocialApp.Models;

public partial class Message
{
    public int Id { get; set; }

    public string Content { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime SentAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public int SenderId { get; set; }

    public int ReceiverId { get; set; }

    public virtual User Receiver { get; set; } = null!;

    public virtual User Sender { get; set; } = null!;
}
