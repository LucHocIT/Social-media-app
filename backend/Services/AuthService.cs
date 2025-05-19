using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Services;

public class AuthService : IAuthService
{
    private readonly SocialMediaDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        SocialMediaDbContext context, 
        IConfiguration configuration,
        IEmailVerificationService emailVerificationService,
        ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _emailVerificationService = emailVerificationService;
        _logger = logger;
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower());
    }    public async Task<AuthResponseDTO> RegisterAsync(RegisterUserDTO registerDto)
    {
        try
        {
            // Log start of registration process
            _logger.LogInformation("Starting registration process for username: {Username}, email: {Email}", 
                registerDto.Username, registerDto.Email);
                
            // Kiểm tra xem username hoặc email đã tồn tại chưa
            if (await _context.Users.AnyAsync(u => u.Username == registerDto.Username))
                throw new Exception("Username đã tồn tại");

            if (await _context.Users.AnyAsync(u => u.Email == registerDto.Email))
                throw new Exception("Email đã tồn tại");

            // Always verify basic email format locally first
            if (!IsValidEmailFormat(registerDto.Email))
            {
                _logger.LogWarning("Invalid email format detected locally: {Email}", registerDto.Email);
                throw new Exception("Email không hợp lệ, vui lòng kiểm tra lại");
            }            // Không bỏ qua việc xác thực email ngay cả khi ở môi trường Development
            // bool skipExternalVerification = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";
            
            // Always verify email regardless of environment
            try
            {
                _logger.LogInformation("Attempting to verify email with external service: {Email}", registerDto.Email);
                // Verify if the email is valid and exists with a short timeout
                var emailVerification = await _emailVerificationService.VerifyEmailAsync(registerDto.Email);
                
                if (!emailVerification.IsValid)
                {
                    _logger.LogWarning("Invalid email format: {Email}", registerDto.Email);
                    throw new Exception("Email không hợp lệ, vui lòng kiểm tra lại");
                }

                if (!emailVerification.Exists)
                {
                    _logger.LogWarning("Email does not exist or is not deliverable: {Email}, Message: {Message}", 
                        registerDto.Email, emailVerification.Message);
                    throw new Exception("Email không tồn tại hoặc không thể gửi thư đến địa chỉ này");
                }
                
                _logger.LogInformation("Email verification successful for: {Email}", registerDto.Email);
            }
            catch (Exception verificationEx)
            {
                // Log the error and throw exception to prevent registration with invalid email
                _logger.LogError(verificationEx, "Email verification service failed for: {Email}", registerDto.Email);
                throw new Exception("Không thể xác minh email. Vui lòng thử lại sau hoặc sử dụng email khác.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during pre-registration checks for: {Username}, {Email}", 
                registerDto.Username, registerDto.Email);
            throw; // Rethrow to be handled by the controller
        }

        // Tạo người dùng mới
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
}
