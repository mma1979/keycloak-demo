using System.Text;
using KeycloackDemoApi.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace KeycloackDemoApi.Services;

public interface IKeycloakService
{
    Task<string> GetAdminAccessTokenAsync();
    Task<IEnumerable<KeycloakUser>> GetUsersAsync();
    Task<KeycloakUser?> GetUserByIdAsync(string userId);
    Task<KeycloakUser> CreateUserAsync(CreateUserRequest request);
    Task UpdateUserAsync(string userId, UpdateUserRequest request);
    Task DeleteUserAsync(string userId);
    Task<IEnumerable<KeycloakRole>> GetRolesAsync();
    Task AssignRoleToUserAsync(string userId, string roleName);
    Task RemoveRoleFromUserAsync(string userId, string roleName);
}

public class KeycloakService : IKeycloakService
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _options;
    private readonly ILogger<KeycloakService> _logger;
    private string? _cachedAccessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public KeycloakService(
        HttpClient httpClient, 
        IOptions<KeycloakOptions> options, 
        ILogger<KeycloakService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetAdminAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_cachedAccessToken) && DateTime.UtcNow < _tokenExpiry)
        {
            return _cachedAccessToken;
        }

        var tokenEndpoint = $"{_options.Authority.Replace("/realms/" + _options.Realm, "")}/realms/master/protocol/openid-connect/token";
        
        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", "admin-cli"),
            new KeyValuePair<string, string>("username", "admin"),
            new KeyValuePair<string, string>("password", "admin_password")
        });

        var response = await _httpClient.PostAsync(tokenEndpoint, formData);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonConvert.DeserializeObject<KeycloakTokenResponse>(jsonResponse);

        if (tokenResponse?.AccessToken != null)
        {
            _cachedAccessToken = tokenResponse.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 30); // 30 seconds buffer
            return _cachedAccessToken;
        }

        throw new InvalidOperationException("Failed to obtain admin access token");
    }

    public async Task<IEnumerable<KeycloakUser>> GetUsersAsync()
    {
        var token = await GetAdminAccessTokenAsync();
        var url = $"{_options.AdminUrl}/users";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var users = JsonConvert.DeserializeObject<IEnumerable<KeycloakUser>>(jsonResponse);

        return users ?? new List<KeycloakUser>();
    }

    public async Task<KeycloakUser?> GetUserByIdAsync(string userId)
    {
        var token = await GetAdminAccessTokenAsync();
        var url = $"{_options.AdminUrl}/users/{userId}";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var response = await _httpClient.GetAsync(url);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<KeycloakUser>(jsonResponse);
    }

    public async Task<KeycloakUser> CreateUserAsync(CreateUserRequest request)
    {
        var token = await GetAdminAccessTokenAsync();
        var url = $"{_options.AdminUrl}/users";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var userPayload = new
        {
            username = request.Username,
            email = request.Email,
            firstName = request.FirstName,
            lastName = request.LastName,
            enabled = request.Enabled,
            emailVerified = request.EmailVerified,
            credentials = request.Password != null ? new[]
            {
                new
                {
                    type = "password",
                    value = request.Password,
                    temporary = request.TemporaryPassword
                }
            } : null
        };

        var json = JsonConvert.SerializeObject(userPayload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        // Extract user ID from Location header
        var locationHeader = response.Headers.Location?.ToString();
        var userId = locationHeader?.Split('/').Last();

        if (string.IsNullOrEmpty(userId))
        {
            throw new InvalidOperationException("Failed to extract user ID from response");
        }

        var createdUser = await GetUserByIdAsync(userId);
        return createdUser ?? throw new InvalidOperationException("Failed to retrieve created user");
    }

    public async Task UpdateUserAsync(string userId, UpdateUserRequest request)
    {
        var token = await GetAdminAccessTokenAsync();
        var url = $"{_options.AdminUrl}/users/{userId}";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var userPayload = new
        {
            email = request.Email,
            firstName = request.FirstName,
            lastName = request.LastName,
            enabled = request.Enabled
        };

        var json = JsonConvert.SerializeObject(userPayload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PutAsync(url, content);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteUserAsync(string userId)
    {
        var token = await GetAdminAccessTokenAsync();
        var url = $"{_options.AdminUrl}/users/{userId}";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var response = await _httpClient.DeleteAsync(url);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IEnumerable<KeycloakRole>> GetRolesAsync()
    {
        var token = await GetAdminAccessTokenAsync();
        var url = $"{_options.AdminUrl}/roles";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var roles = JsonConvert.DeserializeObject<IEnumerable<KeycloakRole>>(jsonResponse);

        return roles ?? new List<KeycloakRole>();
    }

    public async Task AssignRoleToUserAsync(string userId, string roleName)
    {
        var token = await GetAdminAccessTokenAsync();
        
        // First, get the role
        var roles = await GetRolesAsync();
        var role = roles.FirstOrDefault(r => r.Name == roleName);
        
        if (role == null)
        {
            throw new ArgumentException($"Role '{roleName}' not found");
        }

        var url = $"{_options.AdminUrl}/users/{userId}/role-mappings/realm";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var rolePayload = new[] { role };
        var json = JsonConvert.SerializeObject(rolePayload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveRoleFromUserAsync(string userId, string roleName)
    {
        var token = await GetAdminAccessTokenAsync();
        
        // First, get the role
        var roles = await GetRolesAsync();
        var role = roles.FirstOrDefault(r => r.Name == roleName);
        
        if (role == null)
        {
            throw new ArgumentException($"Role '{roleName}' not found");
        }

        var url = $"{_options.AdminUrl}/users/{userId}/role-mappings/realm";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var rolePayload = new[] { role };
        var json = JsonConvert.SerializeObject(rolePayload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Delete, url)
        {
            Content = content
        };

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}

// DTOs
public class KeycloakTokenResponse
{
    [JsonProperty("access_token")]
    public string? AccessToken { get; set; }

    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonProperty("token_type")]
    public string? TokenType { get; set; }
}

public class KeycloakUser
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("username")]
    public string? Username { get; set; }

    [JsonProperty("email")]
    public string? Email { get; set; }

    [JsonProperty("firstName")]
    public string? FirstName { get; set; }

    [JsonProperty("lastName")]
    public string? LastName { get; set; }

    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    [JsonProperty("emailVerified")]
    public bool EmailVerified { get; set; }

    [JsonProperty("createdTimestamp")]
    public long CreatedTimestamp { get; set; }
}

public class KeycloakRole
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("composite")]
    public bool Composite { get; set; }
}

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool EmailVerified { get; set; } = false;
    public string? Password { get; set; }
    public bool TemporaryPassword { get; set; } = true;
}

public class UpdateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}