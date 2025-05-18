using Microsoft.AspNetCore.Mvc;

namespace SocialApp.Controllers;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    private readonly ILogger<TestController> _logger;

    public TestController(ILogger<TestController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<string> Test()
    {
        _logger.LogInformation("Test endpoint was called!");
        return Ok("Test endpoint is working!");
    }
}
