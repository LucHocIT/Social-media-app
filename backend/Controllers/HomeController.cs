using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialApp.Controllers;

[ApiController]
[Route("")]
public class HomeController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public HomeController(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    [HttpGet]
    public ActionResult<string> Index()
    {
        return Ok("SocialApp API is running!");
    }
    
    [HttpGet("test-env")]
    public ActionResult<Dictionary<string, string>> TestEnvironmentVariables()
    {
        var result = new Dictionary<string, string>
        {
            // Display only existence info, not the actual value for security
            ["EMAIL_VERIFICATION_API_KEY (Process)"] = 
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EMAIL_VERIFICATION_API_KEY", EnvironmentVariableTarget.Process)) 
                ? "Set" : "Not set",
                
            ["EMAIL_VERIFICATION_API_KEY (User)"] = 
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EMAIL_VERIFICATION_API_KEY", EnvironmentVariableTarget.User)) 
                ? "Set" : "Not set",
                
            ["EMAIL_VERIFICATION_API_KEY (Machine)"] = 
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EMAIL_VERIFICATION_API_KEY", EnvironmentVariableTarget.Machine)) 
                ? "Set" : "Not set",
                
            ["Configuration EmailVerification:ApiKey"] = 
                string.IsNullOrEmpty(_configuration["EmailVerification:ApiKey"]) 
                ? "Not set" 
                : (_configuration["EmailVerification:ApiKey"] == "[EMAIL_VERIFICATION_API_KEY]" 
                    ? "Placeholder value" 
                    : "Set")
        };
        
        return Ok(result);
    }
}
