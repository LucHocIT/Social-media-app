using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Services;

// This interface now combines all the separate service interfaces for backward compatibility
// It delegates to the appropriate specialized services
public interface IAuthService : IUserAccountService, IEmailVerificationCodeService, IUserManagementService
{
    // All methods are inherited from the specialized interfaces
}
