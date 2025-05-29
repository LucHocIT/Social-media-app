using StackExchange.Redis;
using System.Text.Json;
using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Services.Message;

public class RedisMessageService : IRedisMessageService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisMessageService> _logger;
    
    private const string USER_ONLINE_PREFIX = "user:online:";
    private const string USER_CONNECTIONS_PREFIX = "user:connections:";
    private const string TYPING_PREFIX = "typing:";
    private const string MESSAGES_CACHE_PREFIX = "messages:";
    private const string UNREAD_COUNT_PREFIX = "unread:";
    private const string CONVERSATIONS_CACHE_PREFIX = "conversations:";
    
    private readonly TimeSpan TYPING_EXPIRY = TimeSpan.FromSeconds(5);
    private readonly TimeSpan MESSAGE_CACHE_EXPIRY = TimeSpan.FromHours(1);
    private readonly TimeSpan CONVERSATION_CACHE_EXPIRY = TimeSpan.FromMinutes(30);

    public RedisMessageService(IConnectionMultiplexer redis, ILogger<RedisMessageService> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }

    #region User Online Status
    
    public async Task SetUserOnlineAsync(int userId, string connectionId)
    {
        try
        {
            var onlineKey = USER_ONLINE_PREFIX + userId;
            var connectionsKey = USER_CONNECTIONS_PREFIX + userId;
            
            await _database.StringSetAsync(onlineKey, "1", TimeSpan.FromMinutes(5));
            await _database.SetAddAsync(connectionsKey, connectionId);
            await _database.KeyExpireAsync(connectionsKey, TimeSpan.FromMinutes(5));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting user {UserId} online", userId);
        }
    }

    public async Task SetUserOfflineAsync(int userId, string connectionId)
    {
        try
        {
            var connectionsKey = USER_CONNECTIONS_PREFIX + userId;
            await _database.SetRemoveAsync(connectionsKey, connectionId);
            
            var remainingConnections = await _database.SetLengthAsync(connectionsKey);
            if (remainingConnections == 0)
            {
                var onlineKey = USER_ONLINE_PREFIX + userId;
                await _database.KeyDeleteAsync(onlineKey);
                await _database.KeyDeleteAsync(connectionsKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting user {UserId} offline", userId);
        }
    }

    public async Task<bool> IsUserOnlineAsync(int userId)
    {
        try
        {
            var onlineKey = USER_ONLINE_PREFIX + userId;
            return await _database.KeyExistsAsync(onlineKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} is online", userId);
            return false;
        }
    }

    public async Task<List<string>> GetUserConnectionsAsync(int userId)
    {
        try
        {
            var connectionsKey = USER_CONNECTIONS_PREFIX + userId;
            var connections = await _database.SetMembersAsync(connectionsKey);
            return connections.Select(c => c.ToString()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connections for user {UserId}", userId);
            return new List<string>();
        }
    }

    #endregion

    #region Typing Indicators

    public async Task SetTypingAsync(int conversationId, int userId, bool isTyping)
    {
        try
        {
            var typingKey = TYPING_PREFIX + conversationId;
            
            if (isTyping)
            {
                await _database.SetAddAsync(typingKey, userId);
                await _database.KeyExpireAsync(typingKey, TYPING_EXPIRY);
            }
            else
            {
                await _database.SetRemoveAsync(typingKey, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting typing status for user {UserId} in conversation {ConversationId}", userId, conversationId);
        }
    }

    public async Task<List<int>> GetTypingUsersAsync(int conversationId)
    {
        try
        {
            var typingKey = TYPING_PREFIX + conversationId;
            var typingUsers = await _database.SetMembersAsync(typingKey);
            return typingUsers.Select(u => (int)u).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting typing users for conversation {ConversationId}", conversationId);
            return new List<int>();
        }
    }

    #endregion

    #region Message Caching

    public async Task CacheRecentMessagesAsync(int conversationId, List<MessageItemDTO> messages)
    {
        try
        {
            var cacheKey = MESSAGES_CACHE_PREFIX + conversationId;
            var json = JsonSerializer.Serialize(messages);
            await _database.StringSetAsync(cacheKey, json, MESSAGE_CACHE_EXPIRY);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching messages for conversation {ConversationId}", conversationId);
        }
    }

    public async Task<List<MessageItemDTO>?> GetCachedMessagesAsync(int conversationId)
    {
        try
        {
            var cacheKey = MESSAGES_CACHE_PREFIX + conversationId;
            var json = await _database.StringGetAsync(cacheKey);
            
            if (!json.HasValue) return null;
            
            return JsonSerializer.Deserialize<List<MessageItemDTO>>(json!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cached messages for conversation {ConversationId}", conversationId);
            return null;
        }
    }

    public async Task InvalidateMessageCacheAsync(int conversationId)
    {
        try
        {
            var cacheKey = MESSAGES_CACHE_PREFIX + conversationId;
            await _database.KeyDeleteAsync(cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating message cache for conversation {ConversationId}", conversationId);
        }
    }

    #endregion

    #region Unread Count

    public async Task SetUnreadCountAsync(int userId, int conversationId, int count)
    {
        try
        {
            var unreadKey = UNREAD_COUNT_PREFIX + userId + ":" + conversationId;
            if (count > 0)
            {
                await _database.StringSetAsync(unreadKey, count);
            }
            else
            {
                await _database.KeyDeleteAsync(unreadKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting unread count for user {UserId} conversation {ConversationId}", userId, conversationId);
        }
    }

    public async Task<int> GetUnreadCountAsync(int userId, int conversationId)
    {
        try
        {
            var unreadKey = UNREAD_COUNT_PREFIX + userId + ":" + conversationId;
            var count = await _database.StringGetAsync(unreadKey);
            return count.HasValue ? (int)count : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count for user {UserId} conversation {ConversationId}", userId, conversationId);
            return 0;
        }
    }

    public async Task IncrementUnreadCountAsync(int userId, int conversationId)
    {
        try
        {
            var unreadKey = UNREAD_COUNT_PREFIX + userId + ":" + conversationId;
            await _database.StringIncrementAsync(unreadKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing unread count for user {UserId} conversation {ConversationId}", userId, conversationId);
        }
    }

    public async Task ClearUnreadCountAsync(int userId, int conversationId)
    {
        try
        {
            var unreadKey = UNREAD_COUNT_PREFIX + userId + ":" + conversationId;
            await _database.KeyDeleteAsync(unreadKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing unread count for user {UserId} conversation {ConversationId}", userId, conversationId);
        }
    }

    #endregion

    #region Conversation List Caching

    public async Task CacheUserConversationsAsync(int userId, List<ConversationDTO> conversations)
    {
        try
        {
            var cacheKey = CONVERSATIONS_CACHE_PREFIX + userId;
            var json = JsonSerializer.Serialize(conversations);
            await _database.StringSetAsync(cacheKey, json, CONVERSATION_CACHE_EXPIRY);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching conversations for user {UserId}", userId);
        }
    }

    public async Task<List<ConversationDTO>?> GetCachedUserConversationsAsync(int userId)
    {
        try
        {
            var cacheKey = CONVERSATIONS_CACHE_PREFIX + userId;
            var json = await _database.StringGetAsync(cacheKey);
            
            if (!json.HasValue) return null;
            
            return JsonSerializer.Deserialize<List<ConversationDTO>>(json!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cached conversations for user {UserId}", userId);
            return null;
        }
    }

    public async Task InvalidateUserConversationsCacheAsync(int userId)
    {
        try
        {
            var cacheKey = CONVERSATIONS_CACHE_PREFIX + userId;
            await _database.KeyDeleteAsync(cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating conversations cache for user {UserId}", userId);
        }
    }

    #endregion
}
