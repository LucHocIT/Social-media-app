using SocialApp.DTOs;
using SocialApp.Services.Utils;
using Microsoft.AspNetCore.Http;

namespace SocialApp.Services.User;

public interface IProfileService
{
    Task<ProfileDTO?> GetUserProfileAsync(int userId);
    Task<ProfileDTO?> GetUserProfileByUsernameAsync(string username);    
    Task<bool> UpdateUserProfileAsync(int userId, UpdateProfileDTO profileDto);
    Task<bool> UpdateProfilePictureAsync(int userId, string? pictureUrl);
    Task<IEnumerable<ProfileDTO>> SearchProfilesAsync(string searchTerm, int pageNumber = 1, int pageSize = 10);
    Task<IEnumerable<ProfileDTO>> SearchFriendsAsync(string searchTerm, int currentUserId, int pageNumber = 1, int pageSize = 10);
    Task<bool> IsUsernameUniqueAsync(string username, int? currentUserId = null);
    Task<bool> IsEmailUniqueAsync(string email, int? currentUserId = null);    // Follower-related methods
    Task<IEnumerable<ProfileDTO>> GetUserFollowersAsync(int userId, int page = 1, int pageSize = 10);
    Task<IEnumerable<ProfileDTO>> GetUserFollowingAsync(int userId, int page = 1, int pageSize = 10);
    Task<bool> FollowUserAsync(int followerId, int followingId);
    Task<bool> UnfollowUserAsync(int followerId, int followingId);
    Task<bool> IsFollowingAsync(int followerId, int followingId);
    
    // Cloudinary-related methods
    Task<UploadProfilePictureResult> UploadProfilePictureAsync(int userId, IFormFile profilePicture);
    Task<UploadProfilePictureResult> UploadCroppedProfilePictureAsync(int userId, IFormFile profilePicture, string cropData);
    Task<bool> RemoveProfilePictureAsync(int userId);
    Task<bool> UpdateProfilePictureWithUrlAsync(int userId, ProfilePictureDTO pictureDto);
    Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
}