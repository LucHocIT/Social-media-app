using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SocialApp.DTOs;
using SocialApp.Models;
using SocialApp.Services.Email;
using SocialApp.Services.User;

namespace SocialApp.Services.Auth;

// This class now acts as an adapter that delegates to the appropriate specialized services
public class AuthService : IAuthService
{
    private readonly IUserAccountService _userAccountService;
    private readonly IEmailVerificationCodeService _verificationCodeService;
    private readonly IUserManagementService _userManagementService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserAccountService userAccountService,
        IEmailVerificationCodeService verificationCodeService,
        IUserManagementService userManagementService,
        ILogger<AuthService> logger)
    {
        _userAccountService = userAccountService;
        _verificationCodeService = verificationCodeService;
        _userManagementService = userManagementService;
        _logger = logger;
    }    // Delegate to UserAccountService
    public Task<bool> EmailExistsAsync(string email)
    {
        return _userAccountService.EmailExistsAsync(email);
    }
    
    public Task<AuthResponseDTO> RegisterAsync(RegisterUserDTO registerDto)
    {
        return _userAccountService.RegisterAsync(registerDto);
    }
    
    public Task<(AuthResponseDTO? Result, bool Success, string? ErrorMessage)> LoginAsync(LoginUserDTO loginDto)
    {
        return _userAccountService.LoginAsync(loginDto);
    }    public string GenerateJwtToken(SocialApp.Models.User user)
    {
        return _userAccountService.GenerateJwtToken(user);
    }

    public Task<AuthResponseDTO> RegisterVerifiedUserAsync(VerifiedRegisterDTO registerDto)
    {
        return _userAccountService.RegisterVerifiedUserAsync(registerDto);
    }

    public Task<UserResponseDTO?> GetUserByIdAsync(int userId)
    {
        return _userAccountService.GetUserByIdAsync(userId);
    }

    // Delegate to EmailVerificationCodeService
    public Task<(bool Success, string Message)> SendVerificationCodeAsync(string email)
    {
        return _verificationCodeService.SendVerificationCodeAsync(email);
    }

    public Task<(bool Success, string Message)> VerifyCodeAsync(string email, string code)
    {
        return _verificationCodeService.VerifyCodeAsync(email, code);
    }

    public Task<(bool Success, string Message)> SendPasswordResetCodeAsync(string email)
    {
        return _verificationCodeService.SendPasswordResetCodeAsync(email);
    }

    public Task<(bool Success, string Message)> VerifyPasswordResetCodeAsync(string email, string code)
    {
        return _verificationCodeService.VerifyPasswordResetCodeAsync(email, code);
    }

    public Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordDTO resetPasswordDto)
    {
        return _verificationCodeService.ResetPasswordAsync(resetPasswordDto);
    }

    // Delegate to UserManagementService
    public Task<bool> SetUserRoleAsync(int userId, string role)
    {
        return _userManagementService.SetUserRoleAsync(userId, role);
    }    public Task<bool> SoftDeleteUserAsync(int userId)
    {
        return _userManagementService.SoftDeleteUserAsync(userId);
    }
    
    public Task<bool> RestoreUserAsync(int userId)
    {
        return _userManagementService.RestoreUserAsync(userId);
    }
}
