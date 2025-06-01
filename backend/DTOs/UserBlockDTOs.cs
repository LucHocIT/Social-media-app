using System.ComponentModel.DataAnnotations;

namespace SocialApp.DTOs;

public class BlockUserRequestDto
{
    [Required]
    public int BlockedUserId { get; set; }
    
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string? Reason { get; set; }
}

public class UnblockUserRequestDto
{
    [Required]
    public int BlockedUserId { get; set; }
}

public class BlockedUserDto
{
    public int Id { get; set; }
    public int BlockedUserId { get; set; }
    public string BlockedUserName { get; set; } = string.Empty;
    public string? BlockedUserAvatar { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BlockStatusDto
{
    public bool IsBlocked { get; set; }
    public bool IsBlockedBy { get; set; }
    public DateTime? BlockedAt { get; set; }
    public string? Reason { get; set; }
}

public class BlockedUsersListDto
{
    public List<BlockedUserDto> BlockedUsers { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}
