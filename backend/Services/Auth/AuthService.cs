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
    }

    // Delegate to UserAccountService
    public Task<bool> EmailExistsAsync(string email)
    {
        return _userAccountService.EmailExistsAsync(email);
    }    public Task<AuthResponseDTO> RegisterAsync(RegisterUserDTO registerDto)
    {
        return _userAccountService.RegisterAsync(registerDto);
    }
    
    public Task<(AuthResponseDTO? Result, bool Success, string? ErrorMessage)> LoginAsync(LoginUserDTO loginDto)
    {
        return _userAccountService.LoginAsync(loginDto);
    }

    public string GenerateJwtToken(User user)
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
    }

    public Task<bool> SoftDeleteUserAsync(int userId)
    {
        return _userManagementService.SoftDeleteUserAsync(userId);
    }

    public Task<bool> RestoreUserAsync(int userId)
    {
        return _userManagementService.RestoreUserAsync(userId);
    }
            Role = "User", // Default role is User
            IsDeleted = false, 
            CreatedAt = DateTime.UtcNow,
            LastActive = DateTime.UtcNow
        };

        // Thêm người dùng vào database
        await _context.Users.AddAsync(newUser);
        await _context.SaveChangesAsync();

        // Tạo token và trả về response
        var token = GenerateJwtToken(newUser);
        
        return new AuthResponseDTO
        {
            Token = token,
            User = MapUserToUserResponseDto(newUser)
        };
    }    public async Task<(AuthResponseDTO? Result, bool Success, string? ErrorMessage)> LoginAsync(LoginUserDTO loginDto)
    {
        // Tìm kiếm người dùng theo username
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == loginDto.Username);
        
        // Kiểm tra người dùng tồn tại và mật khẩu khớp
        if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
        {
            _logger.LogInformation("Đăng nhập thất bại cho username: {Username} - thông tin đăng nhập không chính xác", loginDto.Username);
            return (null, false, "Username hoặc mật khẩu không chính xác");
        }

        // Check if user is deleted
        if (user.IsDeleted)
        {
            _logger.LogInformation("Đăng nhập thất bại cho username: {Username} - tài khoản đã bị xóa", loginDto.Username);
            return (null, false, "Tài khoản đã bị xóa");
        }

        // Cập nhật thời gian hoạt động cuối
        user.LastActive = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Tạo token và trả về response
        var token = GenerateJwtToken(user);
        
        _logger.LogInformation("Đăng nhập thành công cho username: {Username}", user.Username);
        
        return (new AuthResponseDTO
        {
            Token = token,
            User = MapUserToUserResponseDto(user)
        }, true, null);
    }

    public string GenerateJwtToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? throw new Exception("Jwt Key is not configured"));
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            ),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"]
        };
        
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }    private static UserResponseDTO MapUserToUserResponseDto(User user)
    {
        return new UserResponseDTO
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Bio = user.Bio,
            ProfilePictureUrl = user.ProfilePictureUrl,
            Role = user.Role,
            IsDeleted = user.IsDeleted,
            CreatedAt = user.CreatedAt,
            LastActive = user.LastActive
        };
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
    
    // Helper method to generate a random 6-digit code
    private string GenerateRandomCode()
    {
        // Generate a 6-digit random code
        Random random = new Random();
        return random.Next(100000, 999999).ToString();
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
            if (await EmailExistsAsync(email))
            {
                _logger.LogWarning("Email already exists: {Email}", email);
                return (false, "Email đã được đăng ký");
            }
            
            // Verify if the email is valid and exists with a short timeout
            var emailVerification = await _emailVerificationService.VerifyEmailAsync(email);
            
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

    // New methods for user role management
    public async Task<bool> SetUserRoleAsync(int userId, string role)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("SetUserRoleAsync failed: User with ID {UserId} not found", userId);
            return false;
        }

        // Allow only valid roles
        if (role != "Admin" && role != "User")
        {
            _logger.LogWarning("SetUserRoleAsync failed: Invalid role {Role} for user {UserId}", role, userId);
            return false;
        }

        user.Role = role;
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("User {UserId} role set to {Role}", userId, role);
        return true;
    }

    public async Task<bool> SoftDeleteUserAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("SoftDeleteUserAsync failed: User with ID {UserId} not found", userId);
            return false;
        }

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("User {UserId} soft deleted", userId);
        return true;
    }

    public async Task<bool> RestoreUserAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("RestoreUserAsync failed: User with ID {UserId} not found", userId);
            return false;
        }

        user.IsDeleted = false;
        user.DeletedAt = null;
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("User {UserId} restored", userId);
        return true;
    }

    public async Task<UserResponseDTO?> GetUserByIdAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("GetUserByIdAsync: User with ID {UserId} not found", userId);
            return null;
        }

        return MapUserToUserResponseDto(user);
    }

    public async Task<AuthResponseDTO> RegisterVerifiedUserAsync(VerifiedRegisterDTO registerDto)
    {
        try
        {
            // Check if email verification code exists and is verified
            var verificationCode = await _context.EmailVerificationCodes
                .FirstOrDefaultAsync(c => 
                    c.Email.ToLower() == registerDto.Email.ToLower() && 
                    c.IsUsed == true);
                
            if (verificationCode == null)
            {
                throw new Exception("Email chưa được xác thực. Vui lòng xác thực email trước khi đăng ký.");
            }
            
            // Log start of registration process
            _logger.LogInformation("Starting registration process for verified email: {Username}, email: {Email}", 
                registerDto.Username, registerDto.Email);
                
            // Check if username already exists
            if (await _context.Users.AnyAsync(u => u.Username == registerDto.Username))
                throw new Exception("Username đã tồn tại");

            // Check if email already exists (shouldn't happen but good to check)
            if (await EmailExistsAsync(registerDto.Email))
                throw new Exception("Email đã tồn tại");

            // Create new user
            var newUser = new User
            {
                Username = registerDto.Username,
                Email = registerDto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                Role = "User", // Default role is User
                IsDeleted = false, 
                CreatedAt = DateTime.UtcNow,
                LastActive = DateTime.UtcNow
            };

            // Add user to database
            await _context.Users.AddAsync(newUser);
            await _context.SaveChangesAsync();

            // Generate token and return response
            var token = GenerateJwtToken(newUser);
            
            return new AuthResponseDTO
            {
                Token = token,
                User = MapUserToUserResponseDto(newUser)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during verified registration for: {Username}, {Email}", 
                registerDto.Username, registerDto.Email);
            throw; // Rethrow to be handled by the controller
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
            if (!await EmailExistsAsync(email))
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
