using SocialApp.DTOs;

namespace SocialApp.Services.Chat;

/// <summary>
/// Combined interface that includes both conversation and message operations
/// This interface serves as a facade for maintaining backward compatibility
/// </summary>
public interface ISimpleChatService : IConversationService, IMessageService
{
    // This interface now inherits all methods from both IConversationService and IMessageService
    // No additional methods needed as it combines both services
}
