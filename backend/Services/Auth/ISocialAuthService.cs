using SocialApp.DTOs;

namespace SocialApp.Services.Auth;

public interface ISocialAuthService
{
    Task<UserInfoFromSocialProvider> GetFacebookUserInfoAsync(string accessToken);
    Task<UserInfoFromSocialProvider> GetGoogleUserInfoAsync(string accessToken);
}
