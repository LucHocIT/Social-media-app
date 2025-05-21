using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialApp.DTOs;
using SocialApp.Services.User;
using System.Security.Claims;

namespace SocialApp.Controllers.User;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        IProfileService profileService,
        ILogger<ProfileController> logger)
    {
        _profileService = profileService;
        _logger = logger;
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
    }
    [HttpPut("update")]
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
}