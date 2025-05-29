using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SocialApp.DTOs
{
    public class CreateCommentDTO
    {
        [Required]
        public int PostId { get; set; }
        
        [Required]
        [StringLength(300, MinimumLength = 1)]
        public string Content { get; set; } = null!;
        
        public int? ParentCommentId { get; set; }
    }
    
    public class UpdateCommentDTO
    {
        [Required]
        [StringLength(300, MinimumLength = 1)]
        public string Content { get; set; } = null!;
    }
    
    public class CommentResponseDTO
    {
        public int Id { get; set; }
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public bool IsVerified { get; set; }public int PostId { get; set; }
        public int? ParentCommentId { get; set; }
        public List<CommentResponseDTO> Replies { get; set; } = new List<CommentResponseDTO>();
        public int RepliesCount { get; set; }
        
        public int ReactionsCount { get; set; }
        public Dictionary<string, int> ReactionCounts { get; set; } = new Dictionary<string, int>();
        public bool HasReactedByCurrentUser { get; set; }
        public string? CurrentUserReactionType { get; set; }
    }
    
    public class CommentReactionDTO
    {
        [Required]
        public int CommentId { get; set; }
        
        [Required]
        [StringLength(20)]
        public string ReactionType { get; set; } = null!;
    }
    
    public class CreateCommentReportDTO
    {
        [Required]
        public int CommentId { get; set; }
        
        [Required]
        [StringLength(300, MinimumLength = 5)]
        public string Reason { get; set; } = null!;
    }
    
    public class CommentReportResponseDTO
    {
        public int Id { get; set; }
        public int CommentId { get; set; }
        public string CommentContent { get; set; } = null!;
        public int ReporterId { get; set; }
        public string ReporterUsername { get; set; } = null!;
        public string Reason { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
    
    public class UpdateCommentReportStatusDTO
    {
        [Required]
        [StringLength(20)]
        [RegularExpression("^(Pending|Resolved|Rejected)$", ErrorMessage = "Status must be either 'Pending', 'Resolved', or 'Rejected'")]
        public string Status { get; set; } = null!;
    }
}
