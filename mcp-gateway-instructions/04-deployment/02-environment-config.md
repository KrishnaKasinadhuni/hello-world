# 02: Environment Configuration

## Objective

Configure environment variables and secrets management for the MCP Gateway, including development and production configurations, secret management, and configuration validation.

## Prerequisites

- Completed: 04-deployment/01-docker-compose-full.md
- Understanding of environment variables
- Knowledge of secret management best practices

## Implementation Steps

### Step 1: Create Complete Environment Template

#### .env.example

Create comprehensive environment template:

```env
# Application Configuration
GATEWAY_HOST=0.0.0.0
GATEWAY_PORT=8000
ENVIRONMENT=development
DEBUG=True

# Database Configuration
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_DB=mcp_gateway
POSTGRES_PORT=5432
DATABASE_URL=postgresql://postgres:postgres@postgres:5432/mcp_gateway
DATABASE_POOL_SIZE=10
DATABASE_MAX_OVERFLOW=20

# Redis Configuration
REDIS_HOST=redis
REDIS_PORT=6379
REDIS_PASSWORD=
REDIS_DB=0

# JWT Configuration
JWT_SECRET_KEY=your-secret-key-change-in-production-minimum-32-characters
JWT_ALGORITHM=HS256
JWT_ACCESS_TOKEN_EXPIRE_MINUTES=30
JWT_REFRESH_TOKEN_EXPIRE_DAYS=7

# TLS Configuration
TLS_ENABLED=True
TLS_CERT_PATH=/etc/nginx/ssl/cert.pem
TLS_KEY_PATH=/etc/nginx/ssl/key.pem
TLS_DOMAIN=localhost

# Nginx Configuration
NGINX_HTTP_PORT=80
NGINX_HTTPS_PORT=443

# Rate Limiting
RATE_LIMIT_ENABLED=True
RATE_LIMIT_PER_MINUTE=60
RATE_LIMIT_PER_HOUR=1000
RATE_LIMIT_PER_DAY=10000

# Logging Configuration
LOG_LEVEL=INFO
LOG_FILE_PATH=/var/log/mcp-gateway/gateway.log

# MCP Server Configuration
MCP_SERVER_NETWORK=mcp_servers
MCP_SERVER_MAX_INSTANCES=10
MCP_SERVER_TIMEOUT=30
MCP_SERVER_MAX_MEMORY=512m
MCP_SERVER_MAX_CPU=0.5

# CORS Configuration
CORS_ORIGINS=*

# Security Configuration
ALLOWED_HOSTS=localhost,127.0.0.1
SECURE_COOKIES=True
SESSION_COOKIE_SECURE=True
CSRF_PROTECTION=True
```

### Step 2: Create Production Environment Template

#### .env.production.example

Create production environment template:

```env
# Application Configuration
GATEWAY_HOST=0.0.0.0
GATEWAY_PORT=8000
ENVIRONMENT=production
DEBUG=False

# Database Configuration (use strong passwords in production)
POSTGRES_USER=mcp_gateway_user
POSTGRES_PASSWORD=CHANGE_THIS_STRONG_PASSWORD
POSTGRES_DB=mcp_gateway
POSTGRES_PORT=5432
DATABASE_URL=postgresql://mcp_gateway_user:CHANGE_THIS_STRONG_PASSWORD@postgres:5432/mcp_gateway
DATABASE_POOL_SIZE=20
DATABASE_MAX_OVERFLOW=40

# Redis Configuration (use strong passwords in production)
REDIS_HOST=redis
REDIS_PORT=6379
REDIS_PASSWORD=CHANGE_THIS_STRONG_PASSWORD
REDIS_DB=0

# JWT Configuration (use strong secret key in production)
JWT_SECRET_KEY=CHANGE_THIS_TO_A_VERY_STRONG_RANDOM_SECRET_KEY_MINIMUM_64_CHARACTERS
JWT_ALGORITHM=HS256
JWT_ACCESS_TOKEN_EXPIRE_MINUTES=15
JWT_REFRESH_TOKEN_EXPIRE_DAYS=30

# TLS Configuration
TLS_ENABLED=True
TLS_CERT_PATH=/etc/nginx/ssl/cert.pem
TLS_KEY_PATH=/etc/nginx/ssl/key.pem
TLS_DOMAIN=yourdomain.com

# Nginx Configuration
NGINX_HTTP_PORT=80
NGINX_HTTPS_PORT=443

# Rate Limiting (stricter in production)
RATE_LIMIT_ENABLED=True
RATE_LIMIT_PER_MINUTE=30
RATE_LIMIT_PER_HOUR=500
RATE_LIMIT_PER_DAY=5000

# Logging Configuration
LOG_LEVEL=WARNING
LOG_FILE_PATH=/var/log/mcp-gateway/gateway.log

# MCP Server Configuration
MCP_SERVER_NETWORK=mcp_servers
MCP_SERVER_MAX_INSTANCES=50
MCP_SERVER_TIMEOUT=60
MCP_SERVER_MAX_MEMORY=1g
MCP_SERVER_MAX_CPU=1.0

# CORS Configuration (restrict in production)
CORS_ORIGINS=https://yourdomain.com,https://www.yourdomain.com

# Security Configuration
ALLOWED_HOSTS=yourdomain.com,www.yourdomain.com
SECURE_COOKIES=True
SESSION_COOKIE_SECURE=True
CSRF_PROTECTION=True
```

### Step 3: Create Secret Generation Script

#### scripts/generate-secrets.sh

Create script to generate secure secrets:

```bash
#!/bin/bash

set -e

echo "Generating secure secrets..."

# Generate JWT secret key
JWT_SECRET=$(openssl rand -hex 32)
echo "JWT_SECRET_KEY=$JWT_SECRET"

# Generate database password
DB_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)
echo "POSTGRES_PASSWORD=$DB_PASSWORD"

# Generate Redis password
REDIS_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)
echo "REDIS_PASSWORD=$REDIS_PASSWORD"

echo "Secrets generated successfully!"
echo "Please add these to your .env file"
```

Make it executable:

```bash
chmod +x scripts/generate-secrets.sh
```

### Step 4: Create Configuration Validation

#### gateway/src/config/validation.py

Create configuration validation:

```python
"""Configuration validation."""
import os
import re
from typing import List, Tuple
from src.config import settings


class ConfigValidator:
    """Validate configuration settings."""

    def __init__(self):
        """Initialize config validator."""
        self.errors: List[str] = []

    def validate(self) -> Tuple[bool, List[str]]:
        """Validate all configuration settings."""
        self.errors = []

        # Validate JWT secret key
        self._validate_jwt_secret()

        # Validate database URL
        self._validate_database_url()

        # Validate Redis configuration
        self._validate_redis_config()

        # Validate TLS configuration
        self._validate_tls_config()

        # Validate rate limiting
        self._validate_rate_limiting()

        return len(self.errors) == 0, self.errors

    def _validate_jwt_secret(self):
        """Validate JWT secret key."""
        secret = settings.JWT_SECRET_KEY
        if not secret or secret == "change-me-in-production":
            self.errors.append("JWT_SECRET_KEY must be set and not use default value")
        if len(secret) < 32:
            self.errors.append("JWT_SECRET_KEY must be at least 32 characters")

    def _validate_database_url(self):
        """Validate database URL."""
        db_url = settings.DATABASE_URL
        if not db_url:
            self.errors.append("DATABASE_URL must be set")
        if "postgres" not in db_url and "postgresql" not in db_url:
            self.errors.append("DATABASE_URL must be a PostgreSQL connection string")

    def _validate_redis_config(self):
        """Validate Redis configuration."""
        if not settings.REDIS_HOST:
            self.errors.append("REDIS_HOST must be set")
        if not isinstance(settings.REDIS_PORT, int):
            self.errors.append("REDIS_PORT must be an integer")

    def _validate_tls_config(self):
        """Validate TLS configuration."""
        if settings.TLS_ENABLED:
            if not settings.TLS_CERT_PATH:
                self.errors.append("TLS_CERT_PATH must be set when TLS_ENABLED is True")
            if not settings.TLS_KEY_PATH:
                self.errors.append("TLS_KEY_PATH must be set when TLS_ENABLED is True")

    def _validate_rate_limiting(self):
        """Validate rate limiting configuration."""
        if settings.RATE_LIMIT_PER_MINUTE > settings.RATE_LIMIT_PER_HOUR:
            self.errors.append("RATE_LIMIT_PER_MINUTE cannot be greater than RATE_LIMIT_PER_HOUR")
        if settings.RATE_LIMIT_PER_HOUR > settings.RATE_LIMIT_PER_DAY:
            self.errors.append("RATE_LIMIT_PER_HOUR cannot be greater than RATE_LIMIT_PER_DAY")
```

### Step 5: Update Configuration to Validate on Startup

#### gateway/src/config.py

Update configuration to validate on startup:

```python
# Add to end of file
from src.config.validation import ConfigValidator

# Validate configuration on import (in production)
if settings.ENVIRONMENT == "production":
    validator = ConfigValidator()
    is_valid, errors = validator.validate()
    if not is_valid:
        raise ValueError(f"Configuration validation failed: {', '.join(errors)}")
```

### Step 6: Create Environment Setup Script

#### scripts/setup-env.sh

Create environment setup script:

```bash
#!/bin/bash

set -e

ENV_FILE=${1:-.env}

echo "Setting up environment configuration..."

# Check if .env exists
if [ -f "$ENV_FILE" ]; then
    echo "Warning: $ENV_FILE already exists. Backing up to ${ENV_FILE}.bak"
    cp "$ENV_FILE" "${ENV_FILE}.bak"
fi

# Copy from example
if [ "$ENV_FILE" == ".env.production" ]; then
    cp .env.production.example "$ENV_FILE"
else
    cp .env.example "$ENV_FILE"
fi

# Generate secrets if in production
if [ "$ENV_FILE" == ".env.production" ]; then
    echo "Generating secure secrets..."
    ./scripts/generate-secrets.sh >> "$ENV_FILE"
fi

echo "Environment configuration setup complete!"
echo "Please edit $ENV_FILE and update the values as needed"
```

Make it executable:

```bash
chmod +x scripts/setup-env.sh
```

## Testing

### Test Configuration Validation

Test configuration validation:

```bash
# Test with invalid configuration
export JWT_SECRET_KEY=short
python -c "from src.config import settings; from src.config.validation import ConfigValidator; v = ConfigValidator(); print(v.validate())"

# Test with valid configuration
export JWT_SECRET_KEY=$(openssl rand -hex 32)
python -c "from src.config import settings; from src.config.validation import ConfigValidator; v = ConfigValidator(); print(v.validate())"
```

## Verification

1. **Environment Template**: Environment templates are created
2. **Secret Generation**: Secrets can be generated securely
3. **Configuration Validation**: Configuration is validated on startup
4. **Environment Setup**: Environment setup script works

## Troubleshooting

### Issue: Configuration validation fails

**Solution**: Check configuration values and fix errors:
```bash
python -c "from src.config.validation import ConfigValidator; v = ConfigValidator(); print(v.validate())"
```

### Issue: Secrets not secure

**Solution**: Use the secret generation script:
```bash
./scripts/generate-secrets.sh
```

## Next Steps

After completing this instruction, proceed to:
- **03-health-checks.md**: Configure health checks

