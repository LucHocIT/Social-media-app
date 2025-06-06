using Microsoft.EntityFrameworkCore;
using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Services.Notification
{
    public class NotificationService : INotificationService
    {
        private readonly SocialMediaDbContext _context;

        public NotificationService(SocialMediaDbContext context)
        {
            _context = context;
        }        public async Task<NotificationDto?> CreateNotificationAsync(CreateNotificationDto createDto)
        {
            // Kiểm tra user tồn tại
            var userExists = await _context.Users.AnyAsync(u => u.Id == createDto.UserId);
            if (!userExists)
            {
                throw new ArgumentException("User not found");
            }

            // Kiểm tra FromUser tồn tại (nếu có)
            if (createDto.FromUserId.HasValue)
            {
                var fromUserExists = await _context.Users.AnyAsync(u => u.Id == createDto.FromUserId.Value);
                if (!fromUserExists)
                {
                    throw new ArgumentException("FromUser not found");
                }

                // Không tạo thông báo cho chính mình
                if (createDto.FromUserId.Value == createDto.UserId)
                {
                    return null;
                }
            }            var notification = new Models.Notification
            {
                Type = (int)createDto.Type,
                Content = createDto.Content,
                UserId = createDto.UserId,
                FromUserId = createDto.FromUserId,
                PostId = createDto.PostId,
                CommentId = createDto.CommentId,
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return await GetNotificationByIdAsync(notification.Id, createDto.UserId);
        }

        public async Task CreateBulkNotificationsAsync(List<CreateNotificationDto> createDtos)
        {
            var notifications = new List<Models.Notification>();

            foreach (var dto in createDtos)
            {
                // Kiểm tra user tồn tại
                var userExists = await _context.Users.AnyAsync(u => u.Id == dto.UserId);
                if (!userExists) continue;

                // Không tạo thông báo cho chính mình
                if (dto.FromUserId.HasValue && dto.FromUserId.Value == dto.UserId)
                    continue;                notifications.Add(new Models.Notification
                {
                    Type = (int)dto.Type,
                    Content = dto.Content,
                    UserId = dto.UserId,
                    FromUserId = dto.FromUserId,
                    PostId = dto.PostId,
                    CommentId = dto.CommentId,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });
            }

            if (notifications.Any())
            {
                _context.Notifications.AddRange(notifications);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<NotificationPagedResult> GetUserNotificationsAsync(int userId, NotificationQueryDto query)
        {            var queryable = _context.Notifications
                .Where(n => n.UserId == userId)
                .Include(n => n.FromUser)
                .Include(n => n.Post!)
                    .ThenInclude(p => p.MediaFiles)
                .Include(n => n.Comment)
                .AsQueryable();

            // Áp dụng filters
            if (query.IsRead.HasValue)
            {
                queryable = queryable.Where(n => n.IsRead == query.IsRead.Value);
            }

            if (query.Type.HasValue)
            {
                queryable = queryable.Where(n => n.Type == (int)query.Type.Value);
            }

            if (query.FromDate.HasValue)
            {
                queryable = queryable.Where(n => n.CreatedAt >= query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                queryable = queryable.Where(n => n.CreatedAt <= query.ToDate.Value);
            }

            // Đếm tổng số
            var totalCount = await queryable.CountAsync();

            // Phân trang và sắp xếp
            var notifications = await queryable
                .OrderByDescending(n => n.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            var notificationDtos = notifications.Select(n => MapToDto(n)).ToList();

            var totalPages = (int)Math.Ceiling((double)totalCount / query.PageSize);

            return new NotificationPagedResult
            {
                Notifications = notificationDtos,
                TotalCount = totalCount,
                PageNumber = query.Page,
                PageSize = query.PageSize,
                TotalPages = totalPages,
                HasNextPage = query.Page < totalPages,
                HasPreviousPage = query.Page > 1
            };
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .CountAsync();
        }        public async Task<NotificationStatsDto> GetNotificationStatsAsync(int userId)
        {
            var today = DateTime.Now.Date;
            var weekAgo = today.AddDays(-7);

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .GroupBy(n => 1)
                .Select(g => new NotificationStatsDto
                {
                    TotalNotifications = g.Count(),
                    UnreadCount = g.Count(n => !n.IsRead),
                    TodayCount = g.Count(n => n.CreatedAt >= today),
                    ThisWeekCount = g.Count(n => n.CreatedAt >= weekAgo)
                })
                .FirstOrDefaultAsync();

            return notifications ?? new NotificationStatsDto();
        }

        public async Task<bool> MarkAsReadAsync(int userId, List<int> notificationIds)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && notificationIds.Contains(n.Id) && !n.IsRead)
                .ToListAsync();

            if (!notifications.Any())
                return false;

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkAllAsReadAsync(int userId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (!notifications.Any())
                return false;

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteNotificationAsync(int userId, int notificationId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
                return false;

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteReadNotificationsAsync(int userId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && n.IsRead)
                .ToListAsync();

            if (!notifications.Any())
                return false;

            _context.Notifications.RemoveRange(notifications);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<NotificationDto?> GetNotificationByIdAsync(int notificationId, int userId)
        {            var notification = await _context.Notifications
                .Include(n => n.FromUser)
                .Include(n => n.Post!)
                    .ThenInclude(p => p.MediaFiles)
                .Include(n => n.Comment)
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            return notification != null ? MapToDto(notification) : null;
        }

        // Helper methods
        public async Task CreateLikeNotificationAsync(int postId, int fromUserId)
        {
            var post = await _context.Posts
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == postId);

            if (post == null || post.UserId == fromUserId) return;

            var fromUser = await _context.Users.FindAsync(fromUserId);
            if (fromUser == null) return;

            var content = $"{GetUserFullName(fromUser)} đã thích bài viết của bạn";

            await CreateNotificationAsync(new CreateNotificationDto
            {
                Type = NotificationType.Like,
                Content = content,
                UserId = post.UserId,
                FromUserId = fromUserId,
                PostId = postId
            });
        }

        public async Task CreateCommentNotificationAsync(int postId, int commentId, int fromUserId)
        {
            var post = await _context.Posts
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == postId);

            if (post == null || post.UserId == fromUserId) return;

            var fromUser = await _context.Users.FindAsync(fromUserId);
            if (fromUser == null) return;

            var content = $"{GetUserFullName(fromUser)} đã bình luận bài viết của bạn";

            await CreateNotificationAsync(new CreateNotificationDto
            {
                Type = NotificationType.Comment,
                Content = content,
                UserId = post.UserId,
                FromUserId = fromUserId,
                PostId = postId,
                CommentId = commentId
            });
        }

        public async Task CreateFollowNotificationAsync(int userId, int fromUserId)
        {
            var fromUser = await _context.Users.FindAsync(fromUserId);
            if (fromUser == null) return;

            var content = $"{GetUserFullName(fromUser)} đã bắt đầu theo dõi bạn";

            await CreateNotificationAsync(new CreateNotificationDto
            {
                Type = NotificationType.Follow,
                Content = content,
                UserId = userId,
                FromUserId = fromUserId
            });
        }

        public async Task CreateCommentReplyNotificationAsync(int commentId, int replyId, int fromUserId)
        {
            var comment = await _context.Comments
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == commentId);

            if (comment == null || comment.UserId == fromUserId) return;

            var fromUser = await _context.Users.FindAsync(fromUserId);
            if (fromUser == null) return;

            var content = $"{GetUserFullName(fromUser)} đã trả lời bình luận của bạn";

            await CreateNotificationAsync(new CreateNotificationDto
            {
                Type = NotificationType.CommentReply,
                Content = content,
                UserId = comment.UserId,
                FromUserId = fromUserId,
                CommentId = replyId
            });
        }

        public async Task CreateCommentLikeNotificationAsync(int commentId, int fromUserId)
        {
            var comment = await _context.Comments
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == commentId);

            if (comment == null || comment.UserId == fromUserId) return;

            var fromUser = await _context.Users.FindAsync(fromUserId);
            if (fromUser == null) return;

            var content = $"{GetUserFullName(fromUser)} đã thích bình luận của bạn";

            await CreateNotificationAsync(new CreateNotificationDto
            {
                Type = NotificationType.CommentLike,
                Content = content,
                UserId = comment.UserId,
                FromUserId = fromUserId,
                CommentId = commentId
            });
        }

        public async Task CreateWelcomeNotificationAsync(int userId)
        {
            var content = "Chào mừng bạn đến với SocialApp! Hãy bắt đầu chia sẻ những khoảnh khắc đáng nhớ của bạn.";

            await CreateNotificationAsync(new CreateNotificationDto
            {
                Type = NotificationType.Welcome,
                Content = content,
                UserId = userId
            });
        }        public async Task CreateSystemNotificationAsync(int userId, string content)
        {
            await CreateNotificationAsync(new CreateNotificationDto
            {
                Type = NotificationType.System,
                Content = content,
                UserId = userId
            });
        }

        public async Task<List<int>> GetAllUserIdsAsync()
        {
            return await _context.Users
                .Where(u => !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync();
        }private string GetUserFullName(Models.User user)
        {
            if (!string.IsNullOrEmpty(user.FirstName) && !string.IsNullOrEmpty(user.LastName))
            {
                return $"{user.FirstName} {user.LastName}";
            }
            return user.FirstName ?? user.LastName ?? user.Username;
        }

        private NotificationDto MapToDto(Models.Notification notification)
        {
            return new NotificationDto
            {
                Id = notification.Id,
                Type = (NotificationType)notification.Type,
                Content = notification.Content,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt,
                UserId = notification.UserId,
                FromUserId = notification.FromUserId,
                PostId = notification.PostId,
                CommentId = notification.CommentId,                FromUser = notification.FromUser != null ? new UserBasicDto
                {
                    Id = notification.FromUser.Id,
                    Username = notification.FromUser.Username,
                    FullName = GetUserFullName(notification.FromUser),
                    ProfilePicture = notification.FromUser.ProfilePictureUrl,
                    IsVerified = false // User model doesn't have IsVerified field
                } : null,                Post = notification.Post != null ? new PostBasicDto
                {
                    Id = notification.Post.Id,
                    Content = notification.Post.Content,
                    FirstMediaUrl = notification.Post.MediaFiles?.FirstOrDefault()?.MediaUrl,
                    CreatedAt = notification.Post.CreatedAt
                } : null,
                Comment = notification.Comment != null ? new CommentBasicDto
                {
                    Id = notification.Comment.Id,
                    Content = notification.Comment.Content,
                    CreatedAt = notification.Comment.CreatedAt
                } : null
            };
        }
    }
}
