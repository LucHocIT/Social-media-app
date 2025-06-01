using Microsoft.EntityFrameworkCore;
using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Services.User;

public class UserBlockService : IUserBlockService
{
    private readonly SocialMediaDbContext _context;
    private readonly ILogger<UserBlockService> _logger;

    public UserBlockService(
        SocialMediaDbContext context,
        ILogger<UserBlockService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> BlockUserAsync(int blockerId, BlockUserRequestDto requestDto)
    {
        try
        {
            // Validate that user is not trying to block themselves
            if (blockerId == requestDto.BlockedUserId)
            {
                _logger.LogWarning("User {UserId} attempted to block themselves", blockerId);
                return false;
            }

            // Check if the blocked user exists
            var blockedUserExists = await _context.Users
                .AnyAsync(u => u.Id == requestDto.BlockedUserId && !u.IsDeleted);
            
            if (!blockedUserExists)
            {
                _logger.LogWarning("BlockUserAsync failed: User {BlockedUserId} not found", requestDto.BlockedUserId);
                return false;
            }

            // Check if block relationship already exists
            var existingBlock = await _context.UserBlocks
                .FirstOrDefaultAsync(ub => ub.BlockerId == blockerId && ub.BlockedUserId == requestDto.BlockedUserId);

            if (existingBlock != null)
            {
                _logger.LogWarning("User {BlockerId} attempted to block user {BlockedUserId} who is already blocked", 
                    blockerId, requestDto.BlockedUserId);
                return true; // Already blocked, consider it successful
            }

            // Create new block record
            var userBlock = new UserBlock
            {
                BlockerId = blockerId,
                BlockedUserId = requestDto.BlockedUserId,
                CreatedAt = DateTime.UtcNow,
                Reason = requestDto.Reason
            };

            await _context.UserBlocks.AddAsync(userBlock);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {BlockerId} successfully blocked user {BlockedUserId}", 
                blockerId, requestDto.BlockedUserId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blocking user {BlockedUserId} by user {BlockerId}", 
                requestDto.BlockedUserId, blockerId);
            throw;
        }
    }

    public async Task<bool> UnblockUserAsync(int blockerId, UnblockUserRequestDto requestDto)
    {
        try
        {
            // Find the existing block record
            var existingBlock = await _context.UserBlocks
                .FirstOrDefaultAsync(ub => ub.BlockerId == blockerId && ub.BlockedUserId == requestDto.BlockedUserId);

            if (existingBlock == null)
            {
                _logger.LogWarning("UnblockUserAsync failed: No block relationship found between user {BlockerId} and user {BlockedUserId}", 
                    blockerId, requestDto.BlockedUserId);
                return false;
            }

            // Remove the block record
            _context.UserBlocks.Remove(existingBlock);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {BlockerId} successfully unblocked user {BlockedUserId}", 
                blockerId, requestDto.BlockedUserId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unblocking user {BlockedUserId} by user {BlockerId}", 
                requestDto.BlockedUserId, blockerId);
            throw;
        }
    }

    public async Task<BlockStatusDto> GetBlockStatusAsync(int currentUserId, int otherUserId)
    {
        try
        {
            // Check if current user has blocked the other user
            var userBlockedOther = await _context.UserBlocks
                .FirstOrDefaultAsync(ub => ub.BlockerId == currentUserId && ub.BlockedUserId == otherUserId);

            // Check if other user has blocked the current user
            var otherBlockedUser = await _context.UserBlocks
                .FirstOrDefaultAsync(ub => ub.BlockerId == otherUserId && ub.BlockedUserId == currentUserId);

            return new BlockStatusDto
            {
                IsBlocked = userBlockedOther != null,
                IsBlockedBy = otherBlockedUser != null,
                BlockedAt = userBlockedOther?.CreatedAt,
                Reason = userBlockedOther?.Reason
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting block status between user {CurrentUserId} and user {OtherUserId}", 
                currentUserId, otherUserId);
            throw;
        }
    }

    public async Task<BlockedUsersListDto> GetBlockedUsersAsync(int userId, int page = 1, int pageSize = 20)
    {
        try
        {
            var query = _context.UserBlocks
                .Where(ub => ub.BlockerId == userId)
                .Include(ub => ub.BlockedUser)
                .OrderByDescending(ub => ub.CreatedAt);

            var totalCount = await query.CountAsync();
            var blockedUsers = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ub => new BlockedUserDto
                {
                    Id = ub.Id,
                    BlockedUserId = ub.BlockedUserId,
                    BlockedUserName = $"{ub.BlockedUser.FirstName} {ub.BlockedUser.LastName}".Trim(),
                    BlockedUserAvatar = ub.BlockedUser.ProfilePictureUrl,
                    Reason = ub.Reason,
                    CreatedAt = ub.CreatedAt
                })
                .ToListAsync();

            return new BlockedUsersListDto
            {
                BlockedUsers = blockedUsers,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                HasMore = totalCount > page * pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting blocked users list for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> IsUserBlockedAsync(int blockerId, int blockedUserId)
    {
        try
        {
            return await _context.UserBlocks
                .AnyAsync(ub => ub.BlockerId == blockerId && ub.BlockedUserId == blockedUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {BlockedUserId} is blocked by user {BlockerId}", 
                blockedUserId, blockerId);
            throw;
        }
    }

    public async Task<bool> AreUsersBlockingEachOtherAsync(int userId1, int userId2)
    {
        try
        {
            // Check if either user has blocked the other
            return await _context.UserBlocks
                .AnyAsync(ub => (ub.BlockerId == userId1 && ub.BlockedUserId == userId2) ||
                               (ub.BlockerId == userId2 && ub.BlockedUserId == userId1));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if users {UserId1} and {UserId2} are blocking each other", 
                userId1, userId2);
            throw;
        }
    }
}
