using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SocialApp.DTOs;
using SocialApp.Models;
using SocialApp.Services.Utils;
using SocialApp.Services.User;
using SocialApp.Hubs;

namespace SocialApp.Services.Chat;

public class MessageService : IMessageService
{
    private readonly SocialMediaDbContext _context;
    private readonly ILogger<MessageService> _logger;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IHubContext<SimpleChatHub> _hubContext;
    private readonly IUserBlockService _userBlockService;
    private readonly IConversationService _conversationService;

    public MessageService(
        SocialMediaDbContext context, 
        ILogger<MessageService> logger, 
        ICloudinaryService cloudinaryService,
        IHubContext<SimpleChatHub> hubContext,
        IUserBlockService userBlockService,
        IConversationService conversationService)
    {
        _context = context;
        _logger = logger;
        _cloudinaryService = cloudinaryService;
        _hubContext = hubContext;
        _userBlockService = userBlockService;
        _conversationService = conversationService;
    }

    public async Task<ConversationMessagesResponseDto> GetConversationMessagesAsync(int conversationId, int currentUserId, int page = 1, int pageSize = 50)
    {
        // Kiểm tra quyền truy cập
        var conversation = await _context.ChatConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && 
                                 ((c.User1Id == currentUserId && c.IsUser1Active) || 
                                  (c.User2Id == currentUserId && c.IsUser2Active)));

        if (conversation == null)
        {
            throw new UnauthorizedAccessException("Access denied to conversation");
        }

        // Lấy tin nhắn với phân trang (mới nhất trước)
        var query = _context.SimpleMessages
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage)
            .Include(m => m.Reactions)
                .ThenInclude(r => r.User)
            .OrderByDescending(m => m.SentAt);

        var totalCount = await query.CountAsync();
        
        var messages = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var messageDtos = messages.Select(m => new SimpleMessageDto
        {
            Id = m.Id,
            SenderId = m.SenderId,
            SenderName = $"{m.Sender.FirstName} {m.Sender.LastName}".Trim(),
            SenderAvatar = m.Sender.ProfilePictureUrl,
            Content = m.Content,
            SentAt = m.SentAt,
            IsMine = m.SenderId == currentUserId,
            ReplyToMessageId = m.ReplyToMessageId,
            ReplyToContent = m.ReplyToMessage?.Content,
            // Media fields
            MediaUrl = m.MediaUrl,
            MediaType = m.MediaType,
            MediaPublicId = m.MediaPublicId,
            MediaMimeType = m.MediaMimeType,
            MediaFilename = m.MediaFilename,
            MediaFileSize = m.MediaFileSize,
            MessageType = !string.IsNullOrEmpty(m.MediaUrl) ? m.MediaType ?? "File" : "Text",
            // Reaction fields
            ReactionCounts = m.Reactions.GroupBy(r => r.ReactionType)
                .ToDictionary(g => g.Key, g => g.Count()),
            HasReactedByCurrentUser = m.Reactions.Any(r => r.UserId == currentUserId),
            CurrentUserReactionType = m.Reactions.FirstOrDefault(r => r.UserId == currentUserId)?.ReactionType,
            TotalReactions = m.Reactions.Count
        }).ToList();

        // Debug logging
        foreach (var dto in messageDtos)
        {
            _logger.LogInformation("Message from {SenderName} (ID: {SenderId}): Avatar = {Avatar}", 
                dto.SenderName, dto.SenderId, dto.SenderAvatar ?? "NULL");
        }

        // Reverse để hiển thị cũ nhất trước
        messageDtos.Reverse();

        return new ConversationMessagesResponseDto
        {
            Messages = messageDtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            HasMore = page * pageSize < totalCount
        };
    }

    public async Task<SimpleMessageDto> SendMessageAsync(int conversationId, int senderId, SendSimpleMessageDto messageRequest, bool sendSignalR = true)
    {
        // Kiểm tra quyền truy cập
        var conversation = await _context.ChatConversations
            .Include(c => c.User1)
            .Include(c => c.User2)
            .FirstOrDefaultAsync(c => c.Id == conversationId && 
                                 ((c.User1Id == senderId && c.IsUser1Active) || 
                                  (c.User2Id == senderId && c.IsUser2Active)));

        if (conversation == null)
        {
            throw new UnauthorizedAccessException("Access denied to conversation");
        }

        // Check for block relationships before sending message
        var otherUserId = conversation.User1Id == senderId ? conversation.User2Id : conversation.User1Id;
        var areBlocking = await _userBlockService.AreUsersBlockingEachOtherAsync(senderId, otherUserId);
        if (areBlocking)
        {
            _logger.LogWarning("Message sending blocked: users {SenderId} and {OtherUserId} are blocking each other", 
                senderId, otherUserId);
            throw new UnauthorizedAccessException("Cannot send message to blocked user");
        }

        // Tạo tin nhắn mới
        var message = new SimpleMessage
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = messageRequest.Content?.Trim(),
            ReplyToMessageId = messageRequest.ReplyToMessageId,
            SentAt = DateTime.Now,
            // Media fields
            MediaUrl = messageRequest.MediaUrl,
            MediaType = messageRequest.MediaType,
            MediaPublicId = messageRequest.MediaPublicId,
            MediaMimeType = messageRequest.MediaMimeType,
            MediaFilename = messageRequest.MediaFilename,
            MediaFileSize = messageRequest.MediaFileSize
        };

        _context.SimpleMessages.Add(message);

        // Cập nhật thông tin cuộc trò chuyện
        var displayMessage = !string.IsNullOrEmpty(message.Content) ? message.Content :
                           GetMediaDisplayMessage(message.MediaType, message.MediaFilename);
        
        conversation.LastMessage = displayMessage.Length > 100 ? 
                                  displayMessage.Substring(0, 100) + "..." :
                                  displayMessage;
        conversation.LastMessageTime = message.SentAt;
        conversation.LastMessageSenderId = senderId;
        conversation.MessageCount++;
        conversation.UpdatedAt = DateTime.Now;

        // Đảm bảo cả 2 user đều có thể thấy cuộc trò chuyện
        conversation.IsUser1Active = true;
        conversation.IsUser2Active = true;

        await _context.SaveChangesAsync();

        // Load sender info và reply message info
        await _context.Entry(message)
            .Reference(m => m.Sender)
            .LoadAsync();
            
        // Load reply message nếu có
        if (message.ReplyToMessageId.HasValue)
        {
            await _context.Entry(message)
                .Reference(m => m.ReplyToMessage)
                .LoadAsync();
        }

        // Debug logging
        _logger.LogInformation("New message from {SenderName} (ID: {SenderId}): Avatar = {Avatar}", 
            $"{message.Sender.FirstName} {message.Sender.LastName}".Trim(), 
            message.Sender.Id, 
            message.Sender.ProfilePictureUrl ?? "NULL");

        var messageDto = new SimpleMessageDto
        {
            Id = message.Id,
            SenderId = message.SenderId,
            SenderName = $"{message.Sender.FirstName} {message.Sender.LastName}".Trim(),
            SenderAvatar = message.Sender.ProfilePictureUrl,
            Content = message.Content,
            SentAt = message.SentAt,
            IsMine = true,
            ReplyToMessageId = message.ReplyToMessageId,
            ReplyToContent = message.ReplyToMessage?.Content,
            // Media fields
            MediaUrl = message.MediaUrl,
            MediaType = message.MediaType,
            MediaPublicId = message.MediaPublicId,
            MediaMimeType = message.MediaMimeType,
            MediaFilename = message.MediaFilename,
            MediaFileSize = message.MediaFileSize,
            MessageType = !string.IsNullOrEmpty(message.MediaUrl) ? message.MediaType ?? "File" : "Text",
            // Reaction fields (empty for new message)
            ReactionCounts = new Dictionary<string, int>(),
            HasReactedByCurrentUser = false,
            CurrentUserReactionType = null,
            TotalReactions = 0
        };

        // Send SignalR notifications only when explicitly requested (e.g., from REST API)
        if (sendSignalR)
        {
            try
            {
                await _hubContext.Clients.Group($"Conversation_{conversationId}")
                    .SendAsync("ReceiveMessage", messageDto);

                // Send conversation update to other participants
                var receiverId = conversation.User1Id == senderId ? conversation.User2Id : conversation.User1Id;
                await _hubContext.Clients.Group($"User_{receiverId}")
                    .SendAsync("ConversationUpdated", new
                    {
                        ConversationId = conversationId,
                        LastMessage = !string.IsNullOrEmpty(messageDto.Content) ? messageDto.Content : 
                                      !string.IsNullOrEmpty(messageDto.MediaFilename) ? $"📁 {messageDto.MediaFilename}" : "📁 File",
                        LastMessageTime = messageDto.SentAt,
                        SenderId = messageDto.SenderId,
                        SenderName = messageDto.SenderName,
                        UnreadCount = await _conversationService.GetUnreadCountAsync(conversationId, receiverId)
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SignalR notifications for message {MessageId}", message.Id);
            }
        }

        return messageDto;
    }

    public async Task<UploadChatMediaResult> UploadChatMediaAsync(int userId, IFormFile mediaFile, string mediaType)
    {
        try
        {
            if (mediaFile == null || mediaFile.Length == 0)
            {
                return new UploadChatMediaResult
                {
                    Success = false,
                    Message = "No file provided"
                };
            }

            using var stream = mediaFile.OpenReadStream();
            var fileName = $"chat_{mediaType}_{userId}_{Guid.NewGuid()}";
            CloudinaryUploadResult? uploadResult = null;

            switch (mediaType.ToLower())
            {
                case "image":
                    uploadResult = await _cloudinaryService.UploadImageAsync(stream, fileName);
                    break;
                case "video":
                    uploadResult = await _cloudinaryService.UploadVideoAsync(stream, fileName);
                    break;
                case "file":
                    uploadResult = await _cloudinaryService.UploadFileAsync(stream, fileName);
                    break;
                default:
                    return new UploadChatMediaResult
                    {
                        Success = false,
                        Message = "Invalid media type"
                    };
            }

            if (uploadResult == null)
            {
                return new UploadChatMediaResult
                {
                    Success = false,
                    Message = "Failed to upload to cloud storage"
                };
            }

            return new UploadChatMediaResult
            {
                Success = true,
                MediaUrl = uploadResult.Url,
                MediaType = mediaType,
                PublicId = uploadResult.PublicId,
                MimeType = mediaFile.ContentType,
                Filename = mediaFile.FileName,
                FileSize = mediaFile.Length,
                Message = "Media uploaded successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading chat media for user {UserId}", userId);
            return new UploadChatMediaResult
            {
                Success = false,
                Message = "An error occurred while uploading media"
            };
        }
    }    private string GetMediaDisplayMessage(string? mediaType, string? mediaFilename)
    {
        if (string.IsNullOrEmpty(mediaType))
        {
            return !string.IsNullOrEmpty(mediaFilename) ? mediaFilename : "Đã gửi tệp tin";
        }

        return mediaType.ToLower() switch
        {
            "image" => "Đã gửi hình ảnh",
            "video" => "Đã gửi video", 
            "file" => !string.IsNullOrEmpty(mediaFilename) ? mediaFilename : "Đã gửi tệp tin",
            _ => !string.IsNullOrEmpty(mediaFilename) ? mediaFilename : "Đã gửi tệp tin"
        };
    }

    public async Task<bool> DeleteMessageAsync(int messageId, int userId)
    {
        try
        {
            var message = await _context.SimpleMessages
                .Include(m => m.Conversation)
                .Include(m => m.Reactions)
                .FirstOrDefaultAsync(m => m.Id == messageId && !m.IsDeleted);

            if (message == null)
            {
                _logger.LogWarning("Message {MessageId} not found or already deleted", messageId);
                return false;
            }

            // Chỉ cho phép người gửi xóa tin nhắn của mình
            if (message.SenderId != userId)
            {
                _logger.LogWarning("User {UserId} tried to delete message {MessageId} without permission", userId, messageId);
                return false;
            }

            // Kiểm tra quyền truy cập cuộc trò chuyện
            var hasAccess = (message.Conversation.User1Id == userId && message.Conversation.IsUser1Active) ||
                           (message.Conversation.User2Id == userId && message.Conversation.IsUser2Active);

            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} doesn't have access to conversation for message {MessageId}", userId, messageId);
                return false;
            }

            // Xóa reactions liên quan
            if (message.Reactions.Any())
            {
                _context.MessageReactions.RemoveRange(message.Reactions);
            }

            // Đánh dấu tin nhắn đã bị xóa (soft delete)
            message.IsDeleted = true;
            message.Content = null; // Xóa nội dung

            // Cập nhật last message của conversation nếu đây là tin nhắn mới nhất
            if (message.Conversation.LastMessageSenderId == userId &&
                message.SentAt == message.Conversation.LastMessageTime)
            {
                // Tìm tin nhắn gần nhất chưa bị xóa
                var lastMessage = await _context.SimpleMessages
                    .Where(m => m.ConversationId == message.ConversationId && !m.IsDeleted)
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();

                if (lastMessage != null)
                {
                    var displayMessage = !string.IsNullOrEmpty(lastMessage.Content) ? lastMessage.Content :
                                       GetMediaDisplayMessage(lastMessage.MediaType, lastMessage.MediaFilename);
                    
                    message.Conversation.LastMessage = displayMessage.Length > 100 ? 
                                                      displayMessage.Substring(0, 100) + "..." :
                                                      displayMessage;
                    message.Conversation.LastMessageTime = lastMessage.SentAt;
                    message.Conversation.LastMessageSenderId = lastMessage.SenderId;
                }
                else
                {
                    // Không có tin nhắn nào còn lại
                    message.Conversation.LastMessage = "";
                    message.Conversation.LastMessageTime = message.Conversation.CreatedAt;
                    message.Conversation.LastMessageSenderId = null;
                }
            }

            message.Conversation.MessageCount = Math.Max(0, message.Conversation.MessageCount - 1);
            message.Conversation.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // Gửi thông báo qua SignalR
            try
            {
                await _hubContext.Clients.Group($"Conversation_{message.ConversationId}")
                    .SendAsync("MessageDeleted", new
                    {
                        MessageId = messageId,
                        ConversationId = message.ConversationId,
                        DeletedBy = userId,
                        Timestamp = DateTime.UtcNow
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SignalR notification for deleted message {MessageId}", messageId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message {MessageId} for user {UserId}", messageId, userId);
            throw;
        }
    }
}
