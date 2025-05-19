using SocialApp.DTOs;
using SocialApp.Models;
using SocialApp.Services.Email;
using SocialApp.Services.User;

namespace SocialApp.Services.Auth;

// This interface now combines all the separate service interfaces for backward compatibility
// It delegates to the appropriate specialized services
public interface IAuthService : IUserAccountService, IEmailVerificationCodeService, IUserManagementService
{
    // All methods are inherited from the specialized interfaces
}
