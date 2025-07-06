using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SocialApp.Hubs;
using SocialApp.Models;

namespace SocialApp.Services.Chat;

public class UserPresenceService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UserPresenceService> _logger;
    private readonly IHubContext<SimpleChatHub> _hubContext;
    private readonly IConfiguration _configuration;

    public UserPresenceService(
        IServiceProvider serviceProvider,
        ILogger<UserPresenceService> logger,
        IHubContext<SimpleChatHub> hubContext,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _hubContext = hubContext;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Check if user presence tracking is enabled
        var enableUserPresence = _configuration.GetValue("EnableUserPresence", true);
        if (!enableUserPresence)
        {
            _logger.LogInformation("User presence tracking is disabled");
            return;
        }

        // Wait a bit before starting to ensure the application is fully initialized
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckUserPresence();
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Kiểm tra mỗi phút
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user presence");
                // Wait longer after an error before retrying
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task CheckUserPresence()
    {
        using var scope = _serviceProvider.CreateScope();
        
        try
        {
            // Try to get the context with timeout
            var context = scope.ServiceProvider.GetRequiredService<SocialMediaDbContext>();
            
            // Test the connection with a simple query first
            if (!await context.Database.CanConnectAsync())
            {
                _logger.LogWarning("Cannot connect to database, skipping user presence check");
                return;
            }
            
            var oneMinuteAgo = DateTime.Now.AddMinutes(-1);
            
            // Tìm users đã offline (LastActive > 1 phút trước)
            var offlineUsers = await context.Users
                .Where(u => u.LastActive.HasValue && u.LastActive.Value <= oneMinuteAgo)
                .Select(u => u.Id)
                .ToListAsync();

            // Thông báo trạng thái offline cho các user này
            foreach (var userId in offlineUsers)
            {
                await _hubContext.Clients.All.SendAsync("UserOffline", userId);
            }

            if (offlineUsers.Any())
            {
                _logger.LogInformation($"Marked {offlineUsers.Count} users as offline");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user presence in CheckUserPresence");
            throw; // Re-throw to let the main ExecuteAsync method handle it
        }
    }
}
