using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Services;

public interface IAuthService
{
    Task<AuthResponseDTO> RegisterAsync(RegisterUserDTO registerDto);
    Task<AuthResponseDTO> LoginAsync(LoginUserDTO loginDto);
    Task<bool> EmailExistsAsync(string email);
    string GenerateJwtToken(User user);
}
