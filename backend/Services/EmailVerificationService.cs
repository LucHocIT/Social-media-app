using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SocialApp.Services;

public class EmailVerificationService : IEmailVerificationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailVerificationService> _logger;

    public EmailVerificationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<EmailVerificationService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("EmailVerificationClient");
        _configuration = configuration;
        _logger = logger;
    }    public async Task<EmailVerificationResult> VerifyEmailAsync(string email)
    {
        try
        {
            // Get API key from configuration
            var apiKey = _configuration["EmailVerification:ApiKey"];
            
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Email verification API key is not configured");
                return new EmailVerificationResult 
                { 
                    IsValid = true, // Email được coi là hợp lệ về định dạng
                    Exists = false, // Không thể xác minh sự tồn tại email
                    Message = "Email verification limited - API key not configured"
                };
            }

            // Basic email format validation first
            if (!IsValidEmailFormat(email))
            {
                _logger.LogWarning("Email {Email} has invalid format", email);
                return new EmailVerificationResult
                {
                    IsValid = false,
                    Exists = false,
                    Message = "Invalid email format"
                };
            }            try
            {
                // Set timeout to avoid long waits
                _httpClient.Timeout = TimeSpan.FromSeconds(5);
                
                // For this example, we'll use Abstract API's email validation service
                var requestUrl = $"https://emailvalidation.abstractapi.com/v1/?api_key={apiKey}&email={Uri.EscapeDataString(email)}";
                
                _logger.LogInformation("Sending email verification request to API for {Email}", email);
                var response = await _httpClient.GetAsync(requestUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("API Response for {Email}: {Response}", email, responseContent);
                    
                    var verificationResponse = await response.Content.ReadFromJsonAsync<EmailVerificationApiResponse>();
                    
                    if (verificationResponse == null)
                    {
                        _logger.LogWarning("Email verification API returned null response");
                        // Không cho phép email không được xác thực
                        return new EmailVerificationResult { IsValid = true, Exists = false, Message = "Could not verify email existence" };
                    }
                    
                    // Extract useful information from the API response
                    bool isValidSyntax = verificationResponse.IsValidFormat == "true";
                    bool isSuspicious = verificationResponse.IsDisposableEmail == "true"; // Chỉ coi email dùng một lần là đáng nghi ngờ
                    bool isMxValid = verificationResponse.IsMxFound == "true"; // Kiểm tra xem domain có bản ghi MX không
                    bool isDeliverable = verificationResponse.Deliverability == "DELIVERABLE";
                    bool isSmtpValid = verificationResponse.IsSmtpValid == "true";
                    
                    // Log chi tiết để debug
                    _logger.LogInformation(
                        "Email {Email} validation results: ValidSyntax={ValidSyntax}, Disposable={Disposable}, " +
                        "MxValid={MxValid}, Deliverable={Deliverable}, SmtpValid={SmtpValid}",
                        email, isValidSyntax, isSuspicious, isMxValid, isDeliverable, isSmtpValid);
                    
                    return new EmailVerificationResult
                    {
                        // Email hợp lệ nếu cú pháp đúng và không phải email dùng một lần
                        IsValid = isValidSyntax && !isSuspicious,
                        // Email tồn tại nếu có bản ghi MX và có thể gửi đến
                        Exists = isMxValid && (isDeliverable || isSmtpValid),
                        Message = verificationResponse.Deliverability
                    };
                }
                else
                {
                    _logger.LogError("Email verification API returned status code: {StatusCode}", response.StatusCode);
                    // Không cho phép email không được xác thực đầy đủ
                    return new EmailVerificationResult 
                    { 
                        IsValid = true, // Email có định dạng hợp lệ (đã kiểm tra ở trên)
                        Exists = false, // Không thể xác minh sự tồn tại
                        Message = "Could not verify email existence (API error)" 
                    };
                }
            }
            catch (Exception apiEx)
            {
                _logger.LogError(apiEx, "API error when verifying email {Email}", email);
                // Không cho phép email không được xác thực đầy đủ
                return new EmailVerificationResult 
                { 
                    IsValid = true, // Email có định dạng hợp lệ (đã kiểm tra ở trên)
                    Exists = false, // Không thể xác minh sự tồn tại
                    Message = "Could not verify email existence (API error)" 
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying email {Email}", email);
            return new EmailVerificationResult 
            { 
                IsValid = false, 
                Exists = false,
                Message = $"Verification error: {ex.Message}" 
            };
        }
    }
    
    private bool IsValidEmailFormat(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    // Response class structured to match the Abstract API email validation response
    private class EmailVerificationApiResponse
    {
        [JsonPropertyName("email")]
        public string? Email { get; set; }
        
        [JsonPropertyName("deliverability")]
        public string? Deliverability { get; set; }
        
        [JsonPropertyName("quality_score")]
        public string? QualityScore { get; set; }
        
        [JsonPropertyName("is_valid_format")]
        public string? IsValidFormat { get; set; }
        
        [JsonPropertyName("is_free_email")]
        public string? IsFreeEmail { get; set; }
        
        [JsonPropertyName("is_disposable_email")]
        public string? IsDisposableEmail { get; set; }
        
        [JsonPropertyName("is_role_email")]
        public string? IsRoleEmail { get; set; }
        
        [JsonPropertyName("is_catchall_email")]
        public string? IsCatchallEmail { get; set; }
        
        [JsonPropertyName("is_mx_found")]
        public string? IsMxFound { get; set; }
        
        [JsonPropertyName("is_smtp_valid")]
        public string? IsSmtpValid { get; set; }
    }
}
