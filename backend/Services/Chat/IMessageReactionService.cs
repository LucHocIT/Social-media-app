using SocialApp.DTOs;

namespace SocialApp.Services.Chat;

public interface IMessageReactionService
{
    Task<MessageReactionDto?> AddReactionAsync(int userId, CreateMessageReactionDto reactionDto);
    Task<bool> RemoveReactionAsync(int userId, int messageId);
    Task<MessageReactionSummaryDto?> GetMessageReactionsAsync(int messageId, int? currentUserId = null);
    Task<List<MessageReactionDto>> GetReactionsByMessageIdAsync(int messageId);
    Task<List<MessageReactionDto>> GetReactionsByTypeAsync(int messageId, string reactionType);
    Task<bool> ToggleReactionAsync(int userId, CreateMessageReactionDto reactionDto);
}
