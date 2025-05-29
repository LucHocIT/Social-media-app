using System;

namespace SocialApp.Models;

public class MessageAttachment
{
    public int Id { get; set; }

    public int MessageBatchId { get; set; }
    public string MessageItemId { get; set; } = string.Empty; // ID của message trong batch

    // Media info
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty; // image, video, file
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }

    // Cloudinary URLs
    public string MediaUrl { get; set; } = string.Empty; // Original
    public string? ThumbnailUrl { get; set; } // For images/videos
    public string? CloudinaryPublicId { get; set; }

    // Metadata for images/videos
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Duration { get; set; } // For videos in seconds

    public DateTime UploadedAt { get; set; }

    // Navigation properties
    public virtual MessageBatch MessageBatch { get; set; } = null!;
}
