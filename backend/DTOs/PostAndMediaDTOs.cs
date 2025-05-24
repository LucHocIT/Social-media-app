using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SocialApp.DTOs
{
    #region Post DTOs
    
    public class CreatePostDTO
    {
        [Required]
        [StringLength(500, MinimumLength = 1)]
        public string Content { get; set; } = null!;

        public string? MediaUrl { get; set; }
        
        public string? MediaType { get; set; } // "image", "video", "file"
        
        public string? MediaPublicId { get; set; }
    }

    public class UpdatePostDTO
    {
        [Required]
        [StringLength(500, MinimumLength = 1)]
        public string Content { get; set; } = null!;

        public string? MediaUrl { get; set; }
        
        public string? MediaType { get; set; } // "image", "video", "file"
        public string? MediaPublicId { get; set; }
    }
    
    public class PostResponseDTO
    {
        public int Id { get; set; }
        public string Content { get; set; } = null!;
        public string? MediaUrl { get; set; }
        public string? MediaType { get; set; } // "image", "video", "file"
        public string? MediaMimeType { get; set; } // MIME type for the media
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string? ProfilePictureUrl { get; set; }        public int LikesCount { get; set; }
        public int CommentsCount { get; set; }        
        public bool IsLikedByCurrentUser { get; set; }
        
        // Reaction information
        public Dictionary<string, int> ReactionCounts { get; set; } = new Dictionary<string, int>();
        public bool HasReactedByCurrentUser { get; set; }
        public string? CurrentUserReactionType { get; set; }
    }

    public class PostPagedResponseDTO
    {
        public List<PostResponseDTO> Posts { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }

    public class PostFilterDTO
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Username { get; set; }
        public bool? OnlyFollowing { get; set; }
    }

    #endregion

    #region Media DTOs
    
    public class MediaUploadDTO
    {
        public IFormFile? Media { get; set; }
        
        [StringLength(20)]
        public string MediaType { get; set; } = "image"; // Default is image; other values: "video", "file"
    }

    public class UploadMediaResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? MediaUrl { get; set; }
        public string? PublicId { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string? Format { get; set; }
        public long Duration { get; set; }  // For videos (in seconds)
        public long FileSize { get; set; }  // Size in bytes
        public string? ResourceType { get; set; } // "image", "video", or "raw"
        public string? MediaType { get; set; } // MIME type
    }

    #endregion
}
