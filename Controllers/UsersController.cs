using System.Security.Claims;
using KeycloackDemoApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeycloackDemoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IKeycloakService _keycloakService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IKeycloakService keycloakService, ILogger<UsersController> logger)
    {
        _keycloakService = keycloakService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<IEnumerable<KeycloakUser>>> GetUsers()
    {
        try
        {
            var users = await _keycloakService.GetUsersAsync();
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<ActionResult<KeycloakUser>> GetUser(string id)
    {
        try
        {
            // Users can only access their own data unless they're admin
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User.IsInRole("admin");

            if (!isAdmin && currentUserId != id)
            {
                return Forbid();
            }

            var user = await _keycloakService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<KeycloakUser>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var user = await _keycloakService.CreateUserAsync(request);
            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            // Users can only update their own data unless they're admin
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User.IsInRole("admin");

            if (!isAdmin && currentUserId != id)
            {
                return Forbid();
            }

            await _keycloakService.UpdateUserAsync(id, request);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        try
        {
            await _keycloakService.DeleteUserAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{id}/roles/{roleName}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AssignRole(string id, string roleName)
    {
        try
        {
            await _keycloakService.AssignRoleToUserAsync(id, roleName);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role {RoleName} to user {UserId}", roleName, id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}/roles/{roleName}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RemoveRole(string id, string roleName)
    {
        try
        {
            await _keycloakService.RemoveRoleFromUserAsync(id, roleName);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing role {RoleName} from user {UserId}", roleName, id);
            return StatusCode(500, "Internal server error");
        }
    }
}
