using Microsoft.EntityFrameworkCore;
using SocialApp.DTOs;
using SocialApp.Models;
using SocialApp.Services.Utils;

namespace SocialApp.Services.Message;

public class MessageService : IMessageService
{
    private readonly SocialMediaDbContext _context;
    private readonly IRedisMessageService _redisService;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        SocialMediaDbContext context,
        IRedisMessageService redisService,
        ICloudinaryService cloudinaryService,
        ILogger<MessageService> logger)
    {
        _context = context;
        _redisService = redisService;
        _cloudinaryService = cloudinaryService;
        _logger = logger;
    }

    #region Conversation Management

    public async Task<ConversationDTO?> GetOrCreateConversationAsync(int user1Id, int user2Id)
    {
        try
        {
            // Đảm bảo user1Id < user2Id để tạo unique constraint
            if (user1Id > user2Id)
            {
                (user1Id, user2Id) = (user2Id, user1Id);
            }

            var conversation = await _context.Conversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .Include(c => c.LastMessageSender)
                .FirstOrDefaultAsync(c => c.User1Id == user1Id && c.User2Id == user2Id);

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    User1Id = user1Id,
                    User2Id = user2Id,
                    CreatedAt = DateTime.UtcNow,
                    LastMessageAt = DateTime.UtcNow
                };

                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();

                // Reload với navigation properties
                conversation = await _context.Conversations
                    .Include(c => c.User1)
                    .Include(c => c.User2)
                    .FirstAsync(c => c.Id == conversation.Id);
            }

            return MapToConversationDTO(conversation, user1Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/getting conversation between users {User1Id} and {User2Id}", user1Id, user2Id);
            return null;
        }
    }

    public async Task<List<ConversationDTO>> GetUserConversationsAsync(int userId, int page = 1, int pageSize = 20)
    {
        try
        {
            // Try cache first
            var cachedConversations = await _redisService.GetCachedUserConversationsAsync(userId);
            if (cachedConversations != null && page == 1)
            {
                return cachedConversations.Take(pageSize).ToList();
            }

            var conversations = await _context.Conversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .Include(c => c.LastMessageSender)
                .Where(c => c.User1Id == userId || c.User2Id == userId)
                .OrderByDescending(c => c.LastMessageAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var conversationDTOs = conversations.Select(c => MapToConversationDTO(c, userId)).ToList();

            // Update unread counts from Redis
            foreach (var dto in conversationDTOs)
            {
                dto.UnreadCount = await _redisService.GetUnreadCountAsync(userId, dto.Id);
                dto.IsOtherUserOnline = await _redisService.IsUserOnlineAsync(dto.OtherUserId);
            }

            // Cache for first page only
            if (page == 1)
            {
                await _redisService.CacheUserConversationsAsync(userId, conversationDTOs);
            }

            return conversationDTOs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversations for user {UserId}", userId);
            return new List<ConversationDTO>();
        }
    }

    #endregion

    #region Message Operations

    public async Task<SendMessageResponseDTO> SendMessageAsync(int senderId, SendMessageDTO messageDto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Get or create conversation
            var conversationDto = await GetOrCreateConversationAsync(senderId, messageDto.ReceiverId);
            if (conversationDto == null)
            {
                return new SendMessageResponseDTO
                {
                    Success = false,
                    Message = "Could not create conversation"
                };
            }

            var conversation = await _context.Conversations.FindAsync(conversationDto.Id);
            if (conversation == null)
            {
                return new SendMessageResponseDTO
                {
                    Success = false,
                    Message = "Conversation not found"
                };
            }

            // Create message item
            var messageItem = new MessageItem
            {
                Id = Guid.NewGuid().ToString(),
                Content = messageDto.Content,
                SentAt = DateTime.UtcNow,
                SenderId = senderId,
                MessageType = "text",
                IsRead = false
            };

            // Handle media attachments
            List<MessageAttachment> attachments = new();
            if (messageDto.MediaFiles?.Any() == true)
            {
                messageItem.MessageType = "media";
                messageItem.AttachmentIds = new List<int>();
                  for (int i = 0; i < messageDto.MediaFiles.Count; i++)
                {
                    var file = messageDto.MediaFiles[i];
                    var mediaType = messageDto.MediaTypes?.ElementAtOrDefault(i) ?? "file";
                    
                    CloudinaryUploadResult? uploadResult = null;
                    
                    using var stream = file.OpenReadStream();
                    switch (mediaType.ToLower())
                    {
                        case "image":
                            uploadResult = await _cloudinaryService.UploadImageAsync(stream, file.FileName);
                            break;
                        case "video":
                            uploadResult = await _cloudinaryService.UploadVideoAsync(stream, file.FileName);
                            break;
                        default:
                            uploadResult = await _cloudinaryService.UploadFileAsync(stream, file.FileName);
                            break;
                    }
                    
                    if (uploadResult != null && !string.IsNullOrEmpty(uploadResult.Url))
                    {
                        var attachment = new MessageAttachment
                        {
                            MessageItemId = messageItem.Id,
                            FileName = uploadResult.PublicId ?? file.FileName,
                            OriginalFileName = file.FileName,
                            MediaType = mediaType,
                            MimeType = file.ContentType,
                            FileSize = file.Length,
                            MediaUrl = uploadResult.Url,
                            CloudinaryPublicId = uploadResult.PublicId,
                            Width = uploadResult.Width,
                            Height = uploadResult.Height,
                            Duration = (int?)uploadResult.Duration,
                            UploadedAt = DateTime.UtcNow
                        };
                        
                        attachments.Add(attachment);
                    }
                }
            }

            // Find or create message batch
            var latestBatch = await _context.MessageBatches
                .Where(mb => mb.ConversationId == conversation.Id)
                .OrderByDescending(mb => mb.BatchEndTime)
                .FirstOrDefaultAsync();

            MessageBatch batch;
            var now = DateTime.UtcNow;
            
            // Create new batch if none exists or if last message is more than 1 hour old
            if (latestBatch == null || (now - latestBatch.BatchEndTime).TotalHours > 1)
            {
                batch = new MessageBatch
                {
                    ConversationId = conversation.Id,
                    BatchStartTime = now,
                    BatchEndTime = now,
                    SenderId = senderId,
                    MessageCount = 0,
                    MessagesData = "[]"
                };
                _context.MessageBatches.Add(batch);
                await _context.SaveChangesAsync(); // Save to get ID
            }
            else
            {
                batch = latestBatch;
            }

            // Add attachments to batch
            foreach (var attachment in attachments)
            {
                attachment.MessageBatchId = batch.Id;
                _context.MessageAttachments.Add(attachment);
            }
            await _context.SaveChangesAsync();

            // Update attachment IDs in message
            if (attachments.Any())
            {
                messageItem.AttachmentIds = attachments.Select(a => a.Id).ToList();
            }

            // Add message to batch
            batch.AddMessage(messageItem);

            // Update conversation
            conversation.LastMessageAt = now;
            conversation.LastMessageContent = messageItem.Content ?? "[Media]";
            conversation.LastMessageSenderId = senderId;

            // Update unread count for receiver
            var receiverId = conversation.GetOtherUserId(senderId);
            conversation.SetUnreadCount(receiverId, conversation.GetUnreadCount(receiverId) + 1);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();            // Update Redis cache
            await _redisService.IncrementUnreadCountAsync(receiverId, conversation.Id);
            await _redisService.InvalidateMessageCacheAsync(conversation.Id);
            await _redisService.InvalidateUserConversationsCacheAsync(senderId);
            await _redisService.InvalidateUserConversationsCacheAsync(receiverId);

            // Create response DTO
            var messageItemDTO = await MapToMessageItemDTO(messageItem, attachments, senderId);
            
            // Cache the new message
            try 
            {
                var existingMessages = await _redisService.GetCachedMessagesAsync(conversation.Id) ?? new List<MessageItemDTO>();
                existingMessages.Add(messageItemDTO);
                // Keep only the most recent 50 messages in cache
                var recentMessages = existingMessages.OrderByDescending(m => m.SentAt).Take(50).ToList();
                await _redisService.CacheRecentMessagesAsync(conversation.Id, recentMessages);
            }
            catch (Exception cacheEx)
            {
                _logger.LogWarning(cacheEx, "Failed to cache new message for conversation {ConversationId}", conversation.Id);
            }
            
            return new SendMessageResponseDTO
            {
                Success = true,
                Message = "Message sent successfully",
                MessageData = messageItemDTO
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error sending message from user {SenderId} to user {ReceiverId}", senderId, messageDto.ReceiverId);
            return new SendMessageResponseDTO
            {
                Success = false,
                Message = "Failed to send message"
            };
        }
    }

    public async Task<ConversationMessagesDTO> GetConversationMessagesAsync(int userId, int conversationId, DateTime? before = null, int limit = 50)
    {
        try
        {
            var conversation = await _context.Conversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .FirstOrDefaultAsync(c => c.Id == conversationId && (c.User1Id == userId || c.User2Id == userId));

            if (conversation == null)
            {
                return new ConversationMessagesDTO
                {
                    ConversationId = conversationId,
                    Messages = new List<MessageItemDTO>()
                };
            }

            // Try cache first for recent messages
            List<MessageItemDTO> messages;
            if (before == null)
            {
                var cachedMessages = await _redisService.GetCachedMessagesAsync(conversationId);
                if (cachedMessages?.Any() == true)
                {
                    messages = cachedMessages.Take(limit).ToList();
                }
                else
                {
                    messages = await LoadMessagesFromDatabase(conversationId, before, limit);
                    if (messages.Any())
                    {
                        await _redisService.CacheRecentMessagesAsync(conversationId, messages);
                    }
                }
            }
            else
            {
                messages = await LoadMessagesFromDatabase(conversationId, before, limit);
            }

            var otherUser = conversation.GetOtherUserId(userId) == conversation.User1Id ? conversation.User1 : conversation.User2;
            var isOtherUserOnline = await _redisService.IsUserOnlineAsync(otherUser.Id);

            return new ConversationMessagesDTO
            {
                ConversationId = conversationId,
                OtherUserId = otherUser.Id,
                OtherUserName = otherUser.Username,
                OtherUserAvatar = otherUser.ProfilePictureUrl,
                IsOtherUserOnline = isOtherUserOnline,
                OtherUserLastSeen = otherUser.LastActive,
                Messages = messages,
                HasMoreMessages = messages.Count == limit,
                OldestMessageTime = messages.LastOrDefault()?.SentAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages for conversation {ConversationId}", conversationId);
            return new ConversationMessagesDTO
            {
                ConversationId = conversationId,
                Messages = new List<MessageItemDTO>()
            };
        }
    }

    public async Task<bool> MarkMessagesAsReadAsync(int userId, int conversationId, string? lastReadMessageId = null)
    {
        try
        {
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && (c.User1Id == userId || c.User2Id == userId));

            if (conversation == null) return false;

            // Update unread count in database
            conversation.SetUnreadCount(userId, 0);
            await _context.SaveChangesAsync();

            // Clear Redis unread count
            await _redisService.ClearUnreadCountAsync(userId, conversationId);
            await _redisService.InvalidateUserConversationsCacheAsync(userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking messages as read for user {UserId} in conversation {ConversationId}", userId, conversationId);
            return false;
        }
    }

    #endregion

    #region User Status

    public async Task UpdateUserOnlineStatusAsync(int userId, bool isOnline, string? connectionId = null)
    {
        try
        {
            if (isOnline && !string.IsNullOrEmpty(connectionId))
            {
                await _redisService.SetUserOnlineAsync(userId, connectionId);
            }
            else if (!isOnline && !string.IsNullOrEmpty(connectionId))
            {
                await _redisService.SetUserOfflineAsync(userId, connectionId);
            }

            // Update last active in database
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.LastActive = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating online status for user {UserId}", userId);
        }
    }

    public async Task<bool> IsUserOnlineAsync(int userId)
    {
        return await _redisService.IsUserOnlineAsync(userId);
    }

    #endregion

    #region Typing Indicators

    public async Task SetTypingStatusAsync(int userId, int conversationId, bool isTyping)
    {
        await _redisService.SetTypingAsync(conversationId, userId, isTyping);
    }

    public async Task<List<int>> GetTypingUsersAsync(int conversationId)
    {
        return await _redisService.GetTypingUsersAsync(conversationId);
    }

    #endregion

    #region Utilities

    public async Task<int> GetUnreadMessageCountAsync(int userId)
    {
        try
        {
            var conversations = await _context.Conversations
                .Where(c => c.User1Id == userId || c.User2Id == userId)
                .ToListAsync();

            int totalUnread = 0;
            foreach (var conversation in conversations)
            {
                var unreadCount = await _redisService.GetUnreadCountAsync(userId, conversation.Id);
                if (unreadCount == 0)
                {
                    // Fallback to database if Redis doesn't have the count
                    unreadCount = conversation.GetUnreadCount(userId);
                }
                totalUnread += unreadCount;
            }

            return totalUnread;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread message count for user {UserId}", userId);
            return 0;
        }
    }

    public async Task DeleteConversationAsync(int userId, int conversationId)
    {
        try
        {
            var conversation = await _context.Conversations
                .Include(c => c.MessageBatches)
                    .ThenInclude(mb => mb.Attachments)
                .FirstOrDefaultAsync(c => c.Id == conversationId && (c.User1Id == userId || c.User2Id == userId));

            if (conversation == null) return;

            // Delete all Cloudinary media
            foreach (var batch in conversation.MessageBatches)
            {
                foreach (var attachment in batch.Attachments)
                {                    if (!string.IsNullOrEmpty(attachment.CloudinaryPublicId))
                    {
                        await _cloudinaryService.DeleteMediaAsync(attachment.CloudinaryPublicId);
                    }
                }
            }

            // Delete from database (cascade will handle related records)
            _context.Conversations.Remove(conversation);
            await _context.SaveChangesAsync();

            // Clear Redis cache
            await _redisService.InvalidateMessageCacheAsync(conversationId);
            await _redisService.InvalidateUserConversationsCacheAsync(conversation.User1Id);
            await _redisService.InvalidateUserConversationsCacheAsync(conversation.User2Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting conversation {ConversationId} for user {UserId}", conversationId, userId);
        }
    }

    #endregion

    #region Helper Methods

    private ConversationDTO MapToConversationDTO(Conversation conversation, int currentUserId)
    {
        var otherUser = conversation.GetOtherUserId(currentUserId) == conversation.User1Id ? conversation.User1 : conversation.User2;
        
        return new ConversationDTO
        {
            Id = conversation.Id,
            OtherUserId = otherUser.Id,
            OtherUserName = otherUser.Username,
            OtherUserAvatar = otherUser.ProfilePictureUrl,
            LastMessageAt = conversation.LastMessageAt,
            LastMessageContent = conversation.LastMessageContent,
            LastMessageSenderId = conversation.LastMessageSenderId,
            IsLastMessageFromMe = conversation.LastMessageSenderId == currentUserId,
            UnreadCount = conversation.GetUnreadCount(currentUserId)
        };
    }    private async Task<List<MessageItemDTO>> LoadMessagesFromDatabase(int conversationId, DateTime? before, int limit)
    {
        var query = _context.MessageBatches
            .Where(mb => mb.ConversationId == conversationId);

        if (before.HasValue)
        {
            query = query.Where(mb => mb.BatchEndTime < before.Value);
        }

        var batches = await query
            .Include(mb => mb.Attachments)
            .Include(mb => mb.Sender)
            .OrderByDescending(mb => mb.BatchEndTime)
            .Take(10)
            .ToListAsync(); // Load up to 10 batches

        var messages = new List<MessageItemDTO>();
        foreach (var batch in batches.OrderBy(b => b.BatchStartTime))
        {
            var batchMessages = batch.GetMessages();
            foreach (var messageItem in batchMessages.OrderBy(m => m.SentAt))
            {
                if (before.HasValue && messageItem.SentAt >= before.Value) continue;
                
                var attachments = batch.Attachments
                    .Where(a => a.MessageItemId == messageItem.Id)
                    .ToList();
                
                var messageDto = await MapToMessageItemDTO(messageItem, attachments, batch.SenderId);
                messages.Add(messageDto);
                
                if (messages.Count >= limit) break;
            }
            if (messages.Count >= limit) break;
        }

        return messages.OrderByDescending(m => m.SentAt).Take(limit).ToList();
    }

    private async Task<MessageItemDTO> MapToMessageItemDTO(MessageItem messageItem, List<MessageAttachment> attachments, int senderId)
    {
        var sender = await _context.Users.FindAsync(senderId);
        
        return new MessageItemDTO
        {
            Id = messageItem.Id,
            Content = messageItem.Content,
            SentAt = messageItem.SentAt,
            ReadAt = messageItem.ReadAt,
            IsRead = messageItem.IsRead,
            SenderId = messageItem.SenderId,
            SenderName = sender?.Username ?? "Unknown",
            SenderAvatar = sender?.ProfilePictureUrl,
            MessageType = messageItem.MessageType,
            SystemAction = messageItem.SystemAction,
            Attachments = attachments.Select(a => new MessageAttachmentDTO
            {
                Id = a.Id,
                FileName = a.FileName,
                OriginalFileName = a.OriginalFileName,
                MediaType = a.MediaType,
                MimeType = a.MimeType,
                FileSize = a.FileSize,
                MediaUrl = a.MediaUrl,
                ThumbnailUrl = a.ThumbnailUrl,
                Width = a.Width,
                Height = a.Height,
                Duration = a.Duration,
                UploadedAt = a.UploadedAt
            }).ToList()
        };
    }

    #endregion
}
