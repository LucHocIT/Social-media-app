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
    }public async Task<EmailVerificationResult> VerifyEmailAsync(string email)
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
                    IsValid = true, // Default to true in case API key is not configured
                    Exists = true,
                    Message = "Email verification skipped - API key not configured"
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
            }

            try
            {
                // Set timeout to avoid long waits
                _httpClient.Timeout = TimeSpan.FromSeconds(5);
                
                // For this example, we'll use Abstract API's email validation service
                var requestUrl = $"https://emailvalidation.abstractapi.com/v1/?api_key={apiKey}&email={Uri.EscapeDataString(email)}";
                
                var response = await _httpClient.GetAsync(requestUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var verificationResponse = await response.Content.ReadFromJsonAsync<EmailVerificationApiResponse>();
                    
                    if (verificationResponse == null)
                    {
                        _logger.LogWarning("Email verification API returned null response");
                        // Fallback to basic validation
                        return new EmailVerificationResult { IsValid = true, Exists = true, Message = "Email format is valid" };
                    }
                    
                    // Extract useful information from the API response
                    bool isValidSyntax = verificationResponse.IsValidFormat == "true";
                    bool isSuspicious = verificationResponse.IsFreeEmail == "true" || 
                                       verificationResponse.IsDisposableEmail == "true";
                    bool isDeliverable = verificationResponse.Deliverability == "DELIVERABLE";
                    
                    return new EmailVerificationResult
                    {
                        IsValid = isValidSyntax && !isSuspicious,
                        Exists = isDeliverable,
                        Message = verificationResponse.Deliverability
                    };
                }
                else
                {
                    _logger.LogError("Email verification API returned status code: {StatusCode}", response.StatusCode);
                    // Fallback to basic validation
                    return new EmailVerificationResult 
                    { 
                        IsValid = true, 
                        Exists = true,
                        Message = "Email format is valid (API unavailable)" 
                    };
                }
            }
            catch (Exception apiEx)
            {
                _logger.LogError(apiEx, "API error when verifying email {Email}", email);
                // Fallback to basic validation when API is unavailable
                return new EmailVerificationResult 
                { 
                    IsValid = true, 
                    Exists = true, 
                    Message = "Email format is valid (API unavailable)" 
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
