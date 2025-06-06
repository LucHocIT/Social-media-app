using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Services.Chat;

public class MessageReactionService : IMessageReactionService
{
    private readonly SocialMediaDbContext _context;
    private readonly ILogger<MessageReactionService> _logger;

    public MessageReactionService(SocialMediaDbContext context, ILogger<MessageReactionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<MessageReactionDto?> AddReactionAsync(int userId, CreateMessageReactionDto reactionDto)
    {
        try
        {
            // Check if message exists and user has access to it
            var message = await _context.SimpleMessages
                .Include(m => m.Conversation)
                .FirstOrDefaultAsync(m => m.Id == reactionDto.MessageId);

            if (message == null)
            {
                _logger.LogWarning("Message {MessageId} not found", reactionDto.MessageId);
                return null;
            }

            // Check if user is part of the conversation
            if (message.Conversation.User1Id != userId && message.Conversation.User2Id != userId)
            {
                _logger.LogWarning("User {UserId} does not have access to message {MessageId}", userId, reactionDto.MessageId);
                return null;
            }

            // Check if user already has a reaction on this message
            var existingReaction = await _context.MessageReactions
                .FirstOrDefaultAsync(r => r.MessageId == reactionDto.MessageId && r.UserId == userId);

            if (existingReaction != null)
            {                // Update existing reaction
                existingReaction.ReactionType = reactionDto.ReactionType;
                existingReaction.CreatedAt = DateTime.Now;
                _context.MessageReactions.Update(existingReaction);
            }            else
            {
                // Create new reaction
                var newReaction = new MessageReaction
                {
                    MessageId = reactionDto.MessageId,
                    UserId = userId,
                    ReactionType = reactionDto.ReactionType,
                    CreatedAt = DateTime.Now
                };

                _context.MessageReactions.Add(newReaction);
                existingReaction = newReaction;
            }

            await _context.SaveChangesAsync();

            // Return the reaction DTO
            var user = await _context.Users.FindAsync(userId);
            return new MessageReactionDto
            {
                Id = existingReaction.Id,
                MessageId = existingReaction.MessageId,
                UserId = existingReaction.UserId,
                Username = user?.Username ?? "",
                FirstName = user?.FirstName,
                LastName = user?.LastName,
                ProfilePictureUrl = user?.ProfilePictureUrl,
                ReactionType = existingReaction.ReactionType,
                CreatedAt = existingReaction.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding reaction for user {UserId} on message {MessageId}", userId, reactionDto.MessageId);
            throw;
        }
    }

    public async Task<bool> RemoveReactionAsync(int userId, int messageId)
    {
        try
        {
            var reaction = await _context.MessageReactions
                .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId);

            if (reaction == null)
            {
                return false;
            }

            _context.MessageReactions.Remove(reaction);
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing reaction for user {UserId} on message {MessageId}", userId, messageId);
            throw;
        }
    }

    public async Task<MessageReactionSummaryDto?> GetMessageReactionsAsync(int messageId, int? currentUserId = null)
    {
        try
        {
            var reactions = await _context.MessageReactions
                .Include(r => r.User)
                .Where(r => r.MessageId == messageId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var reactionCounts = reactions
                .GroupBy(r => r.ReactionType)
                .ToDictionary(g => g.Key, g => g.Count());

            var currentUserReaction = currentUserId.HasValue 
                ? reactions.FirstOrDefault(r => r.UserId == currentUserId.Value)
                : null;

            var recentReactions = reactions
                .Take(5) // Show 5 most recent reactions
                .Select(r => new MessageReactionDto
                {
                    Id = r.Id,
                    MessageId = r.MessageId,
                    UserId = r.UserId,
                    Username = r.User.Username,
                    FirstName = r.User.FirstName,
                    LastName = r.User.LastName,
                    ProfilePictureUrl = r.User.ProfilePictureUrl,
                    ReactionType = r.ReactionType,
                    CreatedAt = r.CreatedAt
                })
                .ToList();

            return new MessageReactionSummaryDto
            {
                MessageId = messageId,
                ReactionCounts = reactionCounts,
                HasReactedByCurrentUser = currentUserReaction != null,
                CurrentUserReactionType = currentUserReaction?.ReactionType,
                RecentReactions = recentReactions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reactions for message {MessageId}", messageId);
            throw;
        }
    }

    public async Task<List<MessageReactionDto>> GetReactionsByMessageIdAsync(int messageId)
    {
        try
        {
            var reactions = await _context.MessageReactions
                .Include(r => r.User)
                .Where(r => r.MessageId == messageId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new MessageReactionDto
                {
                    Id = r.Id,
                    MessageId = r.MessageId,
                    UserId = r.UserId,
                    Username = r.User.Username,
                    FirstName = r.User.FirstName,
                    LastName = r.User.LastName,
                    ProfilePictureUrl = r.User.ProfilePictureUrl,
                    ReactionType = r.ReactionType,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            return reactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reactions for message {MessageId}", messageId);
            throw;
        }
    }

    public async Task<List<MessageReactionDto>> GetReactionsByTypeAsync(int messageId, string reactionType)
    {
        try
        {
            var reactions = await _context.MessageReactions
                .Include(r => r.User)
                .Where(r => r.MessageId == messageId && r.ReactionType == reactionType)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new MessageReactionDto
                {
                    Id = r.Id,
                    MessageId = r.MessageId,
                    UserId = r.UserId,
                    Username = r.User.Username,
                    FirstName = r.User.FirstName,
                    LastName = r.User.LastName,
                    ProfilePictureUrl = r.User.ProfilePictureUrl,
                    ReactionType = r.ReactionType,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            return reactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reactions by type {ReactionType} for message {MessageId}", reactionType, messageId);
            throw;
        }
    }

    public async Task<bool> ToggleReactionAsync(int userId, CreateMessageReactionDto reactionDto)
    {
        try
        {
            var existingReaction = await _context.MessageReactions
                .FirstOrDefaultAsync(r => r.MessageId == reactionDto.MessageId && r.UserId == userId);

            if (existingReaction != null)
            {
                if (existingReaction.ReactionType == reactionDto.ReactionType)
                {
                    // Same reaction type - remove it (toggle off)
                    _context.MessageReactions.Remove(existingReaction);
                    await _context.SaveChangesAsync();
                    return false; // Reaction removed
                }                else
                {
                    // Different reaction type - update it
                    existingReaction.ReactionType = reactionDto.ReactionType;
                    existingReaction.CreatedAt = DateTime.Now;
                    _context.MessageReactions.Update(existingReaction);
                    await _context.SaveChangesAsync();
                    return true; // Reaction updated
                }
            }
            else
            {
                // No existing reaction - add new one
                await AddReactionAsync(userId, reactionDto);
                return true; // Reaction added
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling reaction for user {UserId} on message {MessageId}", userId, reactionDto.MessageId);
            throw;
        }
    }
}
