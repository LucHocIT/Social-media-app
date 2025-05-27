using SocialApp.DTOs;
using SocialApp.Models;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SocialApp.Services.Post;

public interface IPostService
{
    Task<PostResponseDTO?> CreatePostAsync(int userId, CreatePostDTO postDto);
    Task<PostResponseDTO?> UpdatePostAsync(int userId, int postId, UpdatePostDTO postDto);
    Task<bool> DeletePostAsync(int userId, int postId);    Task<PostResponseDTO?> GetPostByIdAsync(int postId, int? currentUserId = null);
    Task<PostPagedResponseDTO> GetPostsAsync(PostFilterDTO filter, int? currentUserId = null);
    Task<PostPagedResponseDTO> GetPostsByUserAsync(int userId, int pageNumber = 1, int pageSize = 10, int? currentUserId = null);    Task<MultipleUploadMediaResult> UploadMultipleMediaAsync(int userId, List<IFormFile> mediaFiles, List<string> mediaTypes);
}
