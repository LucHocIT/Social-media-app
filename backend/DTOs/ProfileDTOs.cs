using System.ComponentModel.DataAnnotations;

namespace SocialApp.DTOs;

public class ProfileDTO
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Bio { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public string Role { get; set; } = "User";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastActive { get; set; }
    public int PostCount { get; set; }
    public int FollowersCount { get; set; }
    public int FollowingCount { get; set; }
    public bool IsFollowedByCurrentUser { get; set; }
}

public class UpdateProfileDTO
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [StringLength(50)]
    public string? FirstName { get; set; }

    [StringLength(50)]
    public string? LastName { get; set; }

    [StringLength(500)]
    public string? Bio { get; set; }
}

public class ProfilePictureDTO
{
    [Required]
    public string PictureUrl { get; set; } = null!;
}

public class ProfileSearchDTO
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string SearchTerm { get; set; } = null!;

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}