using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SocialApp.DTOs;

namespace SocialApp.Services.Auth;

public class SocialAuthService : ISocialAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SocialAuthService> _logger;

    public SocialAuthService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SocialAuthService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<UserInfoFromSocialProvider> GetFacebookUserInfoAsync(string accessToken)
    {
        try
        {
            // Facebook API endpoint to get user info
            var response = await _httpClient.GetAsync(
                $"https://graph.facebook.com/v18.0/me?fields=id,name,email,first_name,last_name,picture&access_token={accessToken}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Facebook API error: {status} {reason}", response.StatusCode, response.ReasonPhrase);
                throw new Exception($"Facebook API error: {response.StatusCode} {response.ReasonPhrase}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var fbUserInfo = JsonSerializer.Deserialize<FacebookUserInfoDTO>(content, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (fbUserInfo == null)
            {
                throw new Exception("Failed to deserialize user info from Facebook");
            }

            return new UserInfoFromSocialProvider
            {
                Id = fbUserInfo.Id,
                Email = fbUserInfo.Email,
                Name = fbUserInfo.Name,
                FirstName = fbUserInfo.First_name,
                LastName = fbUserInfo.Last_name,
                PhotoUrl = fbUserInfo.Picture?.Data?.Url
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user info from Facebook");
            throw;
        }
    }

    public async Task<UserInfoFromSocialProvider> GetGoogleUserInfoAsync(string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            // Google API endpoint to get user info
            var response = await _httpClient.GetAsync("https://www.googleapis.com/oauth2/v3/userinfo");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Google API error: {status} {reason}", response.StatusCode, response.ReasonPhrase);
                throw new Exception($"Google API error: {response.StatusCode} {response.ReasonPhrase}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var googleUserInfo = JsonSerializer.Deserialize<GoogleUserInfoDTO>(content, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (googleUserInfo == null)
            {
                throw new Exception("Failed to deserialize user info from Google");
            }

            return new UserInfoFromSocialProvider
            {
                Id = googleUserInfo.Id,
                Email = googleUserInfo.Email,
                Name = googleUserInfo.Name,
                FirstName = googleUserInfo.Given_name,
                LastName = googleUserInfo.Family_name,
                PhotoUrl = googleUserInfo.Picture
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user info from Google");
            throw;
        }
    }
}