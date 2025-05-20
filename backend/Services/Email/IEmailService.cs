using System.Threading.Tasks;

namespace SocialApp.Services.Email;

public interface IEmailService
{
    Task<bool> SendEmailAsync(string to, string subject, string body);
    Task<bool> SendHtmlEmailAsync(string to, string subject, string htmlContent);
}