using SocialApp.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialApp.Services.Comment
{    
    public interface ICommentReportService
    {
        Task<CommentReportResponseDTO?> CreateCommentReportAsync(CreateCommentReportDTO reportDto, int reporterId);
        Task<CommentReportResponseDTO?> UpdateCommentReportStatusAsync(int reportId, UpdateCommentReportStatusDTO statusDto, int adminId);
        Task<List<CommentReportResponseDTO>> GetCommentReportsByStatusAsync(string status);
        Task<CommentReportResponseDTO?> GetCommentReportByIdAsync(int reportId);
    }
}
