using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialApp.DTOs;
using SocialApp.Models;
using SocialApp.Services.User;
using System.Security.Claims;
using BCrypt.Net;

namespace SocialApp.Controllers.User;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{    private readonly IProfileService _profileService;
    private readonly ILogger<ProfileController> _logger;
    private readonly SocialMediaDbContext _context;

    public ProfileController(
        IProfileService profileService,
        ILogger<ProfileController> logger,
        SocialMediaDbContext context)
    {
        _profileService = profileService;
        _logger = logger;
        _context = context;
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<ProfileDTO>> GetUserProfile(int userId)
    {
        var profile = await _profileService.GetUserProfileAsync(userId);
        if (profile == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Check if the current user is following this profile
        if (User.Identity?.IsAuthenticated == true)
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            // The following logic would be here - to be implemented in a follow/unfollow feature
        }

        return Ok(profile);
    }

    [HttpGet("username/{username}")]
    public async Task<ActionResult<ProfileDTO>> GetUserProfileByUsername(string username)
    {
        var profile = await _profileService.GetUserProfileByUsernameAsync(username);
        if (profile == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Check if the current user is following this profile
        if (User.Identity?.IsAuthenticated == true)
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            // The following logic would be here - to be implemented in a follow/unfollow feature
        }

        return Ok(profile);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ProfileDTO>> GetMyProfile()
    {
        int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var profile = await _profileService.GetUserProfileAsync(currentUserId);

        if (profile == null)
        {
            return NotFound(new { message = "Profile not found" });
        }

        return Ok(profile);
    }    [HttpPut("update")]
    [HttpPut("")]  // Adding a route alias to support direct PUT to /api/profile
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDTO profileDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }        int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        
        // Check if the user is authorized (admin or the user themselves)
        bool isAdmin = User.IsInRole("Admin");
        // This check is currently redundant since we're updating the current user's profile
        // It will be useful when implementing editing of other user profiles
        
        bool result = await _profileService.UpdateUserProfileAsync(currentUserId, profileDto);
        if (!result)
        {
            return BadRequest(new { message = "Failed to update profile. Username or email may already be in use." });
        }

        return Ok(new { message = "Profile updated successfully" });
    }

    // Add endpoints for followers and following
    [HttpGet("{userId}/followers")]
    public async Task<IActionResult> GetFollowers(int userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var followers = await _profileService.GetUserFollowersAsync(userId, page, pageSize);
            return Ok(followers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching followers for user {UserId}", userId);
            return StatusCode(500, new { message = "Failed to fetch followers" });
        }
    }

    [HttpGet("{userId}/following")]
    public async Task<IActionResult> GetFollowing(int userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var following = await _profileService.GetUserFollowingAsync(userId, page, pageSize);
            return Ok(following);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching following for user {UserId}", userId);
            return StatusCode(500, new { message = "Failed to fetch following" });
        }
    }    [HttpPost("follow/{userId}")]
    [Authorize]
    public async Task<IActionResult> FollowUser(int userId)
    {
        int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        if (userId == currentUserId)
        {
            return BadRequest(new { message = "You cannot follow yourself" });
        }

        try
        {
            bool result = await _profileService.FollowUserAsync(currentUserId, userId);
            if (!result)
            {
                return BadRequest(new { message = "Failed to follow user. User may not exist or you may already be following them." });
            }

            return Ok(new { message = "Successfully followed user" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error following user {TargetUserId} by user {CurrentUserId}", userId, currentUserId);
            return StatusCode(500, new { message = "An error occurred while trying to follow user" });
        }
    }

    [HttpDelete("unfollow/{userId}")]
    [Authorize]
    public async Task<IActionResult> UnfollowUser(int userId)
    {
        int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        if (userId == currentUserId)
        {
            return BadRequest(new { message = "You cannot unfollow yourself" });
        }

        try
        {
            bool result = await _profileService.UnfollowUserAsync(currentUserId, userId);
            if (!result)
            {
                return BadRequest(new { message = "Failed to unfollow user. User may not exist or you may not be following them." });
            }

            return Ok(new { message = "Successfully unfollowed user" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unfollowing user {TargetUserId} by user {CurrentUserId}", userId, currentUserId);
            return StatusCode(500, new { message = "An error occurred while trying to unfollow user" });
        }
    }

    // Admin endpoints
    [HttpPut("admin/update/{userId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminUpdateProfile(int userId, [FromBody] UpdateProfileDTO profileDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        bool result = await _profileService.UpdateUserProfileAsync(userId, profileDto);
        if (!result)
        {
            return BadRequest(new { message = "Failed to update profile. User not found or username/email already in use." });
        }

        return Ok(new { message = "Profile updated successfully by admin" });
    }

    [HttpPut("picture")]
    [Authorize]
    public async Task<IActionResult> UpdateProfilePicture([FromBody] ProfilePictureDTO pictureDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

        bool result = await _profileService.UpdateProfilePictureAsync(currentUserId, pictureDto.PictureUrl);
        if (!result)
        {
            return BadRequest(new { message = "Failed to update profile picture" });
        }

        return Ok(new { message = "Profile picture updated successfully" });
    }

    [HttpPost("picture")]
    [Authorize]
    public async Task<IActionResult> UploadProfilePicture(IFormFile profilePicture)
    {
        if (profilePicture == null)
        {
            return BadRequest(new { message = "No file uploaded" });
        }

        try
        {
            // Validate file type
            string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
            string fileExtension = Path.GetExtension(profilePicture.FileName).ToLower();
            
            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(new { message = "Only jpg, jpeg, png, and gif files are allowed" });
            }

            // Generate a unique filename
            string fileName = Guid.NewGuid().ToString() + fileExtension;
            
            // Set the path where to save the file
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
            
            // Create directory if it doesn't exist
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }
            
            string filePath = Path.Combine(uploadsFolder, fileName);
            
            // Save the file
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await profilePicture.CopyToAsync(fileStream);
            }
            
            // Generate URL for the image
            string imageUrl = $"/uploads/profiles/{fileName}";
            
            // Update user profile with new image URL
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            bool result = await _profileService.UpdateProfilePictureAsync(currentUserId, imageUrl);
            
            if (!result)
            {
                // If update failed, delete the uploaded file
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
                
                return BadRequest(new { message = "Failed to update profile picture" });
            }
            
            return Ok(new { profilePictureUrl = imageUrl, message = "Profile picture uploaded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading profile picture for user");
            return StatusCode(500, new { message = "An error occurred while uploading the file" });
        }
    }

    [HttpDelete("picture")]
    [Authorize]
    public async Task<IActionResult> RemoveProfilePicture()
    {
        int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

        bool result = await _profileService.UpdateProfilePictureAsync(currentUserId, null);
        if (!result)
        {
            return BadRequest(new { message = "Failed to remove profile picture" });
        }

        return Ok(new { message = "Profile picture removed successfully" });
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<ProfileDTO>>> SearchProfiles([FromQuery] ProfileSearchDTO searchDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var profiles = await _profileService.SearchProfilesAsync(
            searchDto.SearchTerm,
            searchDto.PageNumber,
            searchDto.PageSize);

        // Check if the current user is following each profile
        if (User.Identity?.IsAuthenticated == true)
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            // The following logic would be here - to be implemented in a follow/unfollow feature
        }

        return Ok(profiles);
    }

    [HttpGet("check-username")]
    public async Task<IActionResult> CheckUsernameAvailability([FromQuery] string username)
    {
        int? currentUserId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        }

        bool isAvailable = await _profileService.IsUsernameUniqueAsync(username, currentUserId);
        return Ok(new { isAvailable });
    }

    [HttpGet("check-email")]
    public async Task<IActionResult> CheckEmailAvailability([FromQuery] string email)
    {
        int? currentUserId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        }

        bool isAvailable = await _profileService.IsEmailUniqueAsync(email, currentUserId);
        return Ok(new { isAvailable });
    }

    [HttpPut("password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDTO passwordDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

        // Get the user
        var user = await _context.Users
            .Where(u => u.Id == currentUserId && !u.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Verify the current password
        if (!BCrypt.Net.BCrypt.Verify(passwordDto.CurrentPassword, user.PasswordHash))
        {
            return BadRequest(new { message = "Current password is incorrect" });
        }

        // Update the password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordDto.NewPassword);
        user.LastActive = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        
        return Ok(new { message = "Password updated successfully" });
    }
}