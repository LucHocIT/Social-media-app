using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Services;

public class UserAccountService : IUserAccountService
{
    private readonly SocialMediaDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IEmailVerificationCodeService _verificationCodeService;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly ILogger<UserAccountService> _logger;

    public UserAccountService(
        SocialMediaDbContext context, 
        IConfiguration configuration,
        IEmailVerificationCodeService verificationCodeService,
        IEmailVerificationService emailVerificationService,
        ILogger<UserAccountService> logger)
    {
        _context = context;
        _configuration = configuration;
        _verificationCodeService = verificationCodeService;
        _emailVerificationService = emailVerificationService;
        _logger = logger;
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower());
    }    

    public async Task<AuthResponseDTO> RegisterAsync(RegisterUserDTO registerDto)
    {
        try
        {
            // Check if it's a registration with verification code
            if (registerDto is RegisterWithVerificationDTO registerWithVerificationDto)
            {
                // Verify the code
                var (success, message) = await _verificationCodeService.VerifyCodeAsync(
                    registerWithVerificationDto.Email, 
                    registerWithVerificationDto.VerificationCode);
                    
                if (!success)
                {
                    throw new Exception(message);
                }
            }
            else
            {
                // This is the older path for backwards compatibility
                var isDevelopment = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";
                
                if (!isDevelopment)
                {
                    // In production, always require email verification
                    _logger.LogWarning("Registration attempted without verification code for {Email}", registerDto.Email);
                    throw new Exception("Bạn cần xác thực email trước khi đăng ký");
                }
                else
                {
                    _logger.LogWarning("Allowing registration without verification in development mode for {Email}", registerDto.Email);
                }
            }
            
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
            }
            
            _logger.LogInformation("Email verification successful for: {Email}", registerDto.Email);
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
    }    

    public async Task<(AuthResponseDTO? Result, bool Success, string? ErrorMessage)> LoginAsync(LoginUserDTO loginDto)
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

    // Helper method to map User entity to UserResponseDTO
    private static UserResponseDTO MapUserToUserResponseDto(User user)
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
}
