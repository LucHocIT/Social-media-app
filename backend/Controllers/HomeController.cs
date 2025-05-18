using Microsoft.AspNetCore.Mvc;

namespace SocialApp.Controllers;

[ApiController]
[Route("")]
public class HomeController : ControllerBase
{
    [HttpGet]
    public ActionResult<string> Index()
    {
        return Ok("SocialApp API is running!");
    }
}
