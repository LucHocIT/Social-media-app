using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Services.Notification
{
    public interface INotificationService
    {        // Tạo thông báo mới
        Task<NotificationDto?> CreateNotificationAsync(CreateNotificationDto createDto);

        // Tạo thông báo hàng loạt
        Task CreateBulkNotificationsAsync(List<CreateNotificationDto> createDtos);

        // Lấy danh sách thông báo của user với phân trang
        Task<NotificationPagedResult> GetUserNotificationsAsync(int userId, NotificationQueryDto query);

        // Lấy số lượng thông báo chưa đọc
        Task<int> GetUnreadCountAsync(int userId);

        // Lấy thống kê thông báo
        Task<NotificationStatsDto> GetNotificationStatsAsync(int userId);

        // Đánh dấu thông báo đã đọc
        Task<bool> MarkAsReadAsync(int userId, List<int> notificationIds);

        // Đánh dấu tất cả thông báo đã đọc
        Task<bool> MarkAllAsReadAsync(int userId);

        // Xóa thông báo
        Task<bool> DeleteNotificationAsync(int userId, int notificationId);

        // Xóa tất cả thông báo đã đọc
        Task<bool> DeleteReadNotificationsAsync(int userId);

        // Lấy thông báo theo ID
        Task<NotificationDto?> GetNotificationByIdAsync(int notificationId, int userId);        // Helper methods để tạo thông báo cho các hành động cụ thể
        Task CreateLikeNotificationAsync(int postId, int fromUserId);
        Task CreateCommentNotificationAsync(int postId, int commentId, int fromUserId);
        Task CreateFollowNotificationAsync(int userId, int fromUserId);
        Task CreateCommentReplyNotificationAsync(int commentId, int replyId, int fromUserId);
        Task CreateCommentLikeNotificationAsync(int commentId, int fromUserId);
        Task CreateWelcomeNotificationAsync(int userId);
        Task CreateSystemNotificationAsync(int userId, string content);
        
        // Admin helper methods
        Task<List<int>> GetAllUserIdsAsync();
    }
}
