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

    public UserPresenceService(
        IServiceProvider serviceProvider,
        ILogger<UserPresenceService> logger,
        IHubContext<SimpleChatHub> hubContext)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckUserPresence();
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Kiểm tra mỗi phút
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user presence");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task CheckUserPresence()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SocialMediaDbContext>();

        try
        {
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
            _logger.LogError(ex, "Error updating user presence");
        }
    }
}
