using Microsoft.AspNetCore.Http;

namespace SocialApp.DTOs
{
    public class MediaUploadDTO
    {
        public IFormFile? Media { get; set; }
    }
}
