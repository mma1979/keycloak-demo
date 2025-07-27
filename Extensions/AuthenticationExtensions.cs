using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using KeycloackDemoApi.Configuration;

namespace KeycloackDemoApi.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddKeycloakAuthentication(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        var keycloakOptions = configuration.GetSection(KeycloakOptions.SectionName)
            .Get<KeycloakOptions>() ?? throw new InvalidOperationException("Keycloak configuration is missing");

        services.Configure<KeycloakOptions>(configuration.GetSection(KeycloakOptions.SectionName));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloakOptions.Authority;
                options.Audience = keycloakOptions.Audience;
                options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;
                
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = keycloakOptions.ValidateAudience,
                    ValidAudience = keycloakOptions.Audience,
                    ValidateIssuer = keycloakOptions.ValidateIssuer,
                    ValidIssuer = keycloakOptions.Authority,
                    ValidateLifetime = keycloakOptions.ValidateLifetime,
                    ClockSkew = keycloakOptions.ClockSkew,
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        // Extract roles from realm_access and resource_access claims
                        var claimsIdentity = context.Principal?.Identity as ClaimsIdentity;
                        if (claimsIdentity != null)
                        {
                            ExtractKeycloakRoles(claimsIdentity);
                        }
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    private static void ExtractKeycloakRoles(ClaimsIdentity claimsIdentity)
    {
        // Extract realm roles
        var realmAccessClaim = claimsIdentity.FindFirst("realm_access")?.Value;
        if (!string.IsNullOrEmpty(realmAccessClaim))
        {
            try
            {
                var realmAccess = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(realmAccessClaim);
                if (realmAccess != null && realmAccess.ContainsKey("roles"))
                {
                    var rolesElement = (System.Text.Json.JsonElement)realmAccess["roles"];
                    if (rolesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var role in rolesElement.EnumerateArray())
                        {
                            claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role.GetString() ?? ""));
                        }
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Log error if needed
            }
        }

        // Extract resource/client roles
        var resourceAccessClaim = claimsIdentity.FindFirst("resource_access")?.Value;
        if (!string.IsNullOrEmpty(resourceAccessClaim))
        {
            try
            {
                var resourceAccess = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(resourceAccessClaim);
                if (resourceAccess != null)
                {
                    foreach (var resource in resourceAccess)
                    {
                        var resourceElement = (System.Text.Json.JsonElement)resource.Value;
                        if (resourceElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                            resourceElement.TryGetProperty("roles", out var rolesProperty) &&
                            rolesProperty.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var role in rolesProperty.EnumerateArray())
                            {
                                claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, 
                                    $"{resource.Key}:{role.GetString()}"));
                            }
                        }
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Log error if needed
            }
        }
    }
}