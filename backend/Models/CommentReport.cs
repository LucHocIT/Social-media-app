using System;
using System.Collections.Generic;

namespace SocialApp.Models;

public partial class CommentReport
{
    public int Id { get; set; }

    public int CommentId { get; set; }

    public int ReporterId { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public virtual Comment Comment { get; set; } = null!;

    public virtual User Reporter { get; set; } = null!;
}
