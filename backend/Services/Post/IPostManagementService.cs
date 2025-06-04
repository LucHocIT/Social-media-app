using SocialApp.DTOs;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SocialApp.Services.Post;

public interface IPostManagementService
{

    Task<PostResponseDTO?> CreatePostAsync(int userId, CreatePostDTO postDto);

    Task<PostResponseDTO?> UpdatePostAsync(int userId, int postId, UpdatePostDTO postDto);
    
    Task<bool> DeletePostAsync(int userId, int postId);
}