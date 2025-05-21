using SocialApp.DTOs;

namespace SocialApp.Services.User;

public interface IProfileService
{
    Task<ProfileDTO?> GetUserProfileAsync(int userId);
    Task<ProfileDTO?> GetUserProfileByUsernameAsync(string username);    Task<bool> UpdateUserProfileAsync(int userId, UpdateProfileDTO profileDto);
    Task<bool> UpdateProfilePictureAsync(int userId, string? pictureUrl);
    Task<IEnumerable<ProfileDTO>> SearchProfilesAsync(string searchTerm, int pageNumber = 1, int pageSize = 10);
    Task<bool> IsUsernameUniqueAsync(string username, int? currentUserId = null);
    Task<bool> IsEmailUniqueAsync(string email, int? currentUserId = null);
      // Follower-related methods
    Task<IEnumerable<ProfileDTO>> GetUserFollowersAsync(int userId, int page = 1, int pageSize = 10);
    Task<IEnumerable<ProfileDTO>> GetUserFollowingAsync(int userId, int page = 1, int pageSize = 10);
    Task<bool> FollowUserAsync(int followerId, int followingId);
    Task<bool> UnfollowUserAsync(int followerId, int followingId);
}