using Microsoft.EntityFrameworkCore;
using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Services.Email;

public class EmailVerificationService : IEmailVerificationService
{
    private readonly SocialMediaDbContext _context;
    private readonly ILogger<EmailVerificationService> _logger;

    public EmailVerificationService(
        SocialMediaDbContext context,
        ILogger<EmailVerificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Helper method to generate a random 6-digit code
    private string GenerateRandomCode()
    {
        // Generate a 6-digit random code
        Random random = new Random();
        return random.Next(100000, 999999).ToString();
    }

    // Helper method to check if email format is valid
    private bool IsValidEmailFormat(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
    
    // Implementation of email verification method
    public async Task<(bool IsValid, bool Exists, string Message)> VerifyEmailAsync(string email)
    {
        // Basic validation of email format
        if (!IsValidEmailFormat(email))
        {
            return (false, false, "Invalid email format");
        }
        
        // In a real application, you might use an email validation service
        // For simplicity, we'll assume the email exists if it has a valid format
        // and is from a common domain
        
        string domain = email.Split('@').LastOrDefault()?.ToLower();
        bool likelyExists = domain != null && (
            domain.Contains("gmail.com") || 
            domain.Contains("yahoo.com") || 
            domain.Contains("outlook.com") ||
            domain.Contains("hotmail.com") ||
            domain.Contains("mail.com")
        );
        
        return (true, likelyExists, likelyExists ? "Email likely exists" : "Email may not exist");
    }
    
    // Send verification code to user's email
    public async Task<(bool Success, string Message)> SendVerificationCodeAsync(string email)
    {
        try
        {
            if (!IsValidEmailFormat(email))
            {
                _logger.LogWarning("Invalid email format: {Email}", email);
                return (false, "Email không hợp lệ, vui lòng kiểm tra lại");
            }
              // Check if email already exists in our database
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower()))
            {
                _logger.LogWarning("Email already exists: {Email}", email);
                return (false, "Email đã được đăng ký");
            }
            
            // Verify email format and existence
            var emailVerification = await VerifyEmailAsync(email);
            
            if (!emailVerification.IsValid)
            {
                _logger.LogWarning("Invalid email format: {Email}", email);
                return (false, "Email không hợp lệ, vui lòng kiểm tra lại");
            }

            if (!emailVerification.Exists)
            {
                _logger.LogWarning("Email does not exist: {Email}", email);
                return (false, "Email không tồn tại hoặc không thể gửi thư đến địa chỉ này");
            }
            
            // Generate verification code
            string verificationCode = GenerateRandomCode();
            
            // Set expiration time (10 minutes from now)
            var expiresAt = DateTime.UtcNow.AddMinutes(10);
            
            // Check if there's an existing code for this email
            var existingCode = await _context.EmailVerificationCodes
                .FirstOrDefaultAsync(c => c.Email.ToLower() == email.ToLower() && !c.IsUsed);
                
            if (existingCode != null)
            {
                // Update existing code
                existingCode.Code = verificationCode;
                existingCode.CreatedAt = DateTime.UtcNow;
                existingCode.ExpiresAt = expiresAt;
                existingCode.IsUsed = false;
            }
            else
            {
                // Create new verification code entry
                var newVerificationCode = new EmailVerificationCode
                {
                    Email = email,
                    Code = verificationCode,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    IsUsed = false
                };
                
                await _context.EmailVerificationCodes.AddAsync(newVerificationCode);
            }
            
            await _context.SaveChangesAsync();
            
            // TODO: Send email with verification code
            // For now, just log the code (in a real application, you would use an email service)
            _logger.LogInformation("Verification code for {Email}: {Code}", email, verificationCode);
            
            return (true, "Mã xác nhận đã được gửi đến email của bạn");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending verification code to {Email}", email);
            return (false, "Đã xảy ra lỗi khi gửi mã xác nhận. Vui lòng thử lại sau.");
        }
    }
      
    // Verify the code provided by the user
    public async Task<(bool Success, string Message)> VerifyCodeAsync(string email, string code)
    {
        try
        {
            // Find the verification code for the email
            var verificationCode = await _context.EmailVerificationCodes
                .FirstOrDefaultAsync(c => 
                    c.Email.ToLower() == email.ToLower() && 
                    c.Code == code && 
                    !c.IsUsed);
            
            if (verificationCode == null)
            {
                return (false, "Mã xác nhận không chính xác");
            }
            
            // Check if code has expired
            if (verificationCode.ExpiresAt < DateTime.UtcNow)
            {
                return (false, "Mã xác nhận đã hết hạn");
            }
            
            // Mark the code as used
            verificationCode.IsUsed = true;
            await _context.SaveChangesAsync();
            
            return (true, "Xác thực thành công. Email này đã sẵn sàng để đăng ký.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying code for {Email}", email);
            return (false, "Đã xảy ra lỗi khi xác thực mã. Vui lòng thử lại sau.");
        }
    }

    // Send password reset code to user's email
    public async Task<(bool Success, string Message)> SendPasswordResetCodeAsync(string email)
    {
        try
        {
            if (!IsValidEmailFormat(email))
            {
                _logger.LogWarning("Invalid email format for password reset: {Email}", email);
                return (false, "Email không hợp lệ, vui lòng kiểm tra lại");
            }
              // Check if email exists in our database
            if (!await _context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower()))
            {
                _logger.LogWarning("Email not found for password reset: {Email}", email);
                return (false, "Email không tồn tại trong hệ thống");
            }
            
            // Generate verification code
            string verificationCode = GenerateRandomCode();
            
            // Set expiration time (10 minutes from now)
            var expiresAt = DateTime.UtcNow.AddMinutes(10);
            
            // Check if there's an existing code for this email
            var existingCode = await _context.EmailVerificationCodes
                .FirstOrDefaultAsync(c => c.Email.ToLower() == email.ToLower() && !c.IsUsed);
                
            if (existingCode != null)
            {
                // Update existing code
                existingCode.Code = verificationCode;
                existingCode.CreatedAt = DateTime.UtcNow;
                existingCode.ExpiresAt = expiresAt;
                existingCode.IsUsed = false;
            }
            else
            {
                // Create new verification code entry
                var newVerificationCode = new EmailVerificationCode
                {
                    Email = email,
                    Code = verificationCode,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    IsUsed = false
                };
                
                await _context.EmailVerificationCodes.AddAsync(newVerificationCode);
            }
            
            await _context.SaveChangesAsync();
            
            // TODO: Send email with verification code
            // For now, just log the code (in a real application, you would use an email service)
            _logger.LogInformation("Password reset verification code for {Email}: {Code}", email, verificationCode);
            
            return (true, "Mã xác nhận đã được gửi đến email của bạn");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending password reset code to {Email}", email);
            return (false, "Đã xảy ra lỗi khi gửi mã xác nhận. Vui lòng thử lại sau.");
        }
    }

    // Verify the password reset code provided by the user
    public async Task<(bool Success, string Message)> VerifyPasswordResetCodeAsync(string email, string code)
    {
        try
        {
            // Find the verification code for the email
            var verificationCode = await _context.EmailVerificationCodes
                .FirstOrDefaultAsync(c => 
                    c.Email.ToLower() == email.ToLower() && 
                    c.Code == code && 
                    !c.IsUsed);
            
            if (verificationCode == null)
            {
                return (false, "Mã xác nhận không chính xác");
            }
            
            // Check if code has expired
            if (verificationCode.ExpiresAt < DateTime.UtcNow)
            {
                return (false, "Mã xác nhận đã hết hạn");
            }
            
            // Don't mark the code as used yet, we'll do that after password reset
            
            return (true, "Xác thực thành công. Bạn có thể đặt lại mật khẩu.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying password reset code for {Email}", email);
            return (false, "Đã xảy ra lỗi khi xác thực mã. Vui lòng thử lại sau.");
        }
    }

    // Reset user's password after verification
    public async Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordDTO resetPasswordDto)
    {
        try
        {
            // Verify the code again
            var (codeValid, message) = await VerifyPasswordResetCodeAsync(resetPasswordDto.Email, resetPasswordDto.Code);
            
            if (!codeValid)
            {
                return (false, message);
            }
            
            // Check if passwords match (even though there's annotation)
            if (resetPasswordDto.NewPassword != resetPasswordDto.ConfirmPassword)
            {
                return (false, "Mật khẩu xác nhận không khớp");
            }
            
            // Find the user by email
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == resetPasswordDto.Email.ToLower());
                
            if (user == null)
            {
                return (false, "Người dùng không tồn tại");
            }
            
            // Update the user's password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(resetPasswordDto.NewPassword);
            user.LastActive = DateTime.UtcNow;
            
            // Mark the verification code as used
            var verificationCode = await _context.EmailVerificationCodes
                .FirstOrDefaultAsync(c => 
                    c.Email.ToLower() == resetPasswordDto.Email.ToLower() && 
                    c.Code == resetPasswordDto.Code && 
                    !c.IsUsed);
                
            if (verificationCode != null)
            {
                verificationCode.IsUsed = true;
            }
            
            await _context.SaveChangesAsync();
            
            return (true, "Đặt lại mật khẩu thành công");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for {Email}", resetPasswordDto.Email);
            return (false, "Đã xảy ra lỗi khi đặt lại mật khẩu. Vui lòng thử lại sau.");
        }
    }
}
