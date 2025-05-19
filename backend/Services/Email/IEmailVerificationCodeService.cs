using SocialApp.DTOs;

namespace SocialApp.Services.Email;

public interface IEmailVerificationCodeService
{
    Task<(bool Success, string Message)> SendVerificationCodeAsync(string email);
    Task<(bool Success, string Message)> VerifyCodeAsync(string email, string code);
    Task<(bool Success, string Message)> SendPasswordResetCodeAsync(string email);
    Task<(bool Success, string Message)> VerifyPasswordResetCodeAsync(string email, string code);
    Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordDTO resetPasswordDto);
}
