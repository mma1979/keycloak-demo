#!/bin/bash

# LDAP Test Data Setup Script
# This script adds test users and groups to OpenLDAP

LDAP_HOST="localhost"
LDAP_PORT="389"
LDAP_ADMIN_DN="cn=admin,dc=example,dc=org"
LDAP_ADMIN_PASSWORD="admin_password"
BASE_DN="dc=example,dc=org"

echo "🔧 Setting up LDAP test data..."

# Wait for LDAP to be ready
echo "⏳ Waiting for LDAP to be ready..."
until ldapsearch -H ldap://$LDAP_HOST:$LDAP_PORT -D "$LDAP_ADMIN_DN" -w "$LDAP_ADMIN_PASSWORD" -b "$BASE_DN" -s base > /dev/null 2>&1; do
    printf '.'
    sleep 5
done
echo "✅ LDAP is ready!"

# Create LDIF file for organizational units
cat > /tmp/ou.ldif << EOF
dn: ou=people,dc=example,dc=org
objectClass: organizationalUnit
ou: people

dn: ou=groups,dc=example,dc=org
objectClass: organizationalUnit
ou: groups
EOF

# Create LDIF file for test users
cat > /tmp/users.ldif << EOF
dn: uid=ldapuser1,ou=people,dc=example,dc=org
objectClass: inetOrgPerson
objectClass: posixAccount
objectClass: shadowAccount
uid: ldapuser1
sn: User1
givenName: LDAP
cn: LDAP User1
displayName: LDAP User1
uidNumber: 10001
gidNumber: 5000
userPassword: {SSHA}password123
gecos: LDAP User1
loginShell: /bin/bash
homeDirectory: /home/ldapuser1
mail: ldapuser1@example.org

dn: uid=ldapuser2,ou=people,dc=example,dc=org
objectClass: inetOrgPerson
objectClass: posixAccount
objectClass: shadowAccount
uid: ldapuser2
sn: User2
givenName: LDAP
cn: LDAP User2
displayName: LDAP User2
uidNumber: 10002
gidNumber: 5000
userPassword: {SSHA}password123
gecos: LDAP User2
loginShell: /bin/bash
homeDirectory: /home/ldapuser2
mail: ldapuser2@example.org

dn: uid=ldapadmin,ou=people,dc=example,dc=org
objectClass: inetOrgPerson
objectClass: posixAccount
objectClass: shadowAccount
uid: ldapadmin
sn: Admin
givenName: LDAP
cn: LDAP Admin
displayName: LDAP Admin
uidNumber: 10003
gidNumber: 5000
userPassword: {SSHA}admin123
gecos: LDAP Admin
loginShell: /bin/bash
homeDirectory: /home/ldapadmin
mail: ldapadmin@example.org
EOF

# Create LDIF file for groups
cat > /tmp/groups.ldif << EOF
dn: cn=users,ou=groups,dc=example,dc=org
objectClass: groupOfNames
cn: users
member: uid=ldapuser1,ou=people,dc=example,dc=org
member: uid=ldapuser2,ou=people,dc=example,dc=org

dn: cn=admins,ou=groups,dc=example,dc=org
objectClass: groupOfNames
cn: admins
member: uid=ldapadmin,ou=people,dc=example,dc=org

dn: cn=managers,ou=groups,dc=example,dc=org
objectClass: groupOfNames
cn: managers
member: uid=ldapadmin,ou=people,dc=example,dc=org
EOF

# Add organizational units
echo "📁 Creating organizational units..."
ldapadd -H ldap://$LDAP_HOST:$LDAP_PORT -D "$LDAP_ADMIN_DN" -w "$LDAP_ADMIN_PASSWORD" -f /tmp/ou.ldif

# Add users
echo "👤 Creating test users..."
ldapadd -H ldap://$LDAP_HOST:$LDAP_PORT -D "$LDAP_ADMIN_DN" -w "$LDAP_ADMIN_PASSWORD" -f /tmp/users.ldif

# Add groups
echo "👥 Creating groups..."
ldapadd -H ldap://$LDAP_HOST:$LDAP_PORT -D "$LDAP_ADMIN_DN" -w "$LDAP_ADMIN_PASSWORD" -f /tmp/groups.ldif

# Clean up temporary files
rm -f /tmp/ou.ldif /tmp/users.ldif /tmp/groups.ldif

echo "✅ LDAP test data setup completed!"
echo ""
echo "📋 LDAP Test Data Summary:"
echo "   Base DN: $BASE_DN"
echo "   Users OU: ou=people,dc=example,dc=org"
echo "   Groups OU: ou=groups,dc=example,dc=org"
echo ""
echo "👤 Test Users:"
echo "   ldapuser1 / password123 (users group)"
echo "   ldapuser2 / password123 (users group)"
echo "   ldapadmin / admin123 (admins, managers groups)"
echo ""
echo "🌐 Access phpLDAPadmin at: http://localhost:8081"
echo "   Login DN: cn=admin,dc=example,dc=org"
echo "   Password: admin_password"
echo ""
echo "🔍 To verify the setup, run:"
echo "   ldapsearch -H ldap://localhost:389 -D 'cn=admin,dc=example,dc=org' -w 'admin_password' -b 'dc=example,dc=org' '(objectClass=*)'"