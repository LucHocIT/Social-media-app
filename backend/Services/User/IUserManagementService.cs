namespace SocialApp.Services.User;

public interface IUserManagementService
{
    Task<bool> SetUserRoleAsync(int userId, string role);
    Task<bool> SoftDeleteUserAsync(int userId);
    Task<bool> RestoreUserAsync(int userId);
}
