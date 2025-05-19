namespace SocialApp.Services.Email;

public interface IEmailVerificationService
{
    /// <summary>
    /// Verifies if an email address exists and is valid
    /// </summary>
    /// <param name="email">The email address to verify</param>
    /// <returns>True if the email is valid and exists, otherwise false</returns>
    Task<EmailVerificationResult> VerifyEmailAsync(string email);
}

public class EmailVerificationResult
{
    public bool IsValid { get; set; }
    public bool Exists { get; set; }
    public string? Message { get; set; }
}
