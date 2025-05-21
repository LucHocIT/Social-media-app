namespace SocialApp.DTOs;

public class UploadProfilePictureResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public string? PublicId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Format { get; set; }
}
