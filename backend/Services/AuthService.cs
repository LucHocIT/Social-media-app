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
    }

    public async Task<AuthResponseDTO> RegisterAsync(RegisterUserDTO registerDto)
    {
        // Kiểm tra xem username hoặc email đã tồn tại chưa
        if (await _context.Users.AnyAsync(u => u.Username == registerDto.Username))
            throw new Exception("Username đã tồn tại");

        if (await _context.Users.AnyAsync(u => u.Email == registerDto.Email))
            throw new Exception("Email đã tồn tại");

        // Verify if the email is valid and exists
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

        // Tạo người dùng mới
        var newUser = new User
        {
            Username = registerDto.Username,
            Email = registerDto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
            FirstName = registerDto.FirstName,
            LastName = registerDto.LastName,
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

    public async Task<AuthResponseDTO> LoginAsync(LoginUserDTO loginDto)
    {
        // Tìm kiếm người dùng theo username
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == loginDto.Username);
        
        // Kiểm tra người dùng tồn tại và mật khẩu khớp
        if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            throw new Exception("Username hoặc mật khẩu không chính xác");

        // Cập nhật thời gian hoạt động cuối
        user.LastActive = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Tạo token và trả về response
        var token = GenerateJwtToken(user);
        
        return new AuthResponseDTO
        {
            Token = token,
            User = MapUserToUserResponseDto(user)
        };
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
                new Claim(ClaimTypes.Name, user.Username)
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
            CreatedAt = user.CreatedAt,
            LastActive = user.LastActive
        };
    }
}
