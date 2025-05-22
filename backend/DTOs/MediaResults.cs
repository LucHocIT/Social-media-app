namespace SocialApp.DTOs;

public class UploadMediaResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? MediaUrl { get; set; }
    public string? PublicId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Format { get; set; }
}
