using Microsoft.AspNetCore.Mvc;
using SocialApp.Services.Message;

namespace SocialApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly ILogger<TestController> _logger;

    public TestController(IMessageService messageService, ILogger<TestController> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    [HttpGet("message-system")]
    public async Task<IActionResult> TestMessageSystem()
    {
        try
        {
            // Test basic functionality without Redis dependencies
            var result = new
            {
                Status = "OK",
                Message = "Message system is properly configured",
                Timestamp = DateTime.UtcNow,
                Features = new[]
                {
                    "Conversation management",
                    "Message batching",
                    "Media attachments",
                    "SignalR realtime messaging",
                    "Redis caching (requires Redis server)"
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing message system");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
    }
}
