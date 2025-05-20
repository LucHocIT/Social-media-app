using SocialApp.DTOs;

namespace SocialApp.Services.Email;

public interface IEmailVerificationService
{
    // Email validation
    Task<(bool IsValid, bool Exists, string Message)> VerifyEmailAsync(string email);
    
    // Verification code management for registration
    Task<(bool Success, string Message)> SendVerificationCodeAsync(string email);
    Task<(bool Success, string Message)> VerifyCodeAsync(string email, string code);
    
    // Password reset functionality
    Task<(bool Success, string Message)> SendPasswordResetCodeAsync(string email);
    Task<(bool Success, string Message)> VerifyPasswordResetCodeAsync(string email, string code);
    Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordDTO resetPasswordDto);
}