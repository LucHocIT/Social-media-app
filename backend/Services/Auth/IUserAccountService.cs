using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Services.Auth;

public interface IUserAccountService
{
    Task<AuthResponseDTO> RegisterAsync(RegisterUserDTO registerDto);
    Task<(AuthResponseDTO? Result, bool Success, string? ErrorMessage)> LoginAsync(LoginUserDTO loginDto);
    Task<bool> EmailExistsAsync(string email);
    string GenerateJwtToken(SocialApp.Models.User user);
    Task<AuthResponseDTO> RegisterVerifiedUserAsync(VerifiedRegisterDTO registerDto);
    Task<UserResponseDTO?> GetUserByIdAsync(int userId);
}
