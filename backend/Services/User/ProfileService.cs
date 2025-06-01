using Microsoft.EntityFrameworkCore;
using SocialApp.DTOs;
using SocialApp.Models;
using SocialApp.Services.Utils;

namespace SocialApp.Services.User;

public partial class ProfileService : IProfileService
{
    private readonly SocialMediaDbContext _context;
    private readonly ILogger<ProfileService> _logger;
    private readonly ICloudinaryService _cloudinaryService;

    public ProfileService(
        SocialMediaDbContext context,
        ILogger<ProfileService> logger,
        ICloudinaryService cloudinaryService)
    {
        _context = context;
        _logger = logger;
        _cloudinaryService = cloudinaryService;
    }

    public async Task<ProfileDTO?> GetUserProfileAsync(int userId)
    {
        try
        {
            var user = await _context.Users
                .Where(u => u.Id == userId && !u.IsDeleted)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found", userId);
                return null;
            }

            return await CreateProfileDTOAsync(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving profile for user {UserId}", userId);
            return null;
        }
    }

    public async Task<ProfileDTO?> GetUserProfileByUsernameAsync(string username)
    {
        try
        {
            var user = await _context.Users
                .Where(u => u.Username == username && !u.IsDeleted)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("User with username {Username} not found", username);
                return null;
            }

            return await CreateProfileDTOAsync(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving profile for username {Username}", username);
            return null;
        }
    }

    public async Task<bool> UpdateUserProfileAsync(int userId, UpdateProfileDTO profileDto)
    {
        try
        {
            var user = await _context.Users
                .Where(u => u.Id == userId && !u.IsDeleted)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("UpdateUserProfileAsync failed: User with ID {UserId} not found", userId);
                return false;
            }

            // Check if username is already taken by another user
            if (user.Username != profileDto.Username)
            {
                if (!await IsUsernameUniqueAsync(profileDto.Username, userId))
                {
                    _logger.LogWarning("UpdateUserProfileAsync failed: Username {Username} already taken", profileDto.Username);
                    return false;
                }
            }

            // Check if email is already taken by another user
            if (user.Email != profileDto.Email)
            {
                if (!await IsEmailUniqueAsync(profileDto.Email, userId))
                {
                    _logger.LogWarning("UpdateUserProfileAsync failed: Email {Email} already taken", profileDto.Email);
                    return false;
                }
            }

            // Update user profile
            user.Username = profileDto.Username;
            user.Email = profileDto.Email;
            user.FirstName = profileDto.FirstName;
            user.LastName = profileDto.LastName;
            user.Bio = profileDto.Bio;
            user.LastActive = DateTime.Now;

            await _context.SaveChangesAsync();
            _logger.LogInformation("User {UserId} profile updated", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for user {UserId}", userId);
            return false;
        }
    }    public async Task<bool> UpdateProfilePictureAsync(int userId, string? pictureUrl)
    {
        try
        {
            var user = await _context.Users
                .Where(u => u.Id == userId && !u.IsDeleted)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("UpdateProfilePictureAsync failed: User with ID {UserId} not found", userId);
                return false;
            }

            user.ProfilePictureUrl = pictureUrl;
            user.LastActive = DateTime.Now;

            await _context.SaveChangesAsync();
            _logger.LogInformation("User {UserId} profile picture updated", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile picture for user {UserId}", userId);
            return false;
        }
    }

    public async Task<IEnumerable<ProfileDTO>> SearchProfilesAsync(string searchTerm, int pageNumber = 1, int pageSize = 10)
    {
        try
        {
            var query = _context.Users
                .Where(u => !u.IsDeleted &&
                           (u.Username.Contains(searchTerm) ||
                            (u.FirstName != null && u.FirstName.Contains(searchTerm)) ||
                            (u.LastName != null && u.LastName.Contains(searchTerm))));

            var users = await query
                .OrderBy(u => u.Username)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var profileDtos = new List<ProfileDTO>();

            foreach (var user in users)
            {
                profileDtos.Add(await CreateProfileDTOAsync(user));
            }

            return profileDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for profiles with search term {SearchTerm}", searchTerm);
            return Enumerable.Empty<ProfileDTO>();
        }
    }    // Tìm kiếm chỉ những người dùng là bạn bè (cho chat)
    public async Task<IEnumerable<ProfileDTO>> SearchFriendsAsync(string searchTerm, int currentUserId, int pageNumber = 1, int pageSize = 10)
    {
        try
        {
            // Tìm danh sách bạn bè (follow 2 chiều)
            var friendsQuery = from user in _context.Users
                              where !user.IsDeleted &&
                                    user.Id != currentUserId &&
                                    (user.Username.Contains(searchTerm) ||
                                     (user.FirstName != null && user.FirstName.Contains(searchTerm)) ||
                                     (user.LastName != null && user.LastName.Contains(searchTerm))) &&
                                    // Kiểm tra follow 2 chiều
                                    _context.UserFollowers.Any(f1 => f1.FollowerId == currentUserId && f1.FollowingId == user.Id) &&
                                    _context.UserFollowers.Any(f2 => f2.FollowerId == user.Id && f2.FollowingId == currentUserId)
                              select user;

            var users = await friendsQuery
                .OrderBy(u => u.Username)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var profileDtos = new List<ProfileDTO>();

            foreach (var user in users)
            {
                profileDtos.Add(await CreateProfileDTOAsync(user));
            }

            return profileDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for friends with search term {SearchTerm} for user {UserId}", searchTerm, currentUserId);
            return Enumerable.Empty<ProfileDTO>();
        }
    }

    public async Task<bool> IsUsernameUniqueAsync(string username, int? currentUserId = null)
    {
        var query = _context.Users.Where(u => u.Username == username && !u.IsDeleted);

        if (currentUserId.HasValue)
        {
            query = query.Where(u => u.Id != currentUserId.Value);
        }

        return !await query.AnyAsync();
    }    public async Task<bool> IsEmailUniqueAsync(string email, int? currentUserId = null)
    {
        var query = _context.Users.Where(u => u.Email == email && !u.IsDeleted);

        if (currentUserId.HasValue)
        {
            query = query.Where(u => u.Id != currentUserId.Value);
        }

        return !await query.AnyAsync();
    }
        
    // Implement follower-related methods
    public async Task<IEnumerable<ProfileDTO>> GetUserFollowersAsync(int userId, int page = 1, int pageSize = 10)
    {
        try
        {            var followers = await _context.UserFollowers
                .Where(f => f.FollowingId == userId)
                .OrderBy(f => f.FollowerId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(f => f.Follower)
                .Where(u => !u.IsDeleted)
                .ToListAsync();

            var profileDtos = new List<ProfileDTO>();
            foreach (var follower in followers)
            {
                var profileDto = await CreateProfileDTOAsync(follower);
                profileDtos.Add(profileDto);
            }

            return profileDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving followers for user {UserId}", userId);
            return Enumerable.Empty<ProfileDTO>();
        }
    }
    
    public async Task<IEnumerable<ProfileDTO>> GetUserFollowingAsync(int userId, int page = 1, int pageSize = 10)
    {
        try
        {            var following = await _context.UserFollowers
                .Where(f => f.FollowerId == userId)
                .OrderBy(f => f.FollowingId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(f => f.Following)
                .Where(u => !u.IsDeleted)
                .ToListAsync();

            var profileDtos = new List<ProfileDTO>();
            foreach (var followee in following)
            {
                var profileDto = await CreateProfileDTOAsync(followee);
                profileDtos.Add(profileDto);
            }

            return profileDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving following for user {UserId}", userId);
            return Enumerable.Empty<ProfileDTO>();
        }
    }    public async Task<bool> FollowUserAsync(int followerId, int followingId)
    {
        try
        {
            // Check if both users exist
            var follower = await _context.Users.FindAsync(followerId);
            var following = await _context.Users.FindAsync(followingId);

            if (follower == null || following == null || follower.IsDeleted || following.IsDeleted)
            {
                _logger.LogWarning("One or both users in follow relationship do not exist: {FollowerId} -> {FollowingId}", followerId, followingId);
                return false;
            }

            // Check if already following
            var existingFollow = await _context.UserFollowers
                .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FollowingId == followingId);

            if (existingFollow != null)
            {
                // Already following
                return true;
            }            // Create new follow relationship
            _context.UserFollowers.Add(new UserFollower
            {
                FollowerId = followerId,
                FollowingId = followingId,
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error following user {FollowingId} by user {FollowerId}", followingId, followerId);
            return false;
        }
    }    public async Task<bool> UnfollowUserAsync(int followerId, int followingId)
    {
        try
        {
            var followRelationship = await _context.UserFollowers
                .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FollowingId == followingId);

            if (followRelationship == null)
            {
                // Not following
                return true;
            }

            _context.UserFollowers.Remove(followRelationship);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unfollowing user {FollowingId} by user {FollowerId}", followingId, followerId);
            return false;
        }
    }
    
    public async Task<bool> IsFollowingAsync(int followerId, int followingId)
    {
        try
        {
            return await _context.UserFollowers
                .AnyAsync(f => f.FollowerId == followerId && f.FollowingId == followingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {FollowerId} is following user {FollowingId}", followerId, followingId);
            return false;
        }
    }

    private async Task<ProfileDTO> CreateProfileDTOAsync(Models.User user)
    {
        var postCount = await _context.Posts.CountAsync(p => p.UserId == user.Id);
        var followersCount = await _context.UserFollowers.CountAsync(uf => uf.FollowingId == user.Id);
        var followingCount = await _context.UserFollowers.CountAsync(uf => uf.FollowerId == user.Id);

        return new ProfileDTO
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Bio = user.Bio,
            ProfilePictureUrl = user.ProfilePictureUrl,
            Role = user.Role,
            CreatedAt = user.CreatedAt,
            LastActive = user.LastActive,
            PostCount = postCount,
            FollowersCount = followersCount,
            FollowingCount = followingCount,
            IsFollowedByCurrentUser = false // This will be set by the controller based on the current user
        };
    }
}
