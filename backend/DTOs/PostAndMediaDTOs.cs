using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SocialApp.DTOs
{
    #region Post DTOs
    
    public class PostMediaDTO
    {
        public string MediaUrl { get; set; } = null!;
        public string MediaType { get; set; } = null!; // "image", "video", "file"
        public string? MediaPublicId { get; set; }
        public string? MediaMimeType { get; set; }
        public string? MediaFilename { get; set; }
        public long? MediaFileSize { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public long? Duration { get; set; }
        public int OrderIndex { get; set; } = 0;
    }
      public class CreatePostDTO
    {
        [StringLength(500)]
        public string? Content { get; set; }

        // Legacy single media support (for backward compatibility)
        public string? MediaUrl { get; set; }
        public string? MediaType { get; set; } // "image", "video", "file"
        public string? MediaPublicId { get; set; }

        // New multiple media support
        public List<PostMediaDTO>? MediaFiles { get; set; } = new List<PostMediaDTO>();

        public string? Location { get; set; }
    }    public class UpdatePostDTO
    {
        [StringLength(500)]
        public string? Content { get; set; }

        // Legacy single media support (for backward compatibility)
        public string? MediaUrl { get; set; }
        public string? MediaType { get; set; } // "image", "video", "file"
        public string? MediaPublicId { get; set; }

        // New multiple media support
        public List<PostMediaDTO>? MediaFiles { get; set; } = new List<PostMediaDTO>();

        public string? Location { get; set; }
    }
      public class PostResponseDTO
    {
        public int Id { get; set; }
        public string? Content { get; set; }
        
        // Legacy single media support (for backward compatibility)
        public string? MediaUrl { get; set; }
        public string? MediaType { get; set; } // "image", "video", "file"
        public string? MediaMimeType { get; set; } // MIME type for the media
        
        // New multiple media support
        public List<PostMediaDTO>? MediaFiles { get; set; } = new List<PostMediaDTO>();
        
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int UserId { get; set; }

        public string? Location { get; set; }        public string Username { get; set; } = null!;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public int CommentsCount { get; set; }
        
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
        public bool? OnlyFollowing { get; set; }    }

    #endregion

    #region Media DTOs
    
    public class MultipleMediaUploadDTO
    {
        public List<IFormFile> MediaFiles { get; set; } = new List<IFormFile>();
        public List<string> MediaTypes { get; set; } = new List<string>(); // Corresponding media types for each file
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
        public string? MediaFilename { get; set; }
    }

    public class MultipleUploadMediaResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<UploadMediaResult> Results { get; set; } = new List<UploadMediaResult>();
    }

    #endregion
}
