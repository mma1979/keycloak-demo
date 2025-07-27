# .NET Core 9 Web API with Keycloak Authentication

A comprehensive .NET Core 9 Web API implementation with Keycloak integration for authentication, authorization, role-based access control, and LDAP user federation.

## Features

- 🔐 **JWT Authentication** with Keycloak
- 👥 **Role-based Authorization** (Admin, User, Manager, Editor)
- 🔑 **Permission-based Access Control**
- 🌐 **LDAP Integration** for external user management
- 🐳 **Docker Compose** deployment
- 📊 **Swagger Documentation** with JWT support
- 🏥 **Health Checks**
- 🔧 **User Management API**
- 📝 **Comprehensive Logging**

## Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Frontend      │    │   .NET Core 9   │    │    Keycloak     │
│   Application   │◄──►│   Web API       │◄──►│   Auth Server   │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                                                        │
                                                        ▼
                                               ┌─────────────────┐
                                               │   PostgreSQL    │
                                               │   Database      │
                                               └─────────────────┘
                                                        │
                                                        ▼
                                               ┌─────────────────┐
                                               │    OpenLDAP     │
                                               │   Directory     │
                                               └─────────────────┘
```

## Quick Start

### Prerequisites

- Docker and Docker Compose
- .NET 9 SDK (for local development)
- curl or similar HTTP client
- jq (for JSON parsing in setup scripts)

### 1. Clone and Setup

```bash
# Create project structure
mkdir keycloak-dotnet-api
cd keycloak-dotnet-api

# Create API directory
mkdir api
cd api

# Create the .NET project files (copy from artifacts above)
# - KeycloakApi.csproj
# - Program.cs
# - appsettings.json
# - Controllers/
# - Services/
# - Extensions/
# - Configuration/
# - Dockerfile

cd ..
```

### 2. Start Services

```bash
# Start all services
docker-compose up -d

# Check service status
docker-compose ps
```

### 3. Configure Keycloak

```bash
# Make setup script executable
chmod +x keycloak-setup.sh

# Wait for services to be ready (may take 2-3 minutes)
sleep 180

# Run Keycloak configuration
./keycloak-setup.sh
```

### 4. Setup LDAP Test Data

```bash
# Make LDAP setup script executable
chmod +x ldap-setup.sh

# Run LDAP configuration
./ldap-setup.sh
```

### 5. Test the API

```bash
# Get an access token
TOKEN=$(curl -s -X POST 'http://localhost:8080/realms/dotnet-api-realm/protocol/openid-connect/token' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'grant_type=password' \
  -d 'client_id=dotnet-api-client' \
  -d 'username=testadmin' \
  -d 'password=testpassword' | jq -r '.access_token')

# Test authenticated endpoint
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/auth/profile

# Test admin endpoint
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/users
```

## API Endpoints

### Public Endpoints

- `GET /` - API information
- `GET /health` - Health check
- `GET /api/public/info` - Public information

### Authentication Required

- `GET /api/auth/profile` - Get current user profile
- `GET /api/auth/test-permissions` - Test read permission
- `POST /api/auth/test-permissions` - Test write permission

### User Management (Admin Only)

- `GET /api/users` - List all users
- `GET /api/users/{id}` - Get user by ID
- `POST /api/users` - Create new user
- `PUT /api/users/{id}` - Update user
- `DELETE /api/users/{id}` - Delete user
- `POST /api/users/{id}/roles/{roleName}` - Assign role to user
- `DELETE /api/users/{id}/roles/{roleName}` - Remove role from user

### Role Management (Admin Only)

- `GET /api/roles` - List all roles

## User Roles and Permissions

### Default Roles

- **admin**: Full access to all endpoints
- **user**: Basic access to profile and read operations
- **manager**: Extended access including approval operations
- **editor**: Read and write access to content

### Permission Mapping

```csharp
var rolePermissions = new Dictionary<string, string[]>
{
    ["admin"] = ["read", "write", "delete", "manage"],
    ["manager"] = ["read", "write", "approve"],
    ["user"] = ["read"],
    ["editor"] = ["read", "write"]
};
```

## Test Users

### Keycloak Users

- **testadmin** / testpassword (admin role)
- **testuser** / testpassword (user role)

### LDAP Users

- **ldapuser1** / password123 (users group)
- **ldapuser2** / password123 (users group)
- **ldapadmin** / admin123 (admins, managers groups)

## Configuration

### Environment Variables

```env
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://+:5000
Keycloak__Authority=http://keycloak:8080/realms/dotnet-api-realm
Keycloak__Audience=dotnet-api-client
Keycloak__RequireHttpsMetadata=false
```

### appsettings.json

Key configuration sections:

```json
{
  "Keycloak": {
    "Authority": "http://localhost:8080/realms/dotnet-api-realm",
    "Audience": "dotnet-api-client",
    "RequireHttpsMetadata": false,
    "ClientId": "dotnet-api-client",
    "ClientSecret": "your-client-secret-here"
  }
}
```

## Development

### Local Development Setup

```bash
# Install dependencies
dotnet restore

# Run the API locally
dotnet run

# The API will be available at http://localhost:5000
```

### Adding New Policies

```csharp
// In AuthorizationExtensions.cs
options.AddPolicy("CustomPolicy", policy =>
    policy.RequireAssertion(context =>
        // Your custom authorization logic
    ));
```

### Custom Permission Requirements

```csharp
public class CustomRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public CustomRequirement(string permission) => Permission = permission;
}
```

## Monitoring and Management

### Keycloak Admin Console

- URL: http://localhost:8080/admin/
- Username: admin
- Password: admin_password

### LDAP Management

- phpLDAPadmin: http://localhost:8081
- Login DN: cn=admin,dc=example,dc=org
- Password: admin_password

### API Documentation

- Swagger UI: http://localhost:5000
- Health Check: http://localhost:5000/health

## Troubleshooting

### Common Issues

1. **Token Validation Errors**
   ```bash
   # Check Keycloak is accessible
   curl http://localhost:8080/health/ready
   
   # Verify realm configuration
   curl http://localhost:8080/realms/dotnet-api-realm/.well-known/openid_configuration
   ```

2. **LDAP Connection Issues**
   ```bash
   # Test LDAP connectivity
   ldapsearch -H ldap://localhost:389 -D 'cn=admin,dc=example,dc=org' -w 'admin_password' -b 'dc=example,dc=org'
   ```

3. **Service Startup Issues**
   ```bash
   # Check container logs
   docker-compose logs keycloak
   docker-compose logs api
   docker-compose logs postgres
   ```

### Debugging

Enable detailed logging by setting:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore.Authentication": "Debug",
      "Microsoft.AspNetCore.Authorization": "Debug"
    }
  }
}
```

## Production Considerations

### Security Hardening

1. **Enable HTTPS**
   ```json
   {
     "Keycloak": {
       "RequireHttpsMetadata": true
     }
   }
   ```

2. **Use Strong Secrets**
   - Generate strong client secrets
   - Use secure passwords for admin accounts
   - Enable certificate-based authentication

3. **Network Security**
   - Use internal networks for service communication
   - Implement proper firewall rules
   - Enable SSL/TLS for all connections

### Performance Optimization

1. **Token Caching**
   - Implement Redis for distributed caching
   - Configure appropriate cache expiration

2. **Database Optimization**
   - Use connection pooling
   - Implement proper indexing
   - Monitor query performance

3. **Resource Limits**
   ```yaml
   # In docker-compose.yml
   deploy:
     resources:
       limits:
         cpus: '1.0'
         memory: 512M
   ```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.