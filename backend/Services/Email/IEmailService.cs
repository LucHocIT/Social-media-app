using System.Threading.Tasks;

namespace SocialApp.Services.Email;

public interface IEmailService
{
    // Core email sending functionality
    Task<bool> SendEmailAsync(string to, string subject, string body);
    Task<bool> SendHtmlEmailAsync(string to, string subject, string htmlContent);
    
    // Template generation for verification emails
    string GenerateVerificationEmailTemplate(string code);
    string GeneratePasswordResetEmailTemplate(string code);
}