using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialApp.DTOs;
using SocialApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocialApp.Services.Comment
{
    public class CommentReportService : ICommentReportService
    {
        private readonly SocialMediaDbContext _context;
        private readonly ILogger<CommentReportService> _logger;

        public CommentReportService(SocialMediaDbContext context, ILogger<CommentReportService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CommentReportResponseDTO?> CreateCommentReportAsync(CreateCommentReportDTO reportDto, int reporterId)
        {
            try
            {
                var user = await _context.Users.FindAsync(reporterId);
                if (user == null || user.IsDeleted)
                {
                    _logger.LogWarning("Attempted to create comment report for non-existent or deleted user: {UserId}", reporterId);
                    return null;
                }

                var comment = await _context.Comments
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == reportDto.CommentId);
                    
                if (comment == null)
                {
                    _logger.LogWarning("Attempted to report non-existent comment: {CommentId}", reportDto.CommentId);
                    return null;
                }
                
                // Check if user has already reported this comment
                var existingReport = await _context.CommentReports
                    .FirstOrDefaultAsync(r => r.CommentId == reportDto.CommentId && r.ReporterId == reporterId);
                    
                if (existingReport != null)
                {
                    _logger.LogWarning("User {UserId} has already reported comment {CommentId}", reporterId, reportDto.CommentId);
                    return null;
                }
                
                var commentReport = new CommentReport
                {
                    CommentId = reportDto.CommentId,
                    ReporterId = reporterId,
                    Reason = reportDto.Reason,
                    CreatedAt = DateTime.UtcNow,
                    Status = "Pending"
                };
                
                _context.CommentReports.Add(commentReport);
                await _context.SaveChangesAsync();
                
                return new CommentReportResponseDTO
                {
                    Id = commentReport.Id,
                    CommentId = commentReport.CommentId,
                    CommentContent = comment.Content,
                    ReporterId = commentReport.ReporterId,
                    ReporterUsername = user.Username,
                    Reason = commentReport.Reason,
                    Status = commentReport.Status,
                    CreatedAt = commentReport.CreatedAt,
                    ResolvedAt = commentReport.ResolvedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating comment report for comment {CommentId} by user {UserId}", reportDto.CommentId, reporterId);
                return null;
            }
        }
        
        public async Task<CommentReportResponseDTO?> UpdateCommentReportStatusAsync(int reportId, UpdateCommentReportStatusDTO statusDto, int adminId)
        {
            try
            {
                var admin = await _context.Users.FindAsync(adminId);
                if (admin == null || admin.Role != "Admin" && admin.Role != "Moderator")
                {
                    _logger.LogWarning("Non-admin user {UserId} attempted to update comment report status", adminId);
                    return null;
                }
                
                var report = await _context.CommentReports
                    .Include(r => r.Reporter)
                    .Include(r => r.Comment)
                    .FirstOrDefaultAsync(r => r.Id == reportId);
                    
                if (report == null)
                {
                    _logger.LogWarning("Attempted to update non-existent comment report: {ReportId}", reportId);
                    return null;
                }
                
                report.Status = statusDto.Status;
                
                if (statusDto.Status != "Pending")
                {
                    report.ResolvedAt = DateTime.UtcNow;
                }
                
                await _context.SaveChangesAsync();
                
                return new CommentReportResponseDTO
                {
                    Id = report.Id,
                    CommentId = report.CommentId,
                    CommentContent = report.Comment.Content,
                    ReporterId = report.ReporterId,
                    ReporterUsername = report.Reporter.Username,
                    Reason = report.Reason,
                    Status = report.Status,
                    CreatedAt = report.CreatedAt,
                    ResolvedAt = report.ResolvedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating comment report status for report {ReportId}", reportId);
                return null;
            }
        }
        
        public async Task<List<CommentReportResponseDTO>> GetCommentReportsByStatusAsync(string status)
        {
            try
            {
                var reports = await _context.CommentReports
                    .Include(r => r.Reporter)
                    .Include(r => r.Comment)
                    .Where(r => r.Status == status)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();
                
                return reports.Select(report => new CommentReportResponseDTO
                {
                    Id = report.Id,
                    CommentId = report.CommentId,
                    CommentContent = report.Comment.Content,
                    ReporterId = report.ReporterId,
                    ReporterUsername = report.Reporter.Username,
                    Reason = report.Reason,
                    Status = report.Status,
                    CreatedAt = report.CreatedAt,
                    ResolvedAt = report.ResolvedAt
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving comment reports with status {Status}", status);
                return new List<CommentReportResponseDTO>();
            }
        }
        
        public async Task<CommentReportResponseDTO?> GetCommentReportByIdAsync(int reportId)
        {
            try
            {
                var report = await _context.CommentReports
                    .Include(r => r.Reporter)
                    .Include(r => r.Comment)
                    .FirstOrDefaultAsync(r => r.Id == reportId);
                    
                if (report == null)
                {
                    return null;
                }
                
                return new CommentReportResponseDTO
                {
                    Id = report.Id,
                    CommentId = report.CommentId,
                    CommentContent = report.Comment.Content,
                    ReporterId = report.ReporterId,
                    ReporterUsername = report.Reporter.Username,
                    Reason = report.Reason,
                    Status = report.Status,
                    CreatedAt = report.CreatedAt,
                    ResolvedAt = report.ResolvedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving comment report {ReportId}", reportId);
                return null;
            }
        }
    }
}
