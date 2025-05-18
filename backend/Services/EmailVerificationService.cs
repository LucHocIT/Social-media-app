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
            
            // In development mode or any environment when API calls might be unstable,
            // just validate the email format without calling the external API
            var isDevelopment = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";
            // Always consider the email valid and exists in development mode
            if (isDevelopment)
            {
                _logger.LogInformation("Development environment detected - bypassing external API validation for {Email}", email);
                return new EmailVerificationResult
                {
                    IsValid = true,
                    Exists = true,
                    Message = "Development mode - email format is valid"
                };
            }
            
            // Get API key from configuration
            var apiKey = _configuration["EmailVerification:ApiKey"];
            
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Email verification API key is not configured");
                return new EmailVerificationResult 
                { 
                    IsValid = true, // Consider email as valid format
                    Exists = true,  // In development, assume email exists
                    Message = "Email verification limited - API key not configured"
                };
            }
            
            try
            {
                // Using a very short timeout to avoid application hanging
                // Set timeout to avoid long waits
                _httpClient.Timeout = TimeSpan.FromSeconds(3);
                
                // For this example, we'll use Abstract API's email validation service
                var requestUrl = $"https://emailvalidation.abstractapi.com/v1/?api_key={apiKey}&email={Uri.EscapeDataString(email)}";
                
                _logger.LogInformation("Sending email verification request to API for {Email}", email);
                
                // Use CancellationToken to ensure quick timeout
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var response = await _httpClient.GetAsync(requestUrl, cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                    _logger.LogInformation("API Response for {Email}: {Response}", email, responseContent);
                    
                    var verificationResponse = await response.Content.ReadFromJsonAsync<EmailVerificationApiResponse>(cancellationToken: cts.Token);
                    
                    if (verificationResponse == null)
                    {
                        _logger.LogWarning("Email verification API returned null response");
                        // Consider the email valid but not verified
                        return new EmailVerificationResult { IsValid = true, Exists = true, Message = "Could not verify email existence" };
                    }
                    
                    // Extract useful information from the API response
                    bool isValidSyntax = verificationResponse.IsValidFormat == "true";
                    bool isSuspicious = verificationResponse.IsDisposableEmail == "true"; // Only consider disposable emails suspicious
                    bool isMxValid = verificationResponse.IsMxFound == "true"; // Check if domain has MX records
                    bool isDeliverable = verificationResponse.Deliverability == "DELIVERABLE";
                    bool isSmtpValid = verificationResponse.IsSmtpValid == "true";
                    
                    // Log details for debugging
                    _logger.LogInformation(
                        "Email {Email} validation results: ValidSyntax={ValidSyntax}, Disposable={Disposable}, " +
                        "MxValid={MxValid}, Deliverable={Deliverable}, SmtpValid={SmtpValid}",
                        email, isValidSyntax, isSuspicious, isMxValid, isDeliverable, isSmtpValid);
                    
                    return new EmailVerificationResult
                    {                        // Email is valid if syntax is correct and it's not a disposable email
                        IsValid = isValidSyntax && !isSuspicious,
                        // Email exists if it has MX records and is deliverable
                        Exists = isMxValid && (isDeliverable || isSmtpValid),
                        Message = verificationResponse.Deliverability
                    };
                }
                else
                {
                    _logger.LogError("Email verification API returned status code: {StatusCode}", response.StatusCode);
                    // Assume the email is valid and exists when API check fails
                    return new EmailVerificationResult 
                    { 
                        IsValid = true, // Email has valid format (already checked above)
                        Exists = true,  // CHANGED: Assume email exists to allow registration to proceed
                        Message = "Could not verify email existence (API error)" 
                    };
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Email verification API request timed out for {Email}", email);
                // Assume the email is valid and exists when API times out
                return new EmailVerificationResult 
                { 
                    IsValid = true,
                    Exists = true,
                    Message = "Email verification timed out, proceeding with registration" 
                };
            }
            catch (Exception apiEx)
            {
                _logger.LogError(apiEx, "API error when verifying email {Email}", email);
                // Assume the email is valid and exists when API check fails
                return new EmailVerificationResult 
                { 
                    IsValid = true, // Email has valid format (already checked above)
                    Exists = true,  // CHANGED: Assume email exists to allow registration to proceed
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
