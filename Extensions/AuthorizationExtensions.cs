using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace KeycloackDemoApi.Extensions;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddKeycloakAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Define policies based on Keycloak roles
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireRole("admin"));

            options.AddPolicy("UserOrAdmin", policy =>
                policy.RequireRole("user", "admin"));

            options.AddPolicy("ManagerOnly", policy =>
                policy.RequireRole("manager"));

            // Custom policy for specific client roles
            options.AddPolicy("ApiClientRole", policy =>
                policy.RequireAssertion(context =>
                    context.User.HasClaim(ClaimTypes.Role, "dotnet-api-client:api-user")));

            // Permission-based policy
            options.AddPolicy("ReadPermission", policy =>
                policy.Requirements.Add(new PermissionRequirement("read")));

            options.AddPolicy("WritePermission", policy =>
                policy.Requirements.Add(new PermissionRequirement("write")));
        });

        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}

// Custom permission requirement
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }
}

// Custom permission handler
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, 
        PermissionRequirement requirement)
    {
        // Check if user has the required permission in their claims
        var permissionClaim = context.User.FindFirst("permissions")?.Value;
        
        if (!string.IsNullOrEmpty(permissionClaim))
        {
            try
            {
                var permissions = System.Text.Json.JsonSerializer.Deserialize<string[]>(permissionClaim);
                if (permissions != null && permissions.Contains(requirement.Permission))
                {
                    context.Succeed(requirement);
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Log error if needed
            }
        }

        // Alternative: Check for specific role-permission mapping
        var userRoles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value);
        
        if (HasPermissionForRoles(userRoles, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasPermissionForRoles(IEnumerable<string> roles, string permission)
    {
        // Define role-permission mappings
        var rolePermissions = new Dictionary<string, string[]>
        {
            ["admin"] = ["read", "write", "delete", "manage"],
            ["manager"] = ["read", "write", "approve"],
            ["user"] = ["read"],
            ["editor"] = ["read", "write"]
        };

        return roles.Any(role => 
            rolePermissions.ContainsKey(role) && 
            rolePermissions[role].Contains(permission));
    }
}