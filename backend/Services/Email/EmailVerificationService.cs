using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SocialApp.DTOs;
using SocialApp.Models;
using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SocialApp.Services.Email;

public class EmailVerificationService : IEmailVerificationService
{
    private readonly SocialMediaDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailVerificationService> _logger;
    private readonly IEmailService _emailService;
    private readonly bool _isDevelopment;    public EmailVerificationService(
        SocialMediaDbContext context,
        IConfiguration configuration,
        ILogger<EmailVerificationService> logger,
        IEmailService emailService)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _emailService = emailService;
        
        // Determine environment
        _isDevelopment = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";
        
        // Diagnostic log to check environment variables
        DiagnoseEnvironmentVariables();
    }
    
    // Diagnostic method to check environment variables
    private void DiagnoseEnvironmentVariables()
    {
        try
        {
            // List all environment variables
            var apiKeyEnv = Environment.GetEnvironmentVariable("EMAIL_VERIFICATION_API_KEY");
            var apiKeyConfig = _configuration["EmailVerification:ApiKey"];
            
            _logger.LogInformation("Environment Variable Diagnosis:");
            _logger.LogInformation("EMAIL_VERIFICATION_API_KEY from Environment: {Value}",
                string.IsNullOrEmpty(apiKeyEnv) ? "Not set" : "Set (length: " + apiKeyEnv.Length + ")");
            _logger.LogInformation("EmailVerification:ApiKey from Configuration: {Value}",
                string.IsNullOrEmpty(apiKeyConfig) ? "Not set" : 
                (apiKeyConfig == "[EMAIL_VERIFICATION_API_KEY]" ? "Placeholder value" : "Set (length: " + apiKeyConfig.Length + ")"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DiagnoseEnvironmentVariables");
        }
    }

    // Helper method to generate a random 6-digit code
    private string GenerateRandomCode()
    {
        // Generate a 6-digit random code
        Random random = new Random();
        return random.Next(100000, 999999).ToString();
    }    // Helper method to check if email format is valid
    private bool IsValidEmailFormat(string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            // Basic validation with MailAddress
            var addr = new System.Net.Mail.MailAddress(email);
            if (addr.Address != email)
                return false;

            // Check if domain part exists and has at least one dot
            var parts = email.Split('@');
            if (parts.Length != 2) 
                return false;
                
            var domain = parts[1];
            if (!domain.Contains('.') || domain.EndsWith('.'))
                return false;
                
            // Check for minimum lengths
            if (parts[0].Length < 1 || domain.Length < 3)
                return false;
                
            return true;
        }
        catch
        {
            return false;
        }
    }    // Implementation of email verification method
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
            // Get API Key with more reliable approach
            string apiKey = null;
            
            // Try from environment variables first - process level
            apiKey = Environment.GetEnvironmentVariable("EMAIL_VERIFICATION_API_KEY", EnvironmentVariableTarget.Process);
            if (!string.IsNullOrEmpty(apiKey))
            {
                _logger.LogInformation("Using API key from process environment variable");
            }
            else
            {
                // Try from user environment variables
                apiKey = Environment.GetEnvironmentVariable("EMAIL_VERIFICATION_API_KEY", EnvironmentVariableTarget.User);
                if (!string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogInformation("Using API key from user environment variable");
                }
                else
                {
                    // Try from machine environment variables
                    apiKey = Environment.GetEnvironmentVariable("EMAIL_VERIFICATION_API_KEY", EnvironmentVariableTarget.Machine);
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        _logger.LogInformation("Using API key from machine environment variable");
                    }
                    else
                    {
                        // Finally try from configuration
                        apiKey = _configuration["EmailVerification:ApiKey"];
                        
                        // Check if it's a placeholder value
                        if (string.IsNullOrEmpty(apiKey) || apiKey == "[EMAIL_VERIFICATION_API_KEY]")
                        {
                            _logger.LogInformation("EmailVerification API key not found - using fallback validation");
                            // Fallback to basic validation if API key is missing
                            return await FallbackEmailValidationAsync(email);
                        }
                        else
                        {
                            _logger.LogInformation("Using email verification API key from configuration");
                        }
                    }
                }
            }
              // Create HTTP client for email verification service
            using (var httpClient = new HttpClient())
            {
                // Log source of API key
                string apiKeySource = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EMAIL_VERIFICATION_API_KEY")) 
                    ? "configuration" : "environment variable";
                _logger.LogDebug("Using API key from {Source}", apiKeySource);
                
                // Use a real email verification API service - Abstract API for email validation
                string endpoint = $"https://emailvalidation.abstractapi.com/v1/?api_key={apiKey}&email={Uri.EscapeDataString(email)}";
                
                var response = await httpClient.GetAsync(endpoint);
                  if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug("API Response: {Content}", content);
                    
                    try
                    {
                        // Parse response from Abstract API format
                        var options = new System.Text.Json.JsonDocumentOptions 
                        { 
                            AllowTrailingCommas = true 
                        };
                        
                        using (var document = System.Text.Json.JsonDocument.Parse(content, options))
                        {
                            // Abstract API returns more detailed info
                            bool isFormatValid = document.RootElement.TryGetProperty("is_valid_format", out var formatElement) && 
                                                formatElement.GetBoolean();
                            bool isFreeEmail = document.RootElement.TryGetProperty("is_free_email", out var freeEmailElement) && 
                                            freeEmailElement.GetBoolean();
                            bool isDisposable = document.RootElement.TryGetProperty("is_disposable_email", out var disposableElement) && 
                                            disposableElement.GetBoolean();
                            bool isMxFound = document.RootElement.TryGetProperty("is_mx_found", out var mxElement) && 
                                            mxElement.GetBoolean();
                            bool isSmtpValid = document.RootElement.TryGetProperty("is_smtp_valid", out var smtpElement) && 
                                            smtpElement.GetBoolean();
                            
                            // Consider an email valid if it has correct format
                            bool isValid = isFormatValid;
                            
                            // Consider an email to exist if it's a free email provider or has valid MX records and SMTP is valid
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating email with API: {Email}", email);
            // Fallback to basic validation if API call throws exception
            return await FallbackEmailValidationAsync(email);
        }
    }
      // Fallback method for email validation when API is unavailable
    private Task<(bool IsValid, bool Exists, string Message)> FallbackEmailValidationAsync(string email)
    {
        _logger.LogInformation("Using fallback email validation for {Email}", email);
        
        // In fallback mode, we'll be more permissive about validation
        // But still check some basic rules
        
        if (!IsValidEmailFormat(email))
        {
            return Task.FromResult((false, false, "Email format is invalid"));
        }
        
        string domain = email.Split('@').LastOrDefault()?.ToLower() ?? string.Empty;
        
        // Check if it's a common email domain
        bool isCommonDomain = !string.IsNullOrEmpty(domain) && (
            domain.Contains("gmail.com") || 
            domain.Contains("yahoo.com") || 
            domain.Contains("outlook.com") ||
            domain.Contains("hotmail.com") ||
            domain.Contains("mail.com") ||
            domain.Contains("icloud.com") ||
            domain.Contains("protonmail.com") ||
            domain.Contains("zoho.com") ||
            domain.Contains("aol.com") ||
            domain.Contains("yandex.com") ||
            domain.Contains("gmx.com") ||
            domain.Contains("tutanota.com")
        );
          // Check for suspicious patterns in local part
        string localPart = email.Split('@').FirstOrDefault() ?? string.Empty;
        bool isSuspicious = localPart.Contains("admin") || 
                           localPart.Contains("info") ||
                           localPart.Contains("support") ||
                           localPart.Contains("test") ||
                           localPart.Contains("noreply") ||
                           Regex.IsMatch(localPart, @"^\d+$") || // Only numbers
                           Regex.IsMatch(localPart, @"\d{6,}") || // Contains 6+ consecutive digits
                           // Check for unusually long local parts with numbers (likely fake)
                           (localPart.Length > 15 && Regex.IsMatch(localPart, @"\d{4,}")) || // Long username with 4+ digits
                           // Check for random-looking patterns (letters followed by many numbers)
                           Regex.IsMatch(localPart, @"^[a-zA-Z]{1,5}\d{8,}$"); // Few letters followed by many digits
        
        // For disposable email detection, check common domains
        bool isLikelyDisposable = domain.Contains("mailinator.com") ||
                                 domain.Contains("tempmail.com") ||
                                 domain.Contains("temp-mail.org") ||
                                 domain.Contains("fakeinbox.com") ||
                                 domain.Contains("guerrillamail.com") ||
                                 domain.Contains("yopmail.com") ||
                                 domain.Contains("sharklasers.com") ||
                                 domain.Contains("10minutemail.com");
          bool likelyExists = isCommonDomain && !isSuspicious && !isLikelyDisposable;
        
        string message = isLikelyDisposable 
            ? "Email appears to be from a disposable email service"
            : isSuspicious
                ? "Email appears to be suspicious or automatically generated"
            : likelyExists 
                ? "Email format is valid and likely exists" 
                : "Email format is valid but may not exist";
        
        _logger.LogDebug("Fallback validation result for {Email}: LikelyExists={LikelyExists}, Message={Message}", 
            email, likelyExists, message);
        
        return Task.FromResult((true, likelyExists, message));
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
              // Verify email format and existence using the API-based verification
            var emailVerification = await VerifyEmailAsync(email);
            
            // Strict email validation based on API Key availability
            string apiKey = Environment.GetEnvironmentVariable("EMAIL_VERIFICATION_API_KEY");
            bool apiKeyAvailable = !string.IsNullOrEmpty(apiKey);
            
            if (!apiKeyAvailable)
            {
                apiKey = _configuration["EmailVerification:ApiKey"];
                apiKeyAvailable = !string.IsNullOrEmpty(apiKey) && apiKey != "[EMAIL_VERIFICATION_API_KEY]";
            }
            
            // If API key is available, enforce strict validation
            if (apiKeyAvailable)
            {
                if (!emailVerification.IsValid)
                {
                    _logger.LogWarning("Invalid email format according to verification API: {Email}", email);
                    return (false, "Email không hợp lệ, vui lòng kiểm tra lại");
                }

                if (!emailVerification.Exists)
                {
                    _logger.LogWarning("Email does not exist according to verification API: {Email}", email);
                    return (false, "Email không tồn tại hoặc không thể gửi thư đến địa chỉ này");
                }
            }            else
            {            // Fallback to basic validation if API key is not available
                _logger.LogInformation("Using basic email validation for: {Email}", email);
                if (!IsValidEmailFormat(email))
                {
                    return (false, "Email không hợp lệ, vui lòng kiểm tra lại");
                }
                  // Check if it might be a disposable email
                string domain = email.Split('@').LastOrDefault()?.ToLower() ?? string.Empty;
                string localPart = email.Split('@').FirstOrDefault() ?? string.Empty;
                
                bool isLikelyDisposable = domain.Contains("mailinator.com") ||
                                        domain.Contains("tempmail.com") ||
                                        domain.Contains("temp-mail.org") ||
                                        domain.Contains("fakeinbox.com") ||
                                        domain.Contains("guerrillamail.com") ||
                                        domain.Contains("yopmail.com") ||
                                        domain.Contains("sharklasers.com") ||
                                        domain.Contains("10minutemail.com");
                
                bool isSuspicious = localPart.Contains("admin") || 
                                   localPart.Contains("info") ||
                                   localPart.Contains("support") ||
                                   localPart.Contains("test") ||
                                   localPart.Contains("noreply") ||
                                   Regex.IsMatch(localPart, @"^\d+$") || // Only numbers
                                   Regex.IsMatch(localPart, @"\d{6,}") || // Contains 6+ consecutive digits
                                   (localPart.Length > 15 && Regex.IsMatch(localPart, @"\d{4,}")) || // Long username with 4+ digits
                                   Regex.IsMatch(localPart, @"^[a-zA-Z]{1,5}\d{8,}$"); // Few letters followed by many digits
                                        
                if (isLikelyDisposable)
                {
                    _logger.LogWarning("Disposable email detected: {Email}", email);
                    return (false, "Không chấp nhận email tạm thời. Vui lòng sử dụng địa chỉ email thật.");
                }
                
                if (isSuspicious)
                {
                    _logger.LogWarning("Suspicious email format detected: {Email}", email);
                    return (false, "Email có định dạng đáng ngờ. Vui lòng sử dụng địa chỉ email thật.");
                }
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
                
                await _context.EmailVerificationCodes.AddAsync(newVerificationCode);            }
            await _context.SaveChangesAsync();
            
            // Generate email content for verification
            string subject = "Xác nhận email đăng ký - SocialApp";
            string emailBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px;'>
                        <h2 style='color: #4a6ee0;'>Xác nhận email đăng ký</h2>
                        <p>Cảm ơn bạn đã đăng ký tài khoản trên SocialApp!</p>
                        <p>Mã xác nhận của bạn là:</p>
                        <div style='background-color: #f5f5f5; padding: 10px; text-align: center; font-size: 24px; font-weight: bold; letter-spacing: 2px; margin: 15px 0;'>
                            {verificationCode}
                        </div>
                        <p>Mã xác nhận này sẽ hết hạn sau 10 phút.</p>
                        <p>Nếu bạn không yêu cầu đăng ký tài khoản, vui lòng bỏ qua email này.</p>
                        <p>Trân trọng,<br>Đội ngũ SocialApp</p>
                    </div>
                </body>
                </html>";
            
            // Send verification email
            bool emailSent = await _emailService.SendHtmlEmailAsync(email, subject, emailBody);
            
            if (!emailSent)
            {
                _logger.LogError("Failed to send verification email to {Email}", email);
                return (false, "Không thể gửi email xác nhận. Vui lòng thử lại sau.");
            }
            
            _logger.LogInformation("Verification email sent to {Email}", email);
              
            // Never display the verification code in messages for security reasons
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
                
                await _context.EmailVerificationCodes.AddAsync(newVerificationCode);            }
            await _context.SaveChangesAsync();
            
            // Generate email content for password reset
            string subject = "Đặt lại mật khẩu - SocialApp";
            string emailBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px;'>
                        <h2 style='color: #4a6ee0;'>Đặt lại mật khẩu</h2>
                        <p>Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản của mình trên SocialApp.</p>
                        <p>Mã xác nhận của bạn là:</p>
                        <div style='background-color: #f5f5f5; padding: 10px; text-align: center; font-size: 24px; font-weight: bold; letter-spacing: 2px; margin: 15px 0;'>
                            {verificationCode}
                        </div>
                        <p>Mã xác nhận này sẽ hết hạn sau 10 phút.</p>
                        <p>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                        <p>Trân trọng,<br>Đội ngũ SocialApp</p>
                    </div>
                </body>
                </html>";
            
            // Send password reset verification email
            bool emailSent = await _emailService.SendHtmlEmailAsync(email, subject, emailBody);
            
            if (!emailSent)
            {
                _logger.LogError("Failed to send password reset email to {Email}", email);
                return (false, "Không thể gửi email xác nhận. Vui lòng thử lại sau.");
            }
            
            _logger.LogInformation("Password reset email sent to {Email}", email);
              
            // Never display the verification code in messages for security reasons
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
