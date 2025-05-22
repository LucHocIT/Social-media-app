using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SocialApp.DTOs;

public class CreatePostDTO
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Content { get; set; } = null!;

    public string? MediaUrl { get; set; }
}

public class UpdatePostDTO
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Content { get; set; } = null!;

    public string? MediaUrl { get; set; }
}

public class PostResponseDTO
{
    public int Id { get; set; }
    public string Content { get; set; } = null!;
    public string? MediaUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = null!;
    public string? ProfilePictureUrl { get; set; }
    public int LikesCount { get; set; }
    public int CommentsCount { get; set; }
    public bool IsLikedByCurrentUser { get; set; }
}

public class PostPagedResponseDTO
{
    public List<PostResponseDTO> Posts { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}

public class PostFilterDTO
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Username { get; set; }
    public bool? OnlyFollowing { get; set; }
}
