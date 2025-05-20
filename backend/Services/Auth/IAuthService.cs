using SocialApp.DTOs;
using SocialApp.Models;
using SocialApp.Services.Email;
using SocialApp.Services.User;

namespace SocialApp.Services.Auth;

public interface IAuthService : IUserAccountService, IEmailVerificationCodeService, IUserManagementService
{
    // All methods are inherited from the specialized interfaces
}
