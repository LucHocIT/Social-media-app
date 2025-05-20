# Environment Variables Setup for SocialApp

This document describes how to set up environment variables for the SocialApp API, particularly for securing sensitive information like API keys.

## Email Verification API Key

The Email Verification service uses [Abstract API's Email Validation API](https://www.abstractapi.com/api/email-verification-validation-api) to validate email addresses. This service requires an API key which, for security reasons, should be stored in an environment variable rather than in configuration files.

### Getting an API Key

1. Create an account on [Abstract API](https://www.abstractapi.com/)
2. Subscribe to their Email Validation API
3. Copy your API key from the dashboard

The free tier offers 100 validations per month, which should be sufficient for testing purposes.

### Setting up the Environment Variable

#### On Windows (Development Environment)

To set the environment variable temporarily (for current session only):

```powershell
$env:EMAIL_VERIFICATION_API_KEY="your-actual-api-key-here"
```

To set it permanently for the machine (requires admin privileges):

```powershell
[Environment]::SetEnvironmentVariable("EMAIL_VERIFICATION_API_KEY", "your-actual-api-key-here", "Machine")
```

Or for the current user only:

```powershell
[Environment]::SetEnvironmentVariable("EMAIL_VERIFICATION_API_KEY", "your-actual-api-key-here", "User")
```

#### On Linux/macOS (Development Environment)

To set it temporarily for the current session:

```bash
export EMAIL_VERIFICATION_API_KEY="your-actual-api-key-here"
```

To set it permanently, add the export command to your `~/.bashrc` or `~/.zshrc` file.

### In Production Environment

For production deployments, set the environment variable according to your hosting platform:

- **Azure App Service**: Set it in the Configuration > Application settings
- **Docker**: Include it in your docker-compose file or docker run command
- **IIS**: Set it in the web.config or through the IIS Manager
- **Linux server**: Set it in the systemd service file or relevant startup script

## Verifying Environment Variables

To test if your environment variable is correctly set up, you can use the following code:

```csharp
var apiKey = Environment.GetEnvironmentVariable("EMAIL_VERIFICATION_API_KEY");
Console.WriteLine($"API Key is {(string.IsNullOrEmpty(apiKey) ? "not set" : "set correctly")}");
```

## Fallback Mechanism

If the environment variable is not found, the application will attempt to use the value from `appsettings.json`. However, this is not recommended for production use.
