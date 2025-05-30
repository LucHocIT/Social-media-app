using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SocialApp.Models;
using SocialApp.DTOs;
using System.Security.Claims;

namespace SocialApp.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly SocialMediaDbContext _context;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(SocialMediaDbContext context, ILogger<ChatHub> logger)
        {
            _context = context;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            if (userId.HasValue)
            {
                // Update user's online status
                var user = await _context.Users.FindAsync(userId.Value);
                if (user != null)
                {
                    user.LastActive = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                // Join user to their chat rooms
                var userChatRooms = await _context.ChatRoomMembers
                    .Where(m => m.UserId == userId.Value && m.IsActive)
                    .Select(m => m.ChatRoomId.ToString())
                    .ToListAsync();

                foreach (var roomId in userChatRooms)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"ChatRoom_{roomId}");
                }

                _logger.LogInformation($"User {userId} connected to ChatHub");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserId();
            if (userId.HasValue)
            {
                // Update user's last active time
                var user = await _context.Users.FindAsync(userId.Value);
                if (user != null)
                {
                    user.LastActive = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation($"User {userId} disconnected from ChatHub");
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinChatRoom(int chatRoomId)
        {
            var userId = GetUserId();
            if (!userId.HasValue) return;

            // Verify user is a member of this chat room
            var isMember = await _context.ChatRoomMembers
                .AnyAsync(m => m.ChatRoomId == chatRoomId && m.UserId == userId.Value && m.IsActive);

            if (isMember)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"ChatRoom_{chatRoomId}");
                _logger.LogInformation($"User {userId} joined ChatRoom_{chatRoomId}");
            }
        }

        public async Task LeaveChatRoom(int chatRoomId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ChatRoom_{chatRoomId}");
            var userId = GetUserId();
            _logger.LogInformation($"User {userId} left ChatRoom_{chatRoomId}");
        }

        public async Task SendMessage(int chatRoomId, string content, string? replyToMessageId = null)
        {
            var userId = GetUserId();
            if (!userId.HasValue) return;

            try
            {
                // Verify user is a member of this chat room
                var member = await _context.ChatRoomMembers
                    .Include(m => m.ChatRoom)
                    .FirstOrDefaultAsync(m => m.ChatRoomId == chatRoomId && m.UserId == userId.Value && m.IsActive);

                if (member == null) return;

                // Create new message
                var message = new ChatMessage
                {
                    ChatRoomId = chatRoomId,
                    SenderId = userId.Value,
                    Content = content,
                    MessageType = ChatMessageType.Text,
                    SentAt = DateTime.UtcNow
                };

                if (!string.IsNullOrEmpty(replyToMessageId) && int.TryParse(replyToMessageId, out int replyId))
                {
                    message.ReplyToMessageId = replyId;
                }

                _context.ChatMessages.Add(message);

                // Update chat room last activity
                member.ChatRoom.LastActivity = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Load message with related data for response
                var messageDto = await GetMessageDto(message.Id);

                // Send message to all members in the chat room
                await Clients.Group($"ChatRoom_{chatRoomId}").SendAsync("ReceiveMessage", messageDto);

                _logger.LogInformation($"User {userId} sent message to ChatRoom_{chatRoomId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending message from user {userId} to ChatRoom_{chatRoomId}");
                await Clients.Caller.SendAsync("Error", "Failed to send message");
            }
        }

        public async Task MarkMessageAsRead(int messageId)
        {
            var userId = GetUserId();
            if (!userId.HasValue) return;

            try
            {
                // Check if read status already exists
                var existingReadStatus = await _context.ChatMessageReadStatuses
                    .FirstOrDefaultAsync(rs => rs.MessageId == messageId && rs.UserId == userId.Value);

                if (existingReadStatus == null)
                {
                    var readStatus = new ChatMessageReadStatus
                    {
                        MessageId = messageId,
                        UserId = userId.Value,
                        ReadAt = DateTime.UtcNow
                    };

                    _context.ChatMessageReadStatuses.Add(readStatus);
                    await _context.SaveChangesAsync();

                    // Get message details for notification
                    var message = await _context.ChatMessages
                        .Include(m => m.ChatRoom)
                        .FirstOrDefaultAsync(m => m.Id == messageId);

                    if (message != null)
                    {
                        // Notify other members that this user has read the message
                        await Clients.Group($"ChatRoom_{message.ChatRoomId}")
                            .SendAsync("MessageRead", new { MessageId = messageId, UserId = userId.Value, ReadAt = DateTime.UtcNow });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking message {messageId} as read for user {userId}");
            }
        }

        public async Task AddReaction(int messageId, string reactionType)
        {
            var userId = GetUserId();
            if (!userId.HasValue) return;

            try
            {
                // Check if reaction already exists
                var existingReaction = await _context.ChatMessageReactions
                    .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId.Value && r.ReactionType == reactionType);

                if (existingReaction != null)
                {
                    // Remove existing reaction (toggle)
                    _context.ChatMessageReactions.Remove(existingReaction);
                    await _context.SaveChangesAsync();

                    var message = await _context.ChatMessages.FindAsync(messageId);
                    await Clients.Group($"ChatRoom_{message?.ChatRoomId}")
                        .SendAsync("ReactionRemoved", new { MessageId = messageId, UserId = userId.Value, ReactionType = reactionType });
                }
                else
                {
                    // Add new reaction
                    var reaction = new ChatMessageReaction
                    {
                        MessageId = messageId,
                        UserId = userId.Value,
                        ReactionType = reactionType,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.ChatMessageReactions.Add(reaction);
                    await _context.SaveChangesAsync();

                    // Load reaction with user data
                    var reactionDto = await GetReactionDto(reaction.Id);
                    var message = await _context.ChatMessages.FindAsync(messageId);

                    await Clients.Group($"ChatRoom_{message?.ChatRoomId}")
                        .SendAsync("ReactionAdded", new { MessageId = messageId, Reaction = reactionDto });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding reaction to message {messageId} for user {userId}");
            }
        }

        public async Task StartTyping(int chatRoomId)
        {
            var userId = GetUserId();
            if (!userId.HasValue) return;

            var user = await _context.Users.FindAsync(userId.Value);
            if (user != null)
            {
                await Clients.OthersInGroup($"ChatRoom_{chatRoomId}")
                    .SendAsync("UserTyping", new { UserId = userId.Value, Username = user.Username });
            }
        }

        public async Task StopTyping(int chatRoomId)
        {
            var userId = GetUserId();
            if (!userId.HasValue) return;

            await Clients.OthersInGroup($"ChatRoom_{chatRoomId}")
                .SendAsync("UserStoppedTyping", new { UserId = userId.Value });
        }

        private int? GetUserId()
        {
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }

        private async Task<ChatMessageDto> GetMessageDto(int messageId)
        {
            var message = await _context.ChatMessages
                .Include(m => m.Sender)
                .Include(m => m.ReplyToMessage)
                    .ThenInclude(rm => rm!.Sender)
                .Include(m => m.Reactions)
                    .ThenInclude(r => r.User)
                .Include(m => m.ReadStatuses)
                    .ThenInclude(rs => rs.User)
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null) throw new ArgumentException("Message not found");

            return new ChatMessageDto
            {
                Id = message.Id,
                ChatRoomId = message.ChatRoomId,
                SenderId = message.SenderId,                Sender = new UserSummaryDto
                {
                    Id = message.Sender.Id,
                    Username = message.Sender.Username,
                    FirstName = message.Sender.FirstName ?? "",
                    LastName = message.Sender.LastName ?? "",
                    ProfilePictureUrl = message.Sender.ProfilePictureUrl
                },
                Content = message.Content,
                MessageType = message.MessageType,
                AttachmentUrl = message.AttachmentUrl,
                AttachmentType = message.AttachmentType,
                AttachmentName = message.AttachmentName,
                ReplyToMessageId = message.ReplyToMessageId,
                ReplyToMessage = message.ReplyToMessage != null ? new ChatMessageDto
                {
                    Id = message.ReplyToMessage.Id,
                    SenderId = message.ReplyToMessage.SenderId,
                    Sender = new UserSummaryDto
                    {
                        Id = message.ReplyToMessage.Sender.Id,
                        Username = message.ReplyToMessage.Sender.Username ?? "",
                        FirstName = message.ReplyToMessage.Sender.FirstName ?? "",
                        LastName = message.ReplyToMessage.Sender.LastName ?? ""
                    },
                    Content = message.ReplyToMessage.Content,
                    MessageType = message.ReplyToMessage.MessageType,
                    SentAt = message.ReplyToMessage.SentAt
                } : null,
                SentAt = message.SentAt,
                EditedAt = message.EditedAt,
                IsDeleted = message.IsDeleted,
                Reactions = message.Reactions.Select(r => new ChatMessageReactionDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    User = new UserSummaryDto
                    {
                        Id = r.User.Id,
                        Username = r.User.Username ?? "",
                        FirstName = r.User.FirstName ?? "",
                        LastName = r.User.LastName ?? ""
                    },
                    ReactionType = r.ReactionType,
                    CreatedAt = r.CreatedAt
                }).ToList(),
                ReadStatuses = message.ReadStatuses.Select(rs => new ChatMessageReadStatusDto
                {
                    Id = rs.Id,
                    UserId = rs.UserId,
                    User = new UserSummaryDto
                    {
                        Id = rs.User.Id,
                        Username = rs.User.Username ?? "",
                        FirstName = rs.User.FirstName ?? "",
                        LastName = rs.User.LastName ?? ""
                    },
                    ReadAt = rs.ReadAt
                }).ToList()
            };
        }

        private async Task<ChatMessageReactionDto> GetReactionDto(int reactionId)
        {
            var reaction = await _context.ChatMessageReactions
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == reactionId);

            if (reaction == null) throw new ArgumentException("Reaction not found");

            return new ChatMessageReactionDto
            {
                Id = reaction.Id,
                UserId = reaction.UserId,
                User = new UserSummaryDto
                {
                    Id = reaction.User.Id,
                    Username = reaction.User.Username ?? "",
                    FirstName = reaction.User.FirstName ?? "",
                    LastName = reaction.User.LastName ?? ""
                },
                ReactionType = reaction.ReactionType,
                CreatedAt = reaction.CreatedAt
            };
        }
    }
}
