using SocialApp.DTOs;
using System.Threading.Tasks;

namespace SocialApp.Services.Post;

public interface IPostQueryService
{
    Task<PostResponseDTO?> GetPostByIdAsync(int postId, int? currentUserId = null);
    Task<PostPagedResponseDTO> GetPostsAsync(PostFilterDTO filter, int? currentUserId = null);
    
    Task<PostPagedResponseDTO> GetPostsByUserAsync(int userId, int pageNumber = 1, int pageSize = 10, int? currentUserId = null);
}