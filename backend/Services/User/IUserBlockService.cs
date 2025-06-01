using SocialApp.DTOs;

namespace SocialApp.Services.User;

public interface IUserBlockService
{
    Task<bool> BlockUserAsync(int blockerId, BlockUserRequestDto requestDto);
    Task<bool> UnblockUserAsync(int blockerId, UnblockUserRequestDto requestDto);
    Task<BlockStatusDto> GetBlockStatusAsync(int currentUserId, int otherUserId);
    Task<BlockedUsersListDto> GetBlockedUsersAsync(int userId, int page = 1, int pageSize = 20);
    Task<bool> IsUserBlockedAsync(int blockerId, int blockedUserId);
    Task<bool> AreUsersBlockingEachOtherAsync(int userId1, int userId2);
}
