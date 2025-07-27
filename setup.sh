#!/bin/bash

# Keycloak Setup Script
# This script configures Keycloak realm, client, roles, and LDAP integration

KEYCLOAK_URL="http://localhost:8080"
ADMIN_USER="admin"
ADMIN_PASSWORD="admin_password"
REALM_NAME="dotnet-api-realm"
CLIENT_ID="dotnet-api-client"

echo "🚀 Starting Keycloak configuration..."

# Wait for Keycloak to be ready
echo "⏳ Waiting for Keycloak to be ready..."
until $(curl --output /dev/null --silent --head --fail $KEYCLOAK_URL/health/ready); do
    printf '.'
    sleep 5
done
echo "✅ Keycloak is ready!"

# Get admin access token
echo "🔑 Getting admin access token..."
ADMIN_TOKEN=$(curl -s -X POST "$KEYCLOAK_URL/realms/master/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "username=$ADMIN_USER" \
  -d "password=$ADMIN_PASSWORD" \
  -d "grant_type=password" \
  -d "client_id=admin-cli" | jq -r '.access_token')

if [ "$ADMIN_TOKEN" = "null" ]; then
    echo "❌ Failed to get admin token"
    exit 1
fi

echo "✅ Got admin access token"

# Create realm
echo "🏰 Creating realm: $REALM_NAME"
curl -s -X POST "$KEYCLOAK_URL/admin/realms" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "realm": "'"$REALM_NAME"'",
    "displayName": ".NET API Realm",
    "enabled": true,
    "registrationAllowed": false,
    "loginWithEmailAllowed": true,
    "duplicateEmailsAllowed": false,
    "resetPasswordAllowed": true,
    "editUsernameAllowed": false,
    "bruteForceProtected": true,
    "rememberMe": true,
    "verifyEmail": false,
    "loginTheme": "keycloak",
    "accountTheme": "keycloak",
    "adminTheme": "keycloak",
    "emailTheme": "keycloak"
  }'

echo "✅ Realm created successfully"

# Create client
echo "🔧 Creating client: $CLIENT_ID"
curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM_NAME/clients" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "'"$CLIENT_ID"'",
    "name": ".NET API Client",
    "description": "Client for .NET Core API",
    "enabled": true,
    "clientAuthenticatorType": "client-secret",
    "secret": "your-client-secret-here",
    "standardFlowEnabled": true,
    "implicitFlowEnabled": false,
    "directAccessGrantsEnabled": true,
    "serviceAccountsEnabled": true,
    "publicClient": false,
    "frontchannelLogout": false,
    "protocol": "openid-connect",
    "attributes": {
      "saml.assertion.signature": "false",
      "saml.force.post.binding": "false",
      "saml.multivalued.roles": "false",
      "saml.encrypt": "false",
      "saml.server.signature": "false",
      "saml.server.signature.keyinfo.ext": "false",
      "exclude.session.state.from.auth.response": "false",
      "saml_force_name_id_format": "false",
      "saml.client.signature": "false",
      "tls.client.certificate.bound.access.tokens": "false",
      "saml.authnstatement": "false",
      "display.on.consent.screen": "false",
      "saml.onetimeuse.condition": "false"
    },
    "authenticationFlowBindingOverrides": {},
    "fullScopeAllowed": true,
    "nodeReRegistrationTimeout": -1,
    "defaultClientScopes": [
      "web-origins",
      "role_list",
      "profile",
      "roles",
      "email"
    ],
    "optionalClientScopes": [
      "address",
      "phone",
      "offline_access",
      "microprofile-jwt"
    ]
  }'

echo "✅ Client created successfully"

# Create realm roles
echo "👥 Creating realm roles..."

roles=("admin" "user" "manager" "editor")

for role in "${roles[@]}"; do
    echo "Creating role: $role"
    curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM_NAME/roles" \
      -H "Authorization: Bearer $ADMIN_TOKEN" \
      -H "Content-Type: application/json" \
      -d '{
        "name": "'"$role"'",
        "description": "'"$role"' role",
        "composite": false,
        "clientRole": false,
        "containerId": "'"$REALM_NAME"'"
      }'
done

echo "✅ Realm roles created successfully"

# Create test users
echo "👤 Creating test users..."

# Admin user
curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM_NAME/users" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testadmin",
    "email": "admin@example.com",
    "firstName": "Test",
    "lastName": "Admin",
    "enabled": true,
    "emailVerified": true,
    "credentials": [{
      "type": "password",
      "value": "testpassword",
      "temporary": false
    }]
  }'

# Regular user
curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM_NAME/users" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "email": "user@example.com",
    "firstName": "Test",
    "lastName": "User",
    "enabled": true,
    "emailVerified": true,
    "credentials": [{
      "type": "password",
      "value": "testpassword",
      "temporary": false
    }]
  }'

echo "✅ Test users created successfully"

# Get user IDs and assign roles
echo "🔗 Assigning roles to users..."

# Get admin user ID
ADMIN_USER_ID=$(curl -s "$KEYCLOAK_URL/admin/realms/$REALM_NAME/users?username=testadmin" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq -r '.[0].id')

# Get regular user ID
REGULAR_USER_ID=$(curl -s "$KEYCLOAK_URL/admin/realms/$REALM_NAME/users?username=testuser" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq -r '.[0].id')

# Get admin role
ADMIN_ROLE=$(curl -s "$KEYCLOAK_URL/admin/realms/$REALM_NAME/roles/admin" \
  -H "Authorization: Bearer $ADMIN_TOKEN")

# Get user role
USER_ROLE=$(curl -s "$KEYCLOAK_URL/admin/realms/$REALM_NAME/roles/user" \
  -H "Authorization: Bearer $ADMIN_TOKEN")

# Assign admin role to test admin
curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM_NAME/users/$ADMIN_USER_ID/role-mappings/realm" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "[$ADMIN_ROLE]"

# Assign user role to regular user
curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM_NAME/users/$REGULAR_USER_ID/role-mappings/realm" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "[$USER_ROLE]"

echo "✅ Roles assigned successfully"

# Configure LDAP User Federation
echo "🔌 Configuring LDAP User Federation..."

curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM_NAME/components" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "ldap-provider",
    "providerId": "ldap",
    "providerType": "org.keycloak.storage.UserStorageProvider",
    "parentId": "'"$REALM_NAME"'",
    "config": {
      "enabled": ["true"],
      "priority": ["0"],
      "fullSyncPeriod": ["-1"],
      "changedSyncPeriod": ["-1"],
      "cachePolicy": ["DEFAULT"],
      "batchSizeForSync": ["1000"],
      "editMode": ["READ_ONLY"],
      "syncRegistrations": ["false"],
      "vendor": ["other"],
      "usernameLDAPAttribute": ["uid"],
      "rdnLDAPAttribute": ["uid"],
      "uuidLDAPAttribute": ["entryUUID"],
      "userObjectClasses": ["inetOrgPerson, organizationalPerson"],
      "connectionUrl": ["ldap://openldap:389"],
      "usersDn": ["ou=people,dc=example,dc=org"],
      "authType": ["simple"],
      "bindDn": ["cn=admin,dc=example,dc=org"],
      "bindCredential": ["admin_password"],
      "searchScope": ["1"],
      "validatePasswordPolicy": ["false"],
      "trustEmail": ["false"],
      "useTruststoreSpi": ["ldapsOnly"],
      "connectionPooling": ["true"],
      "pagination": ["true"],
      "allowKerberosAuthentication": ["false"],
      "debug": ["false"],
      "useKerberosForPasswordAuthentication": ["false"]
    }
  }'

echo "✅ LDAP User Federation configured"

echo "🎉 Keycloak configuration completed!"
echo ""
echo "📋 Configuration Summary:"
echo "   Realm: $REALM_NAME"
echo "   Client ID: $CLIENT_ID"
echo "   Admin Console: $KEYCLOAK_URL/admin/"
echo "   Realm URL: $KEYCLOAK_URL/realms/$REALM_NAME"
echo ""
echo "👤 Test Users:"
echo "   Admin: testadmin / testpassword (admin role)"
echo "   User: testuser / testpassword (user role)"
echo ""
echo "🔗 Token Endpoint:"
echo "   $KEYCLOAK_URL/realms/$REALM_NAME/protocol/openid-connect/token"
echo ""
echo "💡 To get a token, use:"
echo "   curl -X POST '$KEYCLOAK_URL/realms/$REALM_NAME/protocol/openid-connect/token' \\"
echo "     -H 'Content-Type: application/x-www-form-urlencoded' \\"
echo "     -d 'grant_type=password' \\"
echo "     -d 'client_id=$CLIENT_ID' \\"
echo "     -d 'username=testadmin' \\"
echo "     -d 'password=testpassword'"