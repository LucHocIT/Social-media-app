using System.ComponentModel.DataAnnotations;

namespace SocialApp.DTOs;

public class SocialLoginDTO
{
    [Required]
    public string Provider { get; set; } = string.Empty; // "facebook" or "google"

    [Required]
    public string AccessToken { get; set; } = string.Empty;
}

public class UserInfoFromSocialProvider
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhotoUrl { get; set; }
}

public class FacebookUserInfoDTO
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string First_name { get; set; } = string.Empty;
    public string Last_name { get; set; } = string.Empty;
    public FacebookPicture? Picture { get; set; }
}

public class FacebookPicture
{
    public FacebookPictureData? Data { get; set; }
}

public class FacebookPictureData
{
    public string Url { get; set; } = string.Empty;
}

public class GoogleUserInfoDTO
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Given_name { get; set; } = string.Empty;
    public string Family_name { get; set; } = string.Empty;
    public string Picture { get; set; } = string.Empty;
}
