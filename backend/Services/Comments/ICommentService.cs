using SocialApp.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialApp.Services.Comment
{    
    public interface ICommentService
    {
        Task<CommentResponseDTO?> CreateCommentAsync(CreateCommentDTO commentDto, int userId);
        Task<CommentResponseDTO?> UpdateCommentAsync(int commentId, UpdateCommentDTO commentDto, int userId);
        Task<bool> DeleteCommentAsync(int commentId, int userId);
        Task<List<CommentResponseDTO>> GetCommentsByPostIdAsync(int postId);
        Task<CommentResponseDTO?> GetCommentByIdAsync(int commentId, int? currentUserId = null);
        Task<List<CommentResponseDTO>> GetRepliesByCommentIdAsync(int commentId, int? currentUserId = null);
        Task<CommentResponseDTO?> AddOrToggleReactionAsync(CommentReactionDTO reactionDto, int userId);
    }
}
