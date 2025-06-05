using System;
using System.ComponentModel.DataAnnotations;
using SocialApp.Models;

namespace SocialApp.DTOs
{
    // DTO cho việc tạo thông báo
    public class CreateNotificationDto
    {
        [Required]
        public NotificationType Type { get; set; }

        [Required]
        [StringLength(500)]
        public string Content { get; set; } = null!;

        [Required]
        public int UserId { get; set; }

        public int? FromUserId { get; set; }

        public int? PostId { get; set; }

        public int? CommentId { get; set; }
    }

    // DTO cho response thông báo
    public class NotificationDto
    {
        public int Id { get; set; }
        public NotificationType Type { get; set; }
        public string Content { get; set; } = null!;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public int UserId { get; set; }
        public int? FromUserId { get; set; }
        public int? PostId { get; set; }
        public int? CommentId { get; set; }

        // Thông tin user gửi thông báo
        public UserBasicDto? FromUser { get; set; }

        // Thông tin post (nếu có)
        public PostBasicDto? Post { get; set; }

        // Thông tin comment (nếu có)
        public CommentBasicDto? Comment { get; set; }
    }

    // DTO cơ bản cho user trong thông báo
    public class UserBasicDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? ProfilePicture { get; set; }
        public bool IsVerified { get; set; }
    }

    // DTO cơ bản cho post trong thông báo
    public class PostBasicDto
    {
        public int Id { get; set; }
        public string? Content { get; set; }
        public string? FirstMediaUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // DTO cơ bản cho comment trong thông báo
    public class CommentBasicDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }

    // DTO cho việc cập nhật trạng thái đã đọc
    public class MarkNotificationReadDto
    {
        [Required]
        public List<int> NotificationIds { get; set; } = new List<int>();
    }

    // DTO cho thống kê thông báo
    public class NotificationStatsDto
    {
        public int TotalNotifications { get; set; }
        public int UnreadCount { get; set; }
        public int TodayCount { get; set; }
        public int ThisWeekCount { get; set; }
    }

    // DTO cho phân trang thông báo
    public class NotificationPagedResult
    {
        public List<NotificationDto> Notifications { get; set; } = new List<NotificationDto>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }

    // DTO cho query parameters
    public class NotificationQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public bool? IsRead { get; set; }
        public NotificationType? Type { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
