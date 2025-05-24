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
        public DateTime? UpdatedAt { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string? ProfilePictureUrl { get; set; }
        public bool IsVerified { get; set; }
        public int PostId { get; set; }
        public int? ParentCommentId { get; set; }
        public List<CommentResponseDTO> Replies { get; set; } = new List<CommentResponseDTO>();
        
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
        public string ReactionType { get; set; } = "like";
    }
}
