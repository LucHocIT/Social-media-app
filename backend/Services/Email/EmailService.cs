using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SocialApp.Services.Email;

public class EmailService : IEmailService
{    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly string _smtpServer;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly string _senderEmail;
    private readonly string _senderName;
    private readonly bool _useSsl;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Load email configuration with priority to environment variables
        _smtpServer = Environment.GetEnvironmentVariable("EMAIL_SMTP_SERVER") 
            ?? _configuration["EmailSettings:SmtpServer"] 
            ?? "smtp.example.com";
        
        _smtpPort = int.TryParse(Environment.GetEnvironmentVariable("EMAIL_SMTP_PORT"), out int port) 
            ? port 
            : int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
        
        _smtpUsername = Environment.GetEnvironmentVariable("EMAIL_USERNAME") 
            ?? _configuration["EmailSettings:Username"] 
            ?? "user@example.com";
        
        _smtpPassword = Environment.GetEnvironmentVariable("EMAIL_PASSWORD") 
            ?? _configuration["EmailSettings:Password"] 
            ?? "password";
        
        _senderEmail = Environment.GetEnvironmentVariable("EMAIL_SENDER") 
            ?? _configuration["EmailSettings:SenderEmail"] 
            ?? "noreply@socialapp.com";
        
        _senderName = Environment.GetEnvironmentVariable("EMAIL_SENDER_NAME") 
            ?? _configuration["EmailSettings:SenderName"] 
            ?? "SocialApp";
        
        _useSsl = bool.TryParse(Environment.GetEnvironmentVariable("EMAIL_USE_SSL"), out bool ssl) 
            ? ssl 
            : bool.Parse(_configuration["EmailSettings:UseSsl"] ?? "true");
    }    public async Task<bool> SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            _logger.LogInformation("Attempting to send email to: {To}", to);
            _logger.LogInformation("SMTP Configuration: Server={Server}, Port={Port}, Username={Username}, UseSSL={UseSSL}", 
                _smtpServer, _smtpPort, _smtpUsername, _useSsl);
            
            using var message = new MailMessage
            {
                From = new MailAddress(_senderEmail, _senderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            
            message.To.Add(new MailAddress(to));
            
            using var client = new SmtpClient(_smtpServer, _smtpPort)
            {
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                EnableSsl = _useSsl
            };
            
            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent successfully to {To}", to);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            return false;
        }
    }    public async Task<bool> SendHtmlEmailAsync(string to, string subject, string htmlContent)
    {
        try
        {
            _logger.LogInformation("Attempting to send HTML email to: {To}", to);
            _logger.LogInformation("SMTP Configuration: Server={Server}, Port={Port}, Username={Username}, UseSSL={UseSSL}", 
                _smtpServer, _smtpPort, _smtpUsername, _useSsl);
            
            using var message = new MailMessage
            {
                From = new MailAddress(_senderEmail, _senderName),
                Subject = subject,
                Body = htmlContent,
                IsBodyHtml = true
            };
            
            message.To.Add(new MailAddress(to));
            
            using var client = new SmtpClient(_smtpServer, _smtpPort)
            {
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                EnableSsl = _useSsl
            };
            
            await client.SendMailAsync(message);
            _logger.LogInformation("HTML Email sent successfully to {To}", to);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send HTML email to {To}", to);
            return false;
        }
    }
    
    // Email template generation methods to support verification service
    public string GenerateVerificationEmailTemplate(string code)
    {
        return $@"
            <html>
            <body style='font-family: Arial, sans-serif; color: #333;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px;'>
                    <h2 style='color: #4a6ee0;'>Xác nhận email đăng ký</h2>
                    <p>Cảm ơn bạn đã đăng ký tài khoản trên SocialApp!</p>
                    <p>Mã xác nhận của bạn là:</p>
                    <div style='background-color: #f5f5f5; padding: 10px; text-align: center; font-size: 24px; font-weight: bold; letter-spacing: 2px; margin: 15px 0;'>
                        {code}
                    </div>
                    <p>Mã xác nhận này sẽ hết hạn sau 10 phút.</p>
                    <p>Nếu bạn không yêu cầu đăng ký tài khoản, vui lòng bỏ qua email này.</p>
                    <p>Trân trọng,<br>Đội ngũ SocialApp</p>
                </div>
            </body>
            </html>";
    }
    
    public string GeneratePasswordResetEmailTemplate(string code)
    {
        return $@"
            <html>
            <body style='font-family: Arial, sans-serif; color: #333;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px;'>
                    <h2 style='color: #4a6ee0;'>Đặt lại mật khẩu</h2>
                    <p>Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản của mình trên SocialApp.</p>
                    <p>Mã xác nhận của bạn là:</p>
                    <div style='background-color: #f5f5f5; padding: 10px; text-align: center; font-size: 24px; font-weight: bold; letter-spacing: 2px; margin: 15px 0;'>
                        {code}
                    </div>
                    <p>Mã xác nhận này sẽ hết hạn sau 10 phút.</p>
                    <p>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                    <p>Trân trọng,<br>Đội ngũ SocialApp</p>
                </div>
            </body>
            </html>";
    }
}