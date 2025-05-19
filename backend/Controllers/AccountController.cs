using Microsoft.AspNetCore.Mvc;
using SocialApp.DTOs;
using SocialApp.Services;

namespace SocialApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly IUserAccountService _userAccountService;
    private readonly IEmailVerificationCodeService _verificationCodeService;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly ILogger<AccountController> _logger;
    private readonly IConfiguration _configuration;

    public AccountController(
        IUserAccountService userAccountService,
        IEmailVerificationCodeService verificationCodeService,
        IEmailVerificationService emailVerificationService,
        ILogger<AccountController> logger,
        IConfiguration configuration)
    {
        _userAccountService = userAccountService;
        _verificationCodeService = verificationCodeService;
        _emailVerificationService = emailVerificationService;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPost("register/legacy")]
    [ApiExplorerSettings(IgnoreApi = true)] // Hide this from Swagger/UI as we're now using the verified registration flow
    public async Task<ActionResult<AuthResponseDTO>> RegisterLegacy(RegisterUserDTO registerDto)
    {
        try
        {
            _logger.LogInformation("Registering new user (legacy): {Username}, Email: {Email}", 
                registerDto.Username, registerDto.Email);
                
            if (registerDto == null)
            {
                _logger.LogWarning("Registration data is null");
                return BadRequest(new { message = "Registration data cannot be empty" });
            }
            
            // Log received data for debugging
            _logger.LogInformation("Registration data: Username={Username}, Email={Email}, " +
                "FirstName={FirstName}, LastName={LastName}",
                registerDto.Username, registerDto.Email, registerDto.FirstName, registerDto.LastName);
                
            var result = await _userAccountService.RegisterAsync(registerDto);
            _logger.LogInformation("User registered successfully: {Username}", registerDto.Username);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for user {Username}", registerDto?.Username);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("verifyemail")]
    public async Task<ActionResult> VerifyEmail([FromBody] VerifyEmailDTO verifyEmailDto)
    {
        try
        {
            _logger.LogInformation("Email verification requested for: {Email}", verifyEmailDto.Email);
            
            // Basic email format validation first
            try
            {
                var addr = new System.Net.Mail.MailAddress(verifyEmailDto.Email);
                if (addr.Address != verifyEmailDto.Email)
                {
                    _logger.LogInformation("Email {Email} has invalid format", verifyEmailDto.Email);
                    return BadRequest(new { isValid = false, message = "Invalid email format" });
                }
            }
            catch
            {
                _logger.LogInformation("Email {Email} has invalid format", verifyEmailDto.Email);
                return BadRequest(new { isValid = false, message = "Invalid email format" });
            }
            
            // Check if email already exists in our database
            if (await _userAccountService.EmailExistsAsync(verifyEmailDto.Email))
            {
                _logger.LogInformation("Email {Email} already exists in database", verifyEmailDto.Email);
                return BadRequest(new { isValid = false, message = "Email already in use" });
            }
            
            try 
            {
                // Verify email format and existence using the external service with timeout handling
                var verificationResult = await _emailVerificationService.VerifyEmailAsync(verifyEmailDto.Email);
                _logger.LogInformation("Email {Email} verification result: IsValid={IsValid}, Exists={Exists}, Message={Message}", 
                    verifyEmailDto.Email, verificationResult.IsValid, verificationResult.Exists, verificationResult.Message);
                
                // Check email format
                if (!verificationResult.IsValid)
                {
                    _logger.LogInformation("Email {Email} is invalid", verifyEmailDto.Email);
                    return BadRequest(new { isValid = false, message = "Invalid email format" });
                }
                
                // Check if email exists
                if (!verificationResult.Exists)
                {
                    _logger.LogInformation("Email {Email} doesn't exist", verifyEmailDto.Email);
                    return BadRequest(new { 
                        isValid = false, 
                        message = "This email address doesn't appear to exist. Please use a valid, existing email address." 
                    });
                }
            }
            catch (Exception ex)
            {
                // Log the error and don't allow verification to proceed with potentially invalid emails
                _logger.LogWarning(ex, "External email verification service failed for {Email}", verifyEmailDto.Email);
                return BadRequest(new { 
                    isValid = false, 
                    message = "Could not verify email. Please try again later or contact support."
                });
            }

            return Ok(new { isValid = true, message = "Email is valid" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during email verification");
            return BadRequest(new { isValid = false, message = "Error verifying email. Please try again later." });
        }
    }

    [HttpPost("register/verified")]
    public async Task<ActionResult<AuthResponseDTO>> RegisterVerified(RegisterWithVerificationDTO registerDto)
    {
        try
        {
            _logger.LogInformation("Registering new user with verification: {Username}, Email: {Email}", 
                registerDto.Username, registerDto.Email);
                
            if (registerDto == null)
            {
                _logger.LogWarning("Registration data is null");
                return BadRequest(new { message = "Registration data cannot be empty" });
            }
            
            // Log received data for debugging
            _logger.LogInformation("Registration data: Username={Username}, Email={Email}, " +
                "FirstName={FirstName}, LastName={LastName}",
                registerDto.Username, registerDto.Email, registerDto.FirstName, registerDto.LastName);
                
            var result = await _userAccountService.RegisterAsync(registerDto);
            _logger.LogInformation("User registered successfully: {Username}", registerDto.Username);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for user {Username}", registerDto?.Username);
            return BadRequest(new { message = ex.Message });
        }
    }
    
    [HttpPost("sendVerificationCode")]
    public async Task<IActionResult> SendVerificationCode([FromBody] SendVerificationCodeDTO sendVerificationCodeDto)
    {
        try
        {
            _logger.LogInformation("Sending verification code to: {Email}", sendVerificationCodeDto.Email);
            
            var (success, message) = await _verificationCodeService.SendVerificationCodeAsync(sendVerificationCodeDto.Email);
            
            if (!success)
            {
                return BadRequest(new { success = false, message });
            }
            
            return Ok(new { success = true, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending verification code to {Email}", sendVerificationCodeDto?.Email);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    
    [HttpPost("verifyCode")]
    public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeDTO verifyCodeDto)
    {
        try
        {
            _logger.LogInformation("Verifying code for: {Email}", verifyCodeDto.Email);
            
            var (success, message) = await _verificationCodeService.VerifyCodeAsync(verifyCodeDto.Email, verifyCodeDto.Code);
            
            if (!success)
            {
                return BadRequest(new { success = false, message });
            }
            
            return Ok(new { 
                success = true, 
                message,
                readyForRegistration = true,
                email = verifyCodeDto.Email
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying code for {Email}", verifyCodeDto?.Email);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    
    [HttpPost("verifyAndRegister")]
    public async Task<ActionResult<AuthResponseDTO>> VerifyAndRegister([FromBody] VerifiedRegisterDTO registerDto)
    {
        try
        {
            _logger.LogInformation("Registering verified user: {Email}", registerDto.Email);
            
            if (registerDto == null)
            {
                _logger.LogWarning("Registration data is null");
                return BadRequest(new { message = "Registration data cannot be empty" });
            }
            
            var result = await _userAccountService.RegisterVerifiedUserAsync(registerDto);
            _logger.LogInformation("User registered successfully after verification: {Username}", registerDto.Username);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during verification and registration for user {Username}", registerDto?.Username);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("register")]
    public ActionResult RedirectToVerifiedRegistration()
    {
        // This is just a redirect to maintain backward compatibility
        return RedirectToAction(nameof(RegisterVerified));
    }
    
    [HttpPost("forgotPassword")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO forgotPasswordDto)
    {
        try
        {
            _logger.LogInformation("Password reset requested for: {Email}", forgotPasswordDto.Email);
            
            var (success, message) = await _verificationCodeService.SendPasswordResetCodeAsync(forgotPasswordDto.Email);
            
            if (!success)
            {
                return BadRequest(new { success = false, message });
            }
            
            return Ok(new { success = true, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing forgot password request for {Email}", forgotPasswordDto?.Email);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    
    [HttpPost("verifyResetCode")]
    public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeDTO verifyResetCodeDto)
    {
        try
        {
            _logger.LogInformation("Verifying reset code for: {Email}", verifyResetCodeDto.Email);
            
            var (success, message) = await _verificationCodeService.VerifyPasswordResetCodeAsync(
                verifyResetCodeDto.Email, verifyResetCodeDto.Code);
            
            if (!success)
            {
                return BadRequest(new { success = false, message });
            }
            
            return Ok(new { success = true, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying reset code for {Email}", verifyResetCodeDto?.Email);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    
    [HttpPost("resetPassword")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO resetPasswordDto)
    {
        try
        {
            _logger.LogInformation("Resetting password for: {Email}", resetPasswordDto.Email);
            
            var (success, message) = await _verificationCodeService.ResetPasswordAsync(resetPasswordDto);
            
            if (!success)
            {
                return BadRequest(new { success = false, message });
            }
            
            return Ok(new { success = true, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for {Email}", resetPasswordDto?.Email);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}
