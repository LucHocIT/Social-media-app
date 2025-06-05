using System;

namespace SocialApp.Models
{
    public enum NotificationType
    {
        Like = 1,           // Ai đó đã thích bài viết của bạn
        Comment = 2,        // Ai đó đã bình luận bài viết của bạn
        Follow = 3,         // Ai đó đã theo dõi bạn
        CommentReply = 4,   // Ai đó đã trả lời bình luận của bạn
        CommentLike = 5,    // Ai đó đã thích bình luận của bạn
        Mention = 6,        // Ai đó đã nhắc đến bạn
        Welcome = 7,        // Chào mừng người dùng mới
        System = 8          // Thông báo hệ thống
    }
}
