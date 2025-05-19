using Microsoft.EntityFrameworkCore;
using SocialApp.Models;

namespace SocialApp.Services.User;

public class UserManagementService : IUserManagementService
{
    private readonly SocialMediaDbContext _context;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(
        SocialMediaDbContext context,
        ILogger<UserManagementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> SetUserRoleAsync(int userId, string role)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("SetUserRoleAsync failed: User with ID {UserId} not found", userId);
            return false;
        }

        // Allow only valid roles
        if (role != "Admin" && role != "User")
        {
            _logger.LogWarning("SetUserRoleAsync failed: Invalid role {Role} for user {UserId}", role, userId);
            return false;
        }

        user.Role = role;
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("User {UserId} role set to {Role}", userId, role);
        return true;
    }

    public async Task<bool> SoftDeleteUserAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("SoftDeleteUserAsync failed: User with ID {UserId} not found", userId);
            return false;
        }

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("User {UserId} soft deleted", userId);
        return true;
    }

    public async Task<bool> RestoreUserAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("RestoreUserAsync failed: User with ID {UserId} not found", userId);
            return false;
        }

        user.IsDeleted = false;
        user.DeletedAt = null;
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("User {UserId} restored", userId);
        return true;
    }
}
