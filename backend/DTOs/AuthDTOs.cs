using System.ComponentModel.DataAnnotations;

namespace SocialApp.DTOs;

public class RegisterUserDTO
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    [StringLength(50)]
    public string? FirstName { get; set; }

    [StringLength(50)]
    public string? LastName { get; set; }
}

public class LoginUserDTO
{
    [Required]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Password { get; set; } = string.Empty;
}

public class UserResponseDTO
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Bio { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public string Role { get; set; } = string.Empty; // Added Role property
    public bool IsDeleted { get; set; } // Added IsDeleted property
    public DateTime CreatedAt { get; set; }
    public DateTime? LastActive { get; set; }
}

public class AuthResponseDTO
{
    public string Token { get; set; } = string.Empty;
    public UserResponseDTO User { get; set; } = null!;
}

public class VerifyEmailDTO
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class SetUserRoleDTO
{
    [Required]
    [StringLength(20)]
    [RegularExpression("^(Admin|User)$", ErrorMessage = "Role must be either 'Admin' or 'User'")]
    public string Role { get; set; } = "User";
}

public class SendVerificationCodeDTO
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class VerifyCodeDTO
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
}

public class RegisterWithVerificationDTO : RegisterUserDTO
{
    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string VerificationCode { get; set; } = string.Empty;
}

public class VerifiedRegisterDTO
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50, MinimumLength = 6)]
    [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [StringLength(50)]
    public string? FirstName { get; set; }

    [StringLength(50)]
    public string? LastName { get; set; }
}

public class ForgotPasswordDTO
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class VerifyResetCodeDTO
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
}

public class ResetPasswordDTO
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50, MinimumLength = 6)]
    public string NewPassword { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50, MinimumLength = 6)]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
