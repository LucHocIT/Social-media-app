using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Services;

public interface IAuthService
{
    Task<AuthResponseDTO> RegisterAsync(RegisterUserDTO registerDto);
    Task<(AuthResponseDTO? Result, bool Success, string? ErrorMessage)> LoginAsync(LoginUserDTO loginDto);
    Task<bool> EmailExistsAsync(string email);
    string GenerateJwtToken(User user);
      // Email verification methods
    Task<(bool Success, string Message)> SendVerificationCodeAsync(string email);
    Task<(bool Success, string Message)> VerifyCodeAsync(string email, string code);
    Task<AuthResponseDTO> RegisterVerifiedUserAsync(VerifiedRegisterDTO registerDto);
    
    // New methods for user role management
    Task<bool> SetUserRoleAsync(int userId, string role);
    Task<bool> SoftDeleteUserAsync(int userId);
    Task<bool> RestoreUserAsync(int userId);
    Task<UserResponseDTO?> GetUserByIdAsync(int userId);
}
