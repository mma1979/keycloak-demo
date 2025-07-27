using Microsoft.AspNetCore.Mvc;

namespace KeycloackDemoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PublicController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
    }

    [HttpGet("info")]
    public IActionResult Info()
    {
        return Ok(new
        {
            Application = "Keycloak API",
            Version = "1.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            Timestamp = DateTime.UtcNow
        });
    }
}