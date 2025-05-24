using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SocialApp.DTOs
{
    public class CreateReactionDTO
    {
        [Required]
        public int PostId { get; set; }
        
        [Required]
        [StringLength(20)]
        public string ReactionType { get; set; } = "like"; // Default is like; other values: "love", "haha", "wow", "sad", "angry"
    }
    
    public class ReactionResponseDTO
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string? ProfilePictureUrl { get; set; }
        public int PostId { get; set; }
        public string ReactionType { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
    
    public class ReactionSummaryDTO
    {
        public int TotalCount { get; set; }
        public Dictionary<string, int> ReactionCounts { get; set; } = new Dictionary<string, int>();
        public bool HasReactedByCurrentUser { get; set; }
        public string? CurrentUserReactionType { get; set; }
    }
}
