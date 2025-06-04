using SocialApp.DTOs;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SocialApp.Services.Post;

public interface IPostMediaService
{
    Task<MultipleUploadMediaResult> UploadMultipleMediaAsync(int userId, List<IFormFile> mediaFiles, List<string> mediaTypes);
    
    string GetMimeTypeForMediaType(string mediaType, string url);
    
    bool IsValidMediaType(string mediaType);
    
    string[] GetAllowedMimeTypes(string mediaType);
    
    long GetMaxFileSizeForMediaType(string mediaType);
}
