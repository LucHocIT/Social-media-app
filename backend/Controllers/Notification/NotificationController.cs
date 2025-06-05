using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialApp.DTOs;
using SocialApp.Services.Notification;
using System.Security.Claims;

namespace SocialApp.Controllers.Notification
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Lấy danh sách thông báo của user hiện tại với phân trang
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<NotificationPagedResult>> GetNotifications([FromQuery] NotificationQueryDto query)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _notificationService.GetUserNotificationsAsync(userId, query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thông báo theo ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<NotificationDto>> GetNotification(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var notification = await _notificationService.GetNotificationByIdAsync(id, userId);
                
                if (notification == null)
                {
                    return NotFound(new { message = "Notification not found" });
                }

                return Ok(notification);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy số lượng thông báo chưa đọc
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<ActionResult<int>> GetUnreadCount()
        {
            try
            {
                var userId = GetCurrentUserId();
                var count = await _notificationService.GetUnreadCountAsync(userId);
                return Ok(count);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thống kê thông báo
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<NotificationStatsDto>> GetStats()
        {
            try
            {
                var userId = GetCurrentUserId();
                var stats = await _notificationService.GetNotificationStatsAsync(userId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Đánh dấu thông báo đã đọc
        /// </summary>
        [HttpPost("mark-read")]
        public async Task<ActionResult> MarkAsRead([FromBody] MarkNotificationReadDto markReadDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var success = await _notificationService.MarkAsReadAsync(userId, markReadDto.NotificationIds);
                
                if (!success)
                {
                    return BadRequest(new { message = "No notifications were updated" });
                }

                return Ok(new { message = "Notifications marked as read successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Đánh dấu tất cả thông báo đã đọc
        /// </summary>
        [HttpPost("mark-all-read")]
        public async Task<ActionResult> MarkAllAsRead()
        {
            try
            {
                var userId = GetCurrentUserId();
                var success = await _notificationService.MarkAllAsReadAsync(userId);
                
                if (!success)
                {
                    return BadRequest(new { message = "No notifications were updated" });
                }

                return Ok(new { message = "All notifications marked as read successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Xóa thông báo
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteNotification(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var success = await _notificationService.DeleteNotificationAsync(userId, id);
                
                if (!success)
                {
                    return NotFound(new { message = "Notification not found" });
                }

                return Ok(new { message = "Notification deleted successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Xóa tất cả thông báo đã đọc
        /// </summary>
        [HttpDelete("read-notifications")]
        public async Task<ActionResult> DeleteReadNotifications()
        {
            try
            {
                var userId = GetCurrentUserId();
                var success = await _notificationService.DeleteReadNotificationsAsync(userId);
                
                if (!success)
                {
                    return BadRequest(new { message = "No notifications were deleted" });
                }

                return Ok(new { message = "Read notifications deleted successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Tạo thông báo mới (chỉ dành cho admin hoặc system)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<NotificationDto>> CreateNotification([FromBody] CreateNotificationDto createDto)
        {
            try
            {
                var notification = await _notificationService.CreateNotificationAsync(createDto);
                return Ok(notification);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Tạo thông báo hàng loạt (chỉ dành cho admin hoặc system)
        /// </summary>
        [HttpPost("bulk")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> CreateBulkNotifications([FromBody] List<CreateNotificationDto> createDtos)
        {
            try
            {
                await _notificationService.CreateBulkNotificationsAsync(createDtos);
                return Ok(new { message = $"Created {createDtos.Count} notifications successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Tạo thông báo hệ thống cho tất cả user (chỉ dành cho admin)
        /// </summary>
        [HttpPost("system-broadcast")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> CreateSystemBroadcast([FromBody] string content)
        {
            try
            {
                // Lấy tất cả user ID
                var userIds = await GetAllUserIds();
                
                var notifications = userIds.Select(userId => new CreateNotificationDto
                {
                    Type = Models.NotificationType.System,
                    Content = content,
                    UserId = userId
                }).ToList();

                await _notificationService.CreateBulkNotificationsAsync(notifications);
                
                return Ok(new { message = $"System notification sent to {userIds.Count} users" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }
            return userId;
        }        private async Task<List<int>> GetAllUserIds()
        {
            return await _notificationService.GetAllUserIdsAsync();
        }
    }
}
