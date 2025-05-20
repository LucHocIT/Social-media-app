using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocialApp.DTOs;
using SocialApp.Models;
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SocialApp.Services.Email;

public class EmailVerificationService : IEmailVerificationService
{
    private readonly SocialMediaDbContext _context;    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailVerificationService> _logger;
    private readonly IEmailService _emailService;
    private readonly string? _apiKey;
    
    public EmailVerificationService(
        SocialMediaDbContext context,
        IConfiguration configuration,
        ILogger<EmailVerificationService> logger,
        IEmailService emailService)
    {        _context = context;
        _configuration = configuration;
        _logger = logger;
        _emailService = emailService;
        _apiKey = Environment.GetEnvironmentVariable("EMAIL_VERIFICATION_API_KEY") ?? 
                  _configuration["EmailVerification:ApiKey"] ?? string.Empty;
    }// Helper method to generate a random 6-digit code
    private string GenerateRandomCode()
    {
        return new Random().Next(100000, 999999).ToString();
    }
    
    // Helper method to check if email format is valid
    private bool IsValidEmailFormat(string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            // Basic validation with MailAddress
            var addr = new System.Net.Mail.MailAddress(email);
            
            // Check domain part
            var parts = email.Split('@');
            if (parts.Length != 2)
                return false;
                
            var domain = parts[1];
            if (!domain.Contains('.') || domain.EndsWith('.') || parts[0].Length < 1 || domain.Length < 3)
                return false;
                
            return true;
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
            _logger.LogWarning("Invalid email format: {Email}", email);
            return (false, false, "Invalid email format");
        }
          try
        {
            // Get API Key from environment or configuration
            string apiKey = Environment.GetEnvironmentVariable("EMAIL_VERIFICATION_API_KEY") ?? 
                            _configuration["EmailVerification:ApiKey"] ?? string.Empty;
            
            // If no API key or it's a placeholder, use fallback validation
            if (string.IsNullOrEmpty(apiKey) || apiKey == "[EMAIL_VERIFICATION_API_KEY]")
            {
                _logger.LogInformation("Email verification API key not found - using fallback validation");
                return await FallbackEmailValidationAsync(email);
            }
            
            // Create HTTP handler with SSL configuration
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, errors) => true,
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
            };
            
            try
            {
                // Create HTTP client with the configured handler
                using (var httpClient = new HttpClient(handler))
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(10);
                    string endpoint = $"https://emailvalidation.abstractapi.com/v1/?api_key={apiKey}&email={Uri.EscapeDataString(email)}";
                    
                    var response = await httpClient.GetAsync(endpoint);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    
                    try
                    {
                        var options = new System.Text.Json.JsonDocumentOptions { AllowTrailingCommas = true };
                        using (var document = System.Text.Json.JsonDocument.Parse(content, options))
                        {
                            // Parse API response
                            bool isFormatValid = document.RootElement.TryGetProperty("is_valid_format", out var formatElement) && 
                                                formatElement.GetBoolean();
                            bool isFreeEmail = document.RootElement.TryGetProperty("is_free_email", out var freeEmailElement) && 
                                            freeEmailElement.GetBoolean();
                            bool isDisposable = document.RootElement.TryGetProperty("is_disposable_email", out var disposableElement) && 
                                            disposableElement.GetBoolean();
                            bool isMxFound = document.RootElement.TryGetProperty("is_mx_found", out var mxElement) && 
                                            mxElement.GetBoolean();
                            
                            // Evaluate email validity
                            bool isValid = isFormatValid;
                            bool exists = (isFreeEmail || isMxFound) && !isDisposable;
                            
                            string message = isValid && exists 
                                ? "Email is valid and exists" 
                                : !isValid 
                                    ? "Email format is invalid" 
                                    : isDisposable 
                                        ? "Disposable email addresses are not accepted"
                                        : "Email may not exist or is unreachable";
                                        
                            _logger.LogInformation("Email verification result for {Email}: Valid={IsValid}, Exists={Exists}, Message={Message}", 
                                email, isValid, exists, message);
                                
                            return (isValid, exists, message);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing API response for email {Email}", email);
                        // If we can't parse the response, fall back to basic validation
                        return await FallbackEmailValidationAsync(email);
                    }
                }
                  _logger.LogWarning("Email verification API request failed: {StatusCode}", response.StatusCode);
                    // Fallback to basic validation if API call fails
                    return await FallbackEmailValidationAsync(email);
                }
            }
            catch (HttpRequestException httpEx) when 
                (httpEx.InnerException is System.IO.IOException || 
                 httpEx.InnerException is System.Net.Sockets.SocketException)
            {
                _logger.LogWarning("SSL or connection error occurred: {Message}", httpEx.Message);
                return await FallbackEmailValidationAsync(email);
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("API request timed out for email: {Email}", email);
                return await FallbackEmailValidationAsync(email);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating email with API: {Email}", email);
            // Fallback to basic validation if API call throws exception
            return await FallbackEmailValidationAsync(email);
        }}
    
    // Fallback method for email validation when API is unavailable
    private Task<(bool IsValid, bool Exists, string Message)> FallbackEmailValidationAsync(string email)
    {
        if (!IsValidEmailFormat(email))
        {
            return Task.FromResult((false, false, "Email format is invalid"));
        }
        
        string[] parts = email.Split('@');
        string localPart = parts[0];
        string domain = parts[1].ToLower();
        
        // Common email providers list
        string[] commonDomains = new[] {
            "gmail.com", "yahoo.com", "outlook.com", "hotmail.com", 
            "mail.com", "icloud.com", "protonmail.com", "zoho.com",
            "aol.com", "yandex.com", "gmx.com", "tutanota.com"
        };
        
        // Disposable email domains
        string[] disposableDomains = new[] {
            "mailinator.com", "tempmail.com", "temp-mail.org", "fakeinbox.com",
            "guerrillamail.com", "yopmail.com", "sharklasers.com", "10minutemail.com"
        };
        
        bool isCommonDomain = commonDomains.Any(d => domain.Contains(d));
        bool isLikelyDisposable = disposableDomains.Any(d => domain.Contains(d));
        
        // Check for suspicious patterns
        bool isSuspicious = new[] { "admin", "info", "support", "test", "noreply" }.Any(s => localPart.Contains(s)) ||
                           Regex.IsMatch(localPart, @"^\d+$") || // Only numbers
                           Regex.IsMatch(localPart, @"\d{6,}") || // Contains 6+ consecutive digits
                           (localPart.Length > 15 && Regex.IsMatch(localPart, @"\d{4,}")) || // Long username with 4+ digits
                           Regex.IsMatch(localPart, @"^[a-zA-Z]{1,5}\d{8,}$"); // Few letters followed by many digits
        
        bool likelyExists = isCommonDomain && !isSuspicious && !isLikelyDisposable;
        
        string message = isLikelyDisposable 
            ? "Email appears to be from a disposable email service"
            : isSuspicious
                ? "Email appears to be suspicious or automatically generated"
            : likelyExists 
                ? "Email format is valid and likely exists" 
                : "Email format is valid but may not exist";
        
        return Task.FromResult((true, likelyExists, message));
    }
    
    // Send verification code to user's email
    public async Task<(bool Success, string Message)> SendVerificationCodeAsync(string email)
    {
        try
        {
            // Validate email format
            if (!IsValidEmailFormat(email))
            {
                _logger.LogWarning("Invalid email format: {Email}", email);
                return (false, "Email không hợp lệ, vui lòng kiểm tra lại");
            }
              
            // Check if email already exists in database
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower()))
            {
                return (false, "Email đã được đăng ký");
            }
              
            // Verify email format and existence
            var emailVerification = await VerifyEmailAsync(email);
            
            // Apply verification rules based on results
            if (!emailVerification.IsValid)
            {
                return (false, "Email không hợp lệ, vui lòng kiểm tra lại");
            }

            if (!emailVerification.Exists)
            {
                return (false, "Email không tồn tại hoặc không thể gửi thư đến địa chỉ này");
            }
              // Generate and save verification code
            string verificationCode = await SaveVerificationCodeAsync(email);
            
            await _context.SaveChangesAsync();
              // Send verification email using template
            string subject = "Xác nhận email đăng ký - SocialApp";
            string emailBody = _emailService.GenerateVerificationEmailTemplate(verificationCode);
            bool emailSent = await _emailService.SendHtmlEmailAsync(email, subject, emailBody);
            
            if (!emailSent)
            {
                _logger.LogError("Failed to send verification email to {Email}", email);
                return (false, "Không thể gửi email xác nhận. Vui lòng thử lại sau.");
            }
            
            return (true, "Mã xác nhận đã được gửi đến email của bạn");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending verification code to {Email}", email);
            return (false, "Đã xảy ra lỗi khi gửi mã xác nhận. Vui lòng thử lại sau.");
        }
    }
    // Helper method to save a verification code
    private async Task<string> SaveVerificationCodeAsync(string email)
    {
        string verificationCode = GenerateRandomCode();
        DateTime expiresAt = DateTime.UtcNow.AddMinutes(10);
        
        // Check for existing code
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
            // Create new verification code
            await _context.EmailVerificationCodes.AddAsync(new EmailVerificationCode
            {
                Email = email,
                Code = verificationCode,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsUsed = false
            });
        }
        
        await _context.SaveChangesAsync();
        return verificationCode;
    }
    
    // Helper method to find and validate a verification code
    private async Task<(bool Valid, string Message, EmailVerificationCode? Code)> ValidateCodeAsync(string email, string code, bool markAsUsed = false)
    {
        try
        {
            // Find the verification code
            var verificationCode = await _context.EmailVerificationCodes
                .FirstOrDefaultAsync(c => 
                    c.Email.ToLower() == email.ToLower() && 
                    c.Code == code && 
                    !c.IsUsed);
            
            if (verificationCode == null)
            {
                return (false, "Mã xác nhận không chính xác", null);
            }
            
            // Check if code has expired
            if (verificationCode.ExpiresAt < DateTime.UtcNow)
            {
                return (false, "Mã xác nhận đã hết hạn", null);
            }
            
            // Mark the code as used if requested
            if (markAsUsed)
            {
                verificationCode.IsUsed = true;
                await _context.SaveChangesAsync();
            }
            
            return (true, "Xác thực thành công", verificationCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating code for {Email}", email);
            return (false, "Đã xảy ra lỗi khi xác thực mã", null);
        }
    }
    
    // Verify the code provided by the user
    public async Task<(bool Success, string Message)> VerifyCodeAsync(string email, string code)
    {
        var (valid, message, _) = await ValidateCodeAsync(email, code, markAsUsed: true);
        return (valid, message);
    }
      // Send password reset code to user's email
    public async Task<(bool Success, string Message)> SendPasswordResetCodeAsync(string email)
    {
        try
        {
            // Validate email format
            if (!IsValidEmailFormat(email))
            {
                return (false, "Email không hợp lệ, vui lòng kiểm tra lại");
            }
            
            // Check if email exists in database
            if (!await _context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower()))
            {
                return (false, "Email không tồn tại trong hệ thống");
            }
            
            // Generate and save verification code
            string verificationCode = await SaveVerificationCodeAsync(email);
            
            // Send password reset email using template
            string subject = "Đặt lại mật khẩu - SocialApp";
            string emailBody = _emailService.GeneratePasswordResetEmailTemplate(verificationCode);
            
            bool emailSent = await _emailService.SendHtmlEmailAsync(email, subject, emailBody);
            
            if (!emailSent)
            {
                return (false, "Không thể gửi email xác nhận. Vui lòng thử lại sau.");
            }
            
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
        var (valid, message, _) = await ValidateCodeAsync(email, code);
        if (valid)
        {
            return (true, "Xác thực thành công. Bạn có thể đặt lại mật khẩu.");
        }
        return (false, message);
    }

    // Reset user's password after verification
    public async Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordDTO resetPasswordDto)
    {
        try
        {
            // Verify the code and get the verification code object
            var (valid, message, codeObject) = await ValidateCodeAsync(
                resetPasswordDto.Email, 
                resetPasswordDto.Code);
                
            if (!valid)
            {
                return (false, message);
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
            if (codeObject != null)
            {
                codeObject.IsUsed = true;
                await _context.SaveChangesAsync();
            }
            
            return (true, "Đặt lại mật khẩu thành công");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for {Email}", resetPasswordDto.Email);
            return (false, "Đã xảy ra lỗi khi đặt lại mật khẩu. Vui lòng thử lại sau.");
        }
    }
}
