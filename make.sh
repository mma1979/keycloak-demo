# Makefile for Keycloak .NET API Project

.PHONY: help build up down restart logs clean setup-keycloak setup-ldap test token health

# Default target
help: ## Show this help message
	@echo "Available commands:"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-20s\033[0m %s\n", $$1, $$2}'

# Docker commands
build: ## Build all Docker images
	docker-compose build --no-cache

up: ## Start all services
	docker-compose up -d
	@echo "🚀 Services starting up..."
	@echo "⏳ Waiting for services to be ready (this may take 2-3 minutes)..."
	@sleep 30
	@$(MAKE) wait-for-services

down: ## Stop all services
	docker-compose down

restart: ## Restart all services
	docker-compose restart

logs: ## Show logs from all services
	docker-compose logs -f

logs-api: ## Show API logs
	docker-compose logs -f api

logs-keycloak: ## Show Keycloak logs
	docker-compose logs -f keycloak

clean: ## Remove all containers, volumes, and images
	docker-compose down -v --remove-orphans
	docker system prune -f

# Setup commands
setup: up setup-keycloak setup-ldap ## Complete setup (start services, configure Keycloak and LDAP)
	@echo "✅ Setup completed! API is ready at http://localhost:5000"

setup-keycloak: ## Configure Keycloak realm, client, and roles
	@echo "🔧 Configuring Keycloak..."
	@chmod +x keycloak-setup.sh
	@./keycloak-setup.sh

setup-ldap: ## Setup LDAP test data
	@echo "🔧 Setting up LDAP test data..."
	@chmod +x ldap-setup.sh
	@./ldap-setup.sh

# Development commands
dev: ## Run the API locally (requires .NET 9)
	cd api && dotnet run

build-api: ## Build the API locally
	cd api && dotnet build

test-api: ## Run API tests
	cd api && dotnet test

restore: ## Restore .NET packages
	cd api && dotnet restore

# Utility commands
wait-for-services: ## Wait for all services to be healthy
	@echo "⏳ Waiting for PostgreSQL..."
	@until docker-compose exec -T postgres pg_isready -U keycloak > /dev/null 2>&1; do sleep 2; done
	@echo "⏳ Waiting for Keycloak..."
	@until curl -sf http://localhost:8080/health/ready > /dev/null 2>&1; do sleep 5; done
	@echo "⏳ Waiting for LDAP..."
	@until docker-compose exec -T openldap ldapsearch -H ldap://localhost:389 -D 'cn=admin,dc=example,dc=org' -w 'admin_password' -b 'dc=example,dc=org' -s base > /dev/null 2>&1; do sleep 2; done
	@echo "⏳ Waiting for API..."
	@until curl -sf http://localhost:5000/health > /dev/null 2>&1; do sleep 2; done
	@echo "✅ All services are ready!"

health: ## Check health of all services
	@echo "🔍 Checking service health..."
	@echo "PostgreSQL:"
	@docker-compose exec -T postgres pg_isready -U keycloak || echo "❌ PostgreSQL not ready"
	@echo "Keycloak:"
	@curl -sf http://localhost:8080/health/ready && echo "✅ Keycloak ready" || echo "❌ Keycloak not ready"
	@echo "LDAP:"
	@docker-compose exec -T openldap ldapsearch -H ldap://localhost:389 -D 'cn=admin,dc=example,dc=org' -w 'admin_password' -b 'dc=example,dc=org' -s base > /dev/null 2>&1 && echo "✅ LDAP ready" || echo "❌ LDAP not ready"
	@echo "API:"
	@curl -sf http://localhost:5000/health && echo "✅ API ready" || echo "❌ API not ready"

# Testing commands
token: ## Get an access token for testing
	@echo "🔑 Getting access token..."
	@curl -s -X POST 'http://localhost:8080/realms/dotnet-api-realm/protocol/openid-connect/token' \
		-H 'Content-Type: application/x-www-form-urlencoded' \
		-d 'grant_type=password' \
		-d 'client_id=dotnet-api-client' \
		-d 'username=testadmin' \
		-d 'password=testpassword' | jq -r '.access_token'

test-endpoints: ## Test API endpoints with admin token
	@echo "🧪 Testing API endpoints..."
	@TOKEN=$$(curl -s -X POST 'http://localhost:8080/realms/dotnet-api-realm/protocol/openid-connect/token' \
		-H 'Content-Type: application/x-www-form-urlencoded' \
		-d 'grant_type=password' \
		-d 'client_id=dotnet-api-client' \
		-d 'username=testadmin' \
		-d 'password=testpassword' | jq -r '.access_token'); \
	echo "Testing profile endpoint:"; \
	curl -H "Authorization: Bearer $$TOKEN" http://localhost:5000/api/auth/profile | jq; \
	echo "\nTesting users endpoint:"; \
	curl -H "Authorization: Bearer $$TOKEN" http://localhost:5000/api/users | jq

test-user-token: ## Get token for regular user
	@echo "🔑 Getting user access token..."
	@curl -s -X POST 'http://localhost:8080/realms/dotnet-api-realm/protocol/openid-connect/token' \
		-H 'Content-Type: application/x-www-form-urlencoded' \
		-d 'grant_type=password' \
		-d 'client_id=dotnet-api-client' \
		-d 'username=testuser' \
		-d 'password=testpassword' | jq -r '.access_token'

# Monitoring commands
monitor: ## Show real-time logs from all services
	docker-compose logs -f --tail=100

ps: ## Show running containers
	docker-compose ps

top: ## Show container resource usage
	docker stats

# Backup and restore
backup: ## Backup PostgreSQL database
	@echo "💾 Creating database backup..."
	@docker-compose exec -T postgres pg_dump -U keycloak keycloak > backup_$$(date +%Y%m%d_%H%M%S).sql
	@echo "✅ Backup created: backup_$$(date +%Y%m%d_%H%M%S).sql"

# URL shortcuts
urls: ## Show important URLs
	@echo "📋 Important URLs:"
	@echo "  🌐 API Swagger:          http://localhost:5000"
	@echo "  🔐 Keycloak Admin:       http://localhost:8080/admin/ (admin/admin_password)"
	@echo "  📁 phpLDAPadmin:         http://localhost:8081 (cn=admin,dc=example,dc=org/admin_password)"
	@echo "  ❤️  Health Check:        http://localhost:5000/health"
	@echo "  📊 API Info:             http://localhost:5000/api/public/info"

# Quick development cycle
dev-cycle: down build up setup ## Complete development cycle (clean, build, start, setup)
	@echo "🔄 Development cycle completed!"

# Reset everything
reset: clean dev-cycle ## Reset everything (remove all data and restart fresh)
	@echo "🔄 Complete reset finished!"