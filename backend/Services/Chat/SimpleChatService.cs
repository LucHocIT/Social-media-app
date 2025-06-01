using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SocialApp.DTOs;
using SocialApp.Models;
using SocialApp.Services.Utils;
using SocialApp.Services.User;
using SocialApp.Hubs;

namespace SocialApp.Services.Chat;

/// <summary>
/// Facade pattern implementation that combines ConversationService and MessageService
/// This maintains backward compatibility while delegating to specialized services
/// </summary>
public class SimpleChatService : ISimpleChatService
{
    private readonly IConversationService _conversationService;
    private readonly IMessageService _messageService;
    private readonly ILogger<SimpleChatService> _logger;

    public SimpleChatService(
        IConversationService conversationService,
        IMessageService messageService,
        ILogger<SimpleChatService> logger)
    {
        _conversationService = conversationService;
        _messageService = messageService;
        _logger = logger;
    }

    // Conversation-related methods - delegate to ConversationService
    public async Task<ConversationsListDto> GetUserConversationsAsync(int userId)
    {
        _logger.LogDebug("Delegating GetUserConversationsAsync to ConversationService for user {UserId}", userId);
        return await _conversationService.GetUserConversationsAsync(userId);
    }

    public async Task<SimpleConversationDto?> GetOrCreateConversationAsync(int currentUserId, int otherUserId)
    {
        _logger.LogDebug("Delegating GetOrCreateConversationAsync to ConversationService for users {CurrentUserId} and {OtherUserId}", currentUserId, otherUserId);
        return await _conversationService.GetOrCreateConversationAsync(currentUserId, otherUserId);
    }

    public async Task<bool> MarkConversationAsReadAsync(int conversationId, int userId)
    {
        _logger.LogDebug("Delegating MarkConversationAsReadAsync to ConversationService for conversation {ConversationId} and user {UserId}", conversationId, userId);
        return await _conversationService.MarkConversationAsReadAsync(conversationId, userId);
    }

    public async Task<bool> AreFriendsAsync(int userId1, int userId2)
    {
        _logger.LogDebug("Delegating AreFriendsAsync to ConversationService for users {UserId1} and {UserId2}", userId1, userId2);
        return await _conversationService.AreFriendsAsync(userId1, userId2);
    }

    public async Task<bool> HideConversationAsync(int conversationId, int userId)
    {
        _logger.LogDebug("Delegating HideConversationAsync to ConversationService for conversation {ConversationId} and user {UserId}", conversationId, userId);
        return await _conversationService.HideConversationAsync(conversationId, userId);
    }

    public async Task<int> GetUnreadCountAsync(int conversationId, int userId)
    {
        _logger.LogDebug("Delegating GetUnreadCountAsync to ConversationService for conversation {ConversationId} and user {UserId}", conversationId, userId);
        return await _conversationService.GetUnreadCountAsync(conversationId, userId);
    }

    // Message-related methods - delegate to MessageService
    public async Task<ConversationMessagesResponseDto> GetConversationMessagesAsync(int conversationId, int currentUserId, int page = 1, int pageSize = 50)
    {
        _logger.LogDebug("Delegating GetConversationMessagesAsync to MessageService for conversation {ConversationId} and user {UserId}", conversationId, currentUserId);
        return await _messageService.GetConversationMessagesAsync(conversationId, currentUserId, page, pageSize);
    }

    public async Task<SimpleMessageDto> SendMessageAsync(int conversationId, int senderId, SendSimpleMessageDto messageDto, bool sendSignalR = true)
    {
        _logger.LogDebug("Delegating SendMessageAsync to MessageService for conversation {ConversationId} and sender {SenderId}", conversationId, senderId);
        return await _messageService.SendMessageAsync(conversationId, senderId, messageDto, sendSignalR);
    }

    public async Task<UploadChatMediaResult> UploadChatMediaAsync(int userId, IFormFile mediaFile, string mediaType)
    {
        _logger.LogDebug("Delegating UploadChatMediaAsync to MessageService for user {UserId} and media type {MediaType}", userId, mediaType);
        return await _messageService.UploadChatMediaAsync(userId, mediaFile, mediaType);
    }
}
