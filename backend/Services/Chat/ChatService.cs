using Microsoft.EntityFrameworkCore;
using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Services.Chat
{
    public class ChatService : IChatService
    {
        private readonly SocialMediaDbContext _context;
        private readonly ILogger<ChatService> _logger;

        public ChatService(SocialMediaDbContext context, ILogger<ChatService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ChatRoomDto> CreateChatRoomAsync(int currentUserId, CreateChatRoomDto createChatRoomDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // For private chats, check if one already exists
                if (createChatRoomDto.Type == ChatRoomType.Private && createChatRoomDto.MemberUserIds.Count == 1)
                {
                    var otherUserId = createChatRoomDto.MemberUserIds[0];
                    var existingPrivateChat = await GetExistingPrivateChatAsync(currentUserId, otherUserId);
                    if (existingPrivateChat != null)
                    {
                        return existingPrivateChat;
                    }
                }

                // Create chat room
                var chatRoom = new ChatRoom
                {
                    Name = createChatRoomDto.Name,
                    Description = createChatRoomDto.Description,
                    Type = createChatRoomDto.Type,
                    CreatedByUserId = currentUserId,
                    CreatedAt = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow
                };

                _context.ChatRooms.Add(chatRoom);
                await _context.SaveChangesAsync();

                // Add creator as owner
                var creatorMember = new ChatRoomMember
                {
                    ChatRoomId = chatRoom.Id,
                    UserId = currentUserId,
                    Role = ChatMemberRole.Owner,
                    JoinedAt = DateTime.UtcNow
                };
                _context.ChatRoomMembers.Add(creatorMember);

                // Add other members
                foreach (var userId in createChatRoomDto.MemberUserIds)
                {
                    if (userId != currentUserId) // Don't add creator twice
                    {
                        var member = new ChatRoomMember
                        {
                            ChatRoomId = chatRoom.Id,
                            UserId = userId,
                            Role = ChatMemberRole.Member,
                            JoinedAt = DateTime.UtcNow
                        };
                        _context.ChatRoomMembers.Add(member);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return await GetChatRoomAsync(chatRoom.Id, currentUserId) ?? throw new InvalidOperationException("Failed to retrieve created chat room");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating chat room");
                throw;
            }
        }

        public async Task<ChatRoomDto?> GetChatRoomAsync(int chatRoomId, int currentUserId)
        {
            var chatRoom = await _context.ChatRooms
                .Include(cr => cr.CreatedByUser)
                .Include(cr => cr.Members)
                    .ThenInclude(m => m.User)
                .Include(cr => cr.Messages.OrderByDescending(m => m.SentAt).Take(1))
                    .ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(cr => cr.Id == chatRoomId && cr.IsActive);

            if (chatRoom == null) return null;

            // Check if current user is a member
            var userMember = chatRoom.Members.FirstOrDefault(m => m.UserId == currentUserId && m.IsActive);
            if (userMember == null) return null;

            // Get unread count for current user
            var unreadCount = await GetUnreadMessageCountAsync(chatRoomId, currentUserId);

            // Get display name for private chats
            var displayName = chatRoom.Name;
            if (chatRoom.Type == ChatRoomType.Private)
            {
                var otherMember = chatRoom.Members.FirstOrDefault(m => m.UserId != currentUserId && m.IsActive);
                if (otherMember != null)
                {
                    displayName = $"{otherMember.User.FirstName ?? ""} {otherMember.User.LastName ?? ""}";
                }
            }

            return new ChatRoomDto
            {
                Id = chatRoom.Id,
                Name = displayName,
                Description = chatRoom.Description,
                Type = chatRoom.Type,
                CreatedByUserId = chatRoom.CreatedByUserId,
                CreatedByUser = new UserSummaryDto
                {
                    Id = chatRoom.CreatedByUser.Id,
                    Username = chatRoom.CreatedByUser.Username,
                    FirstName = chatRoom.CreatedByUser.FirstName ?? "",
                    LastName = chatRoom.CreatedByUser.LastName ?? "",
                    ProfilePictureUrl = chatRoom.CreatedByUser.ProfilePictureUrl
                },
                CreatedAt = chatRoom.CreatedAt,
                LastActivity = chatRoom.LastActivity,
                IsActive = chatRoom.IsActive,
                Members = chatRoom.Members.Where(m => m.IsActive).Select(m => new ChatRoomMemberDto
                {
                    Id = m.Id,
                    UserId = m.UserId,
                    User = new UserSummaryDto
                    {
                        Id = m.User.Id,
                        Username = m.User.Username,
                        FirstName = m.User.FirstName ?? "",
                        LastName = m.User.LastName ?? "",
                        ProfilePictureUrl = m.User.ProfilePictureUrl,
                        LastActive = m.User.LastActive
                    },
                    JoinedAt = m.JoinedAt,
                    LastReadAt = m.LastReadAt,
                    Role = m.Role,
                    IsActive = m.IsActive,
                    IsMuted = m.IsMuted
                }).ToList(),
                LastMessage = chatRoom.Messages.FirstOrDefault() != null ? new ChatMessageDto
                {
                    Id = chatRoom.Messages.First().Id,
                    SenderId = chatRoom.Messages.First().SenderId,
                    Sender = new UserSummaryDto
                    {
                        Id = chatRoom.Messages.First().Sender.Id,
                        Username = chatRoom.Messages.First().Sender.Username,
                        FirstName = chatRoom.Messages.First().Sender.FirstName ?? "",
                        LastName = chatRoom.Messages.First().Sender.LastName ?? ""
                    },
                    Content = chatRoom.Messages.First().Content,
                    MessageType = chatRoom.Messages.First().MessageType,
                    SentAt = chatRoom.Messages.First().SentAt
                } : null,
                UnreadCount = unreadCount
            };
        }

        public async Task<ChatRoomsResponseDto> GetUserChatRoomsAsync(int userId, int page = 1, int pageSize = 20)
        {
            var query = _context.ChatRoomMembers
                .Include(m => m.ChatRoom)
                    .ThenInclude(cr => cr.CreatedByUser)
                .Include(m => m.ChatRoom)
                    .ThenInclude(cr => cr.Members)
                        .ThenInclude(m => m.User)
                .Include(m => m.ChatRoom)
                    .ThenInclude(cr => cr.Messages.OrderByDescending(msg => msg.SentAt).Take(1))
                        .ThenInclude(msg => msg.Sender)
                .Where(m => m.UserId == userId && m.IsActive && m.ChatRoom.IsActive)
                .OrderByDescending(m => m.ChatRoom.LastActivity);

            var totalCount = await query.CountAsync();
            var members = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var chatRooms = new List<ChatRoomDto>();
            foreach (var member in members)
            {
                var chatRoom = member.ChatRoom;
                var unreadCount = await GetUnreadMessageCountAsync(chatRoom.Id, userId);

                // Get display name for private chats
                var displayName = chatRoom.Name;
                if (chatRoom.Type == ChatRoomType.Private)
                {
                    var otherMember = chatRoom.Members.FirstOrDefault(m => m.UserId != userId && m.IsActive);
                    if (otherMember != null)
                    {
                        displayName = $"{otherMember.User.FirstName ?? ""} {otherMember.User.LastName ?? ""}";
                    }
                }

                chatRooms.Add(new ChatRoomDto
                {
                    Id = chatRoom.Id,
                    Name = displayName,
                    Description = chatRoom.Description,
                    Type = chatRoom.Type,
                    CreatedByUserId = chatRoom.CreatedByUserId,
                    CreatedByUser = new UserSummaryDto
                    {
                        Id = chatRoom.CreatedByUser.Id,
                        Username = chatRoom.CreatedByUser.Username,
                        FirstName = chatRoom.CreatedByUser.FirstName ?? "",
                        LastName = chatRoom.CreatedByUser.LastName ?? "",
                        ProfilePictureUrl = chatRoom.CreatedByUser.ProfilePictureUrl
                    },
                    CreatedAt = chatRoom.CreatedAt,
                    LastActivity = chatRoom.LastActivity,
                    IsActive = chatRoom.IsActive,
                    Members = chatRoom.Members.Where(m => m.IsActive).Select(m => new ChatRoomMemberDto
                    {
                        Id = m.Id,
                        UserId = m.UserId,
                        User = new UserSummaryDto
                        {
                            Id = m.User.Id,
                            Username = m.User.Username,
                            FirstName = m.User.FirstName ?? "",
                            LastName = m.User.LastName ?? "",
                            ProfilePictureUrl = m.User.ProfilePictureUrl,
                            LastActive = m.User.LastActive
                        },
                        JoinedAt = m.JoinedAt,
                        LastReadAt = m.LastReadAt,
                        Role = m.Role,
                        IsActive = m.IsActive,
                        IsMuted = m.IsMuted
                    }).ToList(),
                    LastMessage = chatRoom.Messages.FirstOrDefault() != null ? new ChatMessageDto
                    {
                        Id = chatRoom.Messages.First().Id,
                        SenderId = chatRoom.Messages.First().SenderId,
                        Sender = new UserSummaryDto
                        {
                            Id = chatRoom.Messages.First().Sender.Id,
                            Username = chatRoom.Messages.First().Sender.Username,
                            FirstName = chatRoom.Messages.First().Sender.FirstName ?? "",
                            LastName = chatRoom.Messages.First().Sender.LastName ?? ""
                        },
                        Content = chatRoom.Messages.First().Content,
                        MessageType = chatRoom.Messages.First().MessageType,
                        SentAt = chatRoom.Messages.First().SentAt
                    } : null,
                    UnreadCount = unreadCount
                });
            }

            return new ChatRoomsResponseDto
            {
                ChatRooms = chatRooms,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                HasNext = page * pageSize < totalCount,
                HasPrevious = page > 1
            };
        }

        public async Task<ChatMessagesResponseDto> GetChatMessagesAsync(int chatRoomId, int currentUserId, int page = 1, int pageSize = 50)
        {
            // Verify user is a member
            var isMember = await _context.ChatRoomMembers
                .AnyAsync(m => m.ChatRoomId == chatRoomId && m.UserId == currentUserId && m.IsActive);

            if (!isMember)
            {
                throw new UnauthorizedAccessException("User is not a member of this chat room");
            }

            var query = _context.ChatMessages
                .Include(m => m.Sender)
                .Include(m => m.ReplyToMessage)
                    .ThenInclude(rm => rm!.Sender)
                .Include(m => m.Reactions)
                    .ThenInclude(r => r.User)
                .Include(m => m.ReadStatuses)
                    .ThenInclude(rs => rs.User)
                .Where(m => m.ChatRoomId == chatRoomId && !m.IsDeleted)
                .OrderByDescending(m => m.SentAt);

            var totalCount = await query.CountAsync();
            var messages = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var messageDtos = messages.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                ChatRoomId = m.ChatRoomId,
                SenderId = m.SenderId,
                Sender = new UserSummaryDto
                {
                    Id = m.Sender.Id,
                    Username = m.Sender.Username,
                    FirstName = m.Sender.FirstName ?? "",
                    LastName = m.Sender.LastName ?? "",
                    ProfilePictureUrl = m.Sender.ProfilePictureUrl
                },
                Content = m.Content,
                MessageType = m.MessageType,
                AttachmentUrl = m.AttachmentUrl,
                AttachmentType = m.AttachmentType,
                AttachmentName = m.AttachmentName,
                ReplyToMessageId = m.ReplyToMessageId,
                ReplyToMessage = m.ReplyToMessage != null ? new ChatMessageDto
                {
                    Id = m.ReplyToMessage.Id,
                    SenderId = m.ReplyToMessage.SenderId,
                    Sender = new UserSummaryDto
                    {
                        Id = m.ReplyToMessage.Sender.Id,
                        Username = m.ReplyToMessage.Sender.Username,
                        FirstName = m.ReplyToMessage.Sender.FirstName ?? "",
                        LastName = m.ReplyToMessage.Sender.LastName ?? ""
                    },
                    Content = m.ReplyToMessage.Content,
                    MessageType = m.ReplyToMessage.MessageType,
                    SentAt = m.ReplyToMessage.SentAt
                } : null,
                SentAt = m.SentAt,
                EditedAt = m.EditedAt,
                IsDeleted = m.IsDeleted,
                Reactions = m.Reactions.Select(r => new ChatMessageReactionDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    User = new UserSummaryDto
                    {
                        Id = r.User.Id,
                        Username = r.User.Username,
                        FirstName = r.User.FirstName ?? "",
                        LastName = r.User.LastName ?? ""
                    },
                    ReactionType = r.ReactionType,
                    CreatedAt = r.CreatedAt
                }).ToList(),
                ReadStatuses = m.ReadStatuses.Select(rs => new ChatMessageReadStatusDto
                {
                    Id = rs.Id,
                    UserId = rs.UserId,
                    User = new UserSummaryDto
                    {
                        Id = rs.User.Id,
                        Username = rs.User.Username,
                        FirstName = rs.User.FirstName ?? "",
                        LastName = rs.User.LastName ?? ""
                    },
                    ReadAt = rs.ReadAt
                }).ToList(),
                IsRead = m.ReadStatuses.Any(rs => rs.UserId == currentUserId)
            }).ToList();

            return new ChatMessagesResponseDto
            {
                Messages = messageDtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                HasNext = page * pageSize < totalCount,
                HasPrevious = page > 1
            };
        }

        public async Task<ChatMessageDto> SendMessageAsync(int chatRoomId, int senderId, SendMessageDto sendMessageDto)
        {
            // Verify user is a member
            var member = await _context.ChatRoomMembers
                .Include(m => m.ChatRoom)
                .FirstOrDefaultAsync(m => m.ChatRoomId == chatRoomId && m.UserId == senderId && m.IsActive);

            if (member == null)
            {
                throw new UnauthorizedAccessException("User is not a member of this chat room");
            }

            var message = new ChatMessage
            {
                ChatRoomId = chatRoomId,
                SenderId = senderId,
                Content = sendMessageDto.Content,
                MessageType = sendMessageDto.MessageType,
                AttachmentUrl = sendMessageDto.AttachmentUrl,
                AttachmentType = sendMessageDto.AttachmentType,
                AttachmentName = sendMessageDto.AttachmentName,
                ReplyToMessageId = sendMessageDto.ReplyToMessageId,
                SentAt = DateTime.UtcNow
            };

            _context.ChatMessages.Add(message);

            // Update chat room last activity
            member.ChatRoom.LastActivity = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Return message with related data
            return await GetMessageDtoAsync(message.Id);
        }

        public async Task<bool> AddMemberToChatRoomAsync(int chatRoomId, int currentUserId, AddMemberDto addMemberDto)
        {
            // Check if current user has permission (is owner or admin)
            var currentUserMember = await _context.ChatRoomMembers
                .FirstOrDefaultAsync(m => m.ChatRoomId == chatRoomId && m.UserId == currentUserId && m.IsActive);

            if (currentUserMember == null || (currentUserMember.Role != ChatMemberRole.Owner && currentUserMember.Role != ChatMemberRole.Admin))
            {
                return false;
            }

            // Check if user is already a member
            var existingMember = await _context.ChatRoomMembers
                .FirstOrDefaultAsync(m => m.ChatRoomId == chatRoomId && m.UserId == addMemberDto.UserId);

            if (existingMember != null)
            {
                if (existingMember.IsActive) return false; // Already active member
                
                // Reactivate inactive member
                existingMember.IsActive = true;
                existingMember.JoinedAt = DateTime.UtcNow;
            }
            else
            {
                // Add new member
                var newMember = new ChatRoomMember
                {
                    ChatRoomId = chatRoomId,
                    UserId = addMemberDto.UserId,
                    Role = addMemberDto.Role,
                    JoinedAt = DateTime.UtcNow
                };
                _context.ChatRoomMembers.Add(newMember);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveMemberFromChatRoomAsync(int chatRoomId, int currentUserId, int memberUserId)
        {
            // Check if current user has permission (is owner or admin)
            var currentUserMember = await _context.ChatRoomMembers
                .FirstOrDefaultAsync(m => m.ChatRoomId == chatRoomId && m.UserId == currentUserId && m.IsActive);

            if (currentUserMember == null || (currentUserMember.Role != ChatMemberRole.Owner && currentUserMember.Role != ChatMemberRole.Admin))
            {
                return false;
            }

            var memberToRemove = await _context.ChatRoomMembers
                .FirstOrDefaultAsync(m => m.ChatRoomId == chatRoomId && m.UserId == memberUserId && m.IsActive);

            if (memberToRemove == null) return false;

            // Don't allow removing the owner unless it's the owner removing themselves
            if (memberToRemove.Role == ChatMemberRole.Owner && currentUserId != memberUserId)
            {
                return false;
            }

            memberToRemove.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> LeaveChatRoomAsync(int chatRoomId, int userId)
        {
            var member = await _context.ChatRoomMembers
                .FirstOrDefaultAsync(m => m.ChatRoomId == chatRoomId && m.UserId == userId && m.IsActive);

            if (member == null) return false;

            member.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteChatRoomAsync(int chatRoomId, int currentUserId)
        {
            var chatRoom = await _context.ChatRooms
                .FirstOrDefaultAsync(cr => cr.Id == chatRoomId && cr.CreatedByUserId == currentUserId && cr.IsActive);

            if (chatRoom == null) return false;

            chatRoom.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ChatRoomDto?> GetOrCreatePrivateChatAsync(int currentUserId, int otherUserId)
        {
            // Try to find existing private chat
            var existingChat = await GetExistingPrivateChatAsync(currentUserId, otherUserId);
            if (existingChat != null) return existingChat;

            // Create new private chat
            var otherUser = await _context.Users.FindAsync(otherUserId);
            if (otherUser == null) return null;

            var createChatDto = new CreateChatRoomDto
            {
                Name = $"Private chat with {otherUser.FirstName ?? ""} {otherUser.LastName ?? ""}",
                Type = ChatRoomType.Private,
                MemberUserIds = new List<int> { otherUserId }
            };

            return await CreateChatRoomAsync(currentUserId, createChatDto);
        }

        public async Task<bool> MarkMessagesAsReadAsync(int chatRoomId, int userId, List<int> messageIds)
        {
            // Verify user is a member
            var isMember = await _context.ChatRoomMembers
                .AnyAsync(m => m.ChatRoomId == chatRoomId && m.UserId == userId && m.IsActive);

            if (!isMember) return false;

            foreach (var messageId in messageIds)
            {
                var existingReadStatus = await _context.ChatMessageReadStatuses
                    .FirstOrDefaultAsync(rs => rs.MessageId == messageId && rs.UserId == userId);

                if (existingReadStatus == null)
                {
                    var readStatus = new ChatMessageReadStatus
                    {
                        MessageId = messageId,
                        UserId = userId,
                        ReadAt = DateTime.UtcNow
                    };
                    _context.ChatMessageReadStatuses.Add(readStatus);
                }
            }

            // Update member's last read time
            var member = await _context.ChatRoomMembers
                .FirstOrDefaultAsync(m => m.ChatRoomId == chatRoomId && m.UserId == userId);
            if (member != null)
            {
                member.LastReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<UserSummaryDto>> SearchUsersForChatAsync(string searchTerm, int currentUserId)
        {
            var users = await _context.Users
                .Where(u => u.Id != currentUserId && 
                           (u.Username.Contains(searchTerm) || 
                            (u.FirstName != null && u.FirstName.Contains(searchTerm)) || 
                            (u.LastName != null && u.LastName.Contains(searchTerm)) ||
                            ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Contains(searchTerm)))
                .Take(20)
                .Select(u => new UserSummaryDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    FirstName = u.FirstName ?? "",
                    LastName = u.LastName ?? "",
                    ProfilePictureUrl = u.ProfilePictureUrl,
                    LastActive = u.LastActive
                })
                .ToListAsync();

            return users;
        }

        public async Task<bool> UpdateChatRoomAsync(int chatRoomId, int currentUserId, string name, string? description)
        {
            var member = await _context.ChatRoomMembers
                .Include(m => m.ChatRoom)
                .FirstOrDefaultAsync(m => m.ChatRoomId == chatRoomId && m.UserId == currentUserId && m.IsActive);

            if (member == null || (member.Role != ChatMemberRole.Owner && member.Role != ChatMemberRole.Admin))
            {
                return false;
            }

            member.ChatRoom.Name = name;
            member.ChatRoom.Description = description;
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<ChatRoomDto?> GetExistingPrivateChatAsync(int userId1, int userId2)
        {
            var chatRoom = await _context.ChatRooms
                .Include(cr => cr.Members)
                    .ThenInclude(m => m.User)
                .Include(cr => cr.CreatedByUser)
                .Where(cr => cr.Type == ChatRoomType.Private && cr.IsActive)
                .Where(cr => cr.Members.Count(m => m.IsActive) == 2)
                .Where(cr => cr.Members.Any(m => m.UserId == userId1 && m.IsActive) &&
                            cr.Members.Any(m => m.UserId == userId2 && m.IsActive))
                .FirstOrDefaultAsync();

            if (chatRoom == null) return null;

            return await GetChatRoomAsync(chatRoom.Id, userId1);
        }

        private async Task<int> GetUnreadMessageCountAsync(int chatRoomId, int userId)
        {
            var member = await _context.ChatRoomMembers
                .FirstOrDefaultAsync(m => m.ChatRoomId == chatRoomId && m.UserId == userId);

            if (member?.LastReadAt == null)
            {
                return await _context.ChatMessages
                    .CountAsync(m => m.ChatRoomId == chatRoomId && m.SenderId != userId && !m.IsDeleted);
            }

            return await _context.ChatMessages
                .CountAsync(m => m.ChatRoomId == chatRoomId && 
                               m.SenderId != userId && 
                               m.SentAt > member.LastReadAt && 
                               !m.IsDeleted);
        }

        private async Task<ChatMessageDto> GetMessageDtoAsync(int messageId)
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
                SenderId = message.SenderId,
                Sender = new UserSummaryDto
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
                        Username = message.ReplyToMessage.Sender.Username,
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
                        Username = r.User.Username,
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
                        Username = rs.User.Username,
                        FirstName = rs.User.FirstName ?? "",
                        LastName = rs.User.LastName ?? ""
                    },
                    ReadAt = rs.ReadAt
                }).ToList()
            };
        }
    }
}
