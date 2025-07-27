using System.Security.Claims;
using KeycloackDemoApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeycloackDemoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IKeycloakService _keycloakService;
    private readonly ILogger<RolesController> _logger;

    public RolesController(IKeycloakService keycloakService, ILogger<RolesController> logger)
    {
        _keycloakService = keycloakService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<IEnumerable<KeycloakRole>>> GetRoles()
    {
        try
        {
            var roles = await _keycloakService.GetRolesAsync();
            return Ok(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving roles");
            return StatusCode(500, "Internal server error");
        }
    }
}

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;

    public AuthController(ILogger<AuthController> logger)
    {
        _logger = logger;
    }

    [HttpGet("profile")]
    [Authorize]
    public IActionResult GetProfile()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToArray();
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst("preferred_username")?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

        return Ok(new
        {
            UserId = userId,
            Username = username,
            Email = email,
            Roles = roles,
            Claims = claims
        });
    }

    [HttpGet("test-permissions")]
    [Authorize(Policy = "ReadPermission")]
    public IActionResult TestReadPermission()
    {
        return Ok("You have read permission!");
    }

    [HttpPost("test-permissions")]
    [Authorize(Policy = "WritePermission")]
    public IActionResult TestWritePermission()
    {
        return Ok("You have write permission!");
    }
}
