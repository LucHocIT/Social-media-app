using SocialApp.DTOs;

namespace SocialApp.Services.User;

public interface IProfileService
{
    Task<ProfileDTO?> GetUserProfileAsync(int userId);
    Task<ProfileDTO?> GetUserProfileByUsernameAsync(string username);
    Task<bool> UpdateUserProfileAsync(int userId, UpdateProfileDTO profileDto);
    Task<bool> UpdateProfilePictureAsync(int userId, string pictureUrl);
    Task<IEnumerable<ProfileDTO>> SearchProfilesAsync(string searchTerm, int pageNumber = 1, int pageSize = 10);
    Task<bool> IsUsernameUniqueAsync(string username, int? currentUserId = null);
    Task<bool> IsEmailUniqueAsync(string email, int? currentUserId = null);
}