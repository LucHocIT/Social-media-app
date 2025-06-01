using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialApp.DTOs;
using SocialApp.Services.User;
using System.Security.Claims;

namespace SocialApp.Controllers.User;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserBlockController : ControllerBase
{
    private readonly IUserBlockService _userBlockService;
    private readonly ILogger<UserBlockController> _logger;

    public UserBlockController(
        IUserBlockService userBlockService,
        ILogger<UserBlockController> logger)
    {
        _userBlockService = userBlockService;
        _logger = logger;
    }

    /// <summary>
    /// Block a user
    /// </summary>
    /// <param name="requestDto">Block user request containing blocked user ID and optional reason</param>
    /// <returns>Success status</returns>
    [HttpPost("block")]
    public async Task<ActionResult> BlockUser([FromBody] BlockUserRequestDto requestDto)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized("User not authenticated");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _userBlockService.BlockUserAsync(currentUserId.Value, requestDto);
            
            if (!result)
            {
                return BadRequest("Failed to block user. User may not exist or you may be trying to block yourself.");
            }

            return Ok(new { message = "User blocked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blocking user {BlockedUserId} by user {CurrentUserId}", 
                requestDto.BlockedUserId, GetCurrentUserId());
            return StatusCode(500, new { message = "An error occurred while blocking the user" });
        }
    }

    /// <summary>
    /// Unblock a user
    /// </summary>
    /// <param name="requestDto">Unblock user request containing blocked user ID</param>
    /// <returns>Success status</returns>
    [HttpPost("unblock")]
    public async Task<ActionResult> UnblockUser([FromBody] UnblockUserRequestDto requestDto)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized("User not authenticated");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _userBlockService.UnblockUserAsync(currentUserId.Value, requestDto);
            
            if (!result)
            {
                return NotFound("No block relationship found or user does not exist");
            }

            return Ok(new { message = "User unblocked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unblocking user {BlockedUserId} by user {CurrentUserId}", 
                requestDto.BlockedUserId, GetCurrentUserId());
            return StatusCode(500, new { message = "An error occurred while unblocking the user" });
        }
    }

    /// <summary>
    /// Get block status between current user and another user
    /// </summary>
    /// <param name="userId">The other user's ID</param>
    /// <returns>Block status information</returns>
    [HttpGet("status/{userId}")]
    public async Task<ActionResult<BlockStatusDto>> GetBlockStatus(int userId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized("User not authenticated");
            }

            var blockStatus = await _userBlockService.GetBlockStatusAsync(currentUserId.Value, userId);
            return Ok(blockStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting block status between user {CurrentUserId} and user {UserId}", 
                GetCurrentUserId(), userId);
            return StatusCode(500, new { message = "An error occurred while getting block status" });
        }
    }

    /// <summary>
    /// Get list of users blocked by current user
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 20, max: 50)</param>
    /// <returns>Paginated list of blocked users</returns>
    [HttpGet("blocked-users")]
    public async Task<ActionResult<BlockedUsersListDto>> GetBlockedUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized("User not authenticated");
            }

            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 20;

            var blockedUsers = await _userBlockService.GetBlockedUsersAsync(currentUserId.Value, page, pageSize);
            return Ok(blockedUsers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting blocked users list for user {CurrentUserId}", GetCurrentUserId());
            return StatusCode(500, new { message = "An error occurred while getting blocked users" });
        }
    }

    /// <summary>
    /// Check if a specific user is blocked by current user
    /// </summary>
    /// <param name="userId">The user ID to check</param>
    /// <returns>Boolean indicating if user is blocked</returns>
    [HttpGet("is-blocked/{userId}")]
    public async Task<ActionResult<bool>> IsUserBlocked(int userId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized("User not authenticated");
            }

            var isBlocked = await _userBlockService.IsUserBlockedAsync(currentUserId.Value, userId);
            return Ok(new { isBlocked });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} is blocked by user {CurrentUserId}", 
                userId, GetCurrentUserId());
            return StatusCode(500, new { message = "An error occurred while checking block status" });
        }
    }

    /// <summary>
    /// Check if two users are blocking each other
    /// </summary>
    /// <param name="userId">The other user's ID</param>
    /// <returns>Boolean indicating if users are blocking each other</returns>
    [HttpGet("mutual-block/{userId}")]
    public async Task<ActionResult<bool>> AreUsersBlockingEachOther(int userId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized("User not authenticated");
            }

            var areBlocking = await _userBlockService.AreUsersBlockingEachOtherAsync(currentUserId.Value, userId);
            return Ok(new { areBlockingEachOther = areBlocking });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking mutual block status between user {CurrentUserId} and user {UserId}", 
                GetCurrentUserId(), userId);
            return StatusCode(500, new { message = "An error occurred while checking mutual block status" });
        }
    }

    /// <summary>
    /// Helper method to get current user ID from JWT token
    /// </summary>
    /// <returns>Current user ID or null if not found</returns>
    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return userIdClaim != null && int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
