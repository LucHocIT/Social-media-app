using System;
using System.ComponentModel.DataAnnotations;

namespace SocialApp.Models;

public partial class PostMedia
{
    public int Id { get; set; }
    
    public int PostId { get; set; }
    
    [Required]
    public string MediaUrl { get; set; } = null!;
    
    [Required]
    public string MediaType { get; set; } = null!; // "image", "video", "file"
    
    public string? MediaPublicId { get; set; }
    
    public string? MediaMimeType { get; set; }
    
    public string? MediaFilename { get; set; }
    
    public long? MediaFileSize { get; set; }
    
    public int? Width { get; set; }
    
    public int? Height { get; set; }
    
    public long? Duration { get; set; } // For videos (in seconds)
    
    public int OrderIndex { get; set; } = 0; // Order of media in the post
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public virtual Post Post { get; set; } = null!;
}
