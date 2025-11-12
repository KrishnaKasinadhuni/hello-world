# 01: Docker Compose Full Configuration

## Objective

Create the complete Docker Compose configuration for the MCP Gateway with all services, networks, volumes, and security configurations integrated.

## Prerequisites

- Completed: All previous instructions (01-setup through 03-security)
- Docker and Docker Compose installed
- Understanding of Docker Compose configuration
- All security configurations implemented

## Implementation Steps

### Step 1: Create Complete Docker Compose File

#### docker-compose.yml

Create the complete Docker Compose configuration:

```yaml
version: '3.8'

services:
  # PostgreSQL Database
  postgres:
    image: postgres:15-alpine
    container_name: mcp-gateway-postgres
    environment:
      POSTGRES_USER: ${POSTGRES_USER:-postgres}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-postgres}
      POSTGRES_DB: ${POSTGRES_DB:-mcp_gateway}
      POSTGRES_INITDB_ARGS: "-E UTF8"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./scripts/init-db.sql:/docker-entrypoint-initdb.d/init-db.sql:ro
    ports:
      - "${POSTGRES_PORT:-5432}:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER:-postgres}"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s
    networks:
      - gateway_network
    restart: unless-stopped
    deploy:
      resources:
        limits:
          cpus: '1'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 512M

  # Redis Cache
  redis:
    image: redis:7-alpine
    container_name: mcp-gateway-redis
    command: >
      redis-server
      --requirepass ${REDIS_PASSWORD:-}
      --maxmemory 512mb
      --maxmemory-policy allkeys-lru
      --appendonly yes
    ports:
      - "${REDIS_PORT:-6379}:6379"
    volumes:
      - redis_data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "--no-auth-warning", "-a", "${REDIS_PASSWORD:-}", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 5s
    networks:
      - gateway_network
    restart: unless-stopped
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 256M

  # Gateway Service
  gateway:
    build:
      context: ./gateway
      dockerfile: Dockerfile
    container_name: mcp-gateway
    environment:
      - GATEWAY_HOST=0.0.0.0
      - GATEWAY_PORT=8000
      - DATABASE_URL=postgresql://${POSTGRES_USER:-postgres}:${POSTGRES_PASSWORD:-postgres}@postgres:5432/${POSTGRES_DB:-mcp_gateway}
      - REDIS_HOST=redis
      - REDIS_PORT=6379
      - REDIS_PASSWORD=${REDIS_PASSWORD:-}
      - JWT_SECRET_KEY=${JWT_SECRET_KEY:-change-me-in-production}
      - JWT_ALGORITHM=${JWT_ALGORITHM:-HS256}
      - JWT_ACCESS_TOKEN_EXPIRE_MINUTES=${JWT_ACCESS_TOKEN_EXPIRE_MINUTES:-30}
      - JWT_REFRESH_TOKEN_EXPIRE_DAYS=${JWT_REFRESH_TOKEN_EXPIRE_DAYS:-7}
      - ENVIRONMENT=${ENVIRONMENT:-development}
      - DEBUG=${DEBUG:-False}
      - RATE_LIMIT_ENABLED=${RATE_LIMIT_ENABLED:-True}
      - RATE_LIMIT_PER_MINUTE=${RATE_LIMIT_PER_MINUTE:-60}
      - RATE_LIMIT_PER_HOUR=${RATE_LIMIT_PER_HOUR:-1000}
      - LOG_LEVEL=${LOG_LEVEL:-INFO}
      - LOG_FILE_PATH=/var/log/mcp-gateway/gateway.log
      - MCP_SERVER_NETWORK=${MCP_SERVER_NETWORK:-mcp_servers}
      - MCP_SERVER_MAX_INSTANCES=${MCP_SERVER_MAX_INSTANCES:-10}
      - MCP_SERVER_TIMEOUT=${MCP_SERVER_TIMEOUT:-30}
    ports:
      - "${GATEWAY_PORT:-8000}:8000"
    volumes:
      - ./gateway/src:/app/src:ro
      - gateway_logs:/var/log/mcp-gateway
      - /var/run/docker.sock:/var/run/docker.sock:ro
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
    networks:
      - gateway_network
      - gateway_to_servers
    restart: unless-stopped
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 2G
        reservations:
          cpus: '1'
          memory: 1G

  # Nginx Reverse Proxy
  nginx:
    build:
      context: ./nginx
      dockerfile: Dockerfile
    container_name: mcp-gateway-nginx
    environment:
      - TLS_ENABLED=${TLS_ENABLED:-True}
    ports:
      - "${NGINX_HTTP_PORT:-80}:80"
      - "${NGINX_HTTPS_PORT:-443}:443"
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./nginx/ssl:/etc/nginx/ssl:ro
      - nginx_logs:/var/log/nginx
    depends_on:
      - gateway
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s
    networks:
      - gateway_network
    restart: unless-stopped
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 256M
        reservations:
          cpus: '0.25'
          memory: 128M

networks:
  gateway_network:
    driver: bridge
    name: mcp_gateway_network
    internal: false
  gateway_to_servers:
    driver: bridge
    name: gateway_to_servers
    internal: false
  mcp_servers:
    driver: bridge
    name: mcp_servers_network
    internal: true  # Isolated network for MCP servers

volumes:
  postgres_data:
    name: mcp_gateway_postgres_data
    driver: local
  redis_data:
    name: mcp_gateway_redis_data
    driver: local
  gateway_logs:
    name: mcp_gateway_logs
    driver: local
  nginx_logs:
    name: mcp_gateway_nginx_logs
    driver: local
```

### Step 2: Create Production Docker Compose

#### docker-compose.prod.yml

Create production Docker Compose override:

```yaml
version: '3.8'

services:
  gateway:
    environment:
      - DEBUG=False
      - ENVIRONMENT=production
    deploy:
      replicas: 2
      update_config:
        parallelism: 1
        delay: 10s
      restart_policy:
        condition: on-failure
        delay: 5s
        max_attempts: 3
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"

  postgres:
    deploy:
      replicas: 1
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"

  redis:
    deploy:
      replicas: 1
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"

  nginx:
    deploy:
      replicas: 2
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
```

### Step 3: Create Development Docker Compose

#### docker-compose.dev.yml

Create development Docker Compose override:

```yaml
version: '3.8'

services:
  gateway:
    environment:
      - DEBUG=True
      - ENVIRONMENT=development
    volumes:
      - ./gateway/src:/app/src
    command: uvicorn src.main:app --host 0.0.0.0 --port 8000 --reload
    ports:
      - "8000:8000"

  postgres:
    ports:
      - "5432:5432"

  redis:
    ports:
      - "6379:6379"
```

### Step 4: Create Database Initialization Script

#### scripts/init-db.sql

Create database initialization script:

```sql
-- Create extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Create indexes for audit logs
CREATE INDEX IF NOT EXISTS idx_audit_logs_user_id ON audit_logs(user_id);
CREATE INDEX IF NOT EXISTS idx_audit_logs_timestamp ON audit_logs(timestamp);
CREATE INDEX IF NOT EXISTS idx_audit_logs_action ON audit_logs(action);
CREATE INDEX IF NOT EXISTS idx_audit_logs_resource ON audit_logs(resource);

-- Create indexes for users
CREATE INDEX IF NOT EXISTS idx_users_username ON users(username);
CREATE INDEX IF NOT EXISTS idx_users_email ON users(email);

-- Create indexes for MCP servers
CREATE INDEX IF NOT EXISTS idx_mcp_servers_name ON mcp_servers(name);
CREATE INDEX IF NOT EXISTS idx_mcp_servers_status ON mcp_servers(status);
CREATE INDEX IF NOT EXISTS idx_mcp_servers_health_status ON mcp_servers(health_status);
```

### Step 5: Create Startup Script

#### scripts/start.sh

Create startup script:

```bash
#!/bin/bash

set -e

echo "Starting MCP Gateway..."

# Check if .env exists
if [ ! -f .env ]; then
    echo "Error: .env file not found. Please create it from .env.example"
    exit 1
fi

# Generate SSL certificates if they don't exist
if [ ! -f nginx/ssl/cert.pem ] || [ ! -f nginx/ssl/key.pem ]; then
    echo "Generating SSL certificates..."
    ./scripts/generate-certs.sh
fi

# Create networks if they don't exist
docker network create mcp_gateway_network 2>/dev/null || true
docker network create gateway_to_servers 2>/dev/null || true
docker network create mcp_servers_network 2>/dev/null || true

# Start services
docker-compose up -d

# Wait for services to be healthy
echo "Waiting for services to start..."
sleep 10

# Run database migrations
echo "Running database migrations..."
docker-compose exec gateway alembic upgrade head

# Seed initial data
echo "Seeding initial data..."
docker-compose exec gateway python -m src.scripts.seed_permissions

echo "MCP Gateway started successfully!"
echo "Gateway: http://localhost"
echo "API Docs: http://localhost/api/docs"
```

Make it executable:

```bash
chmod +x scripts/start.sh
```

### Step 6: Create Stop Script

#### scripts/stop.sh

Create stop script:

```bash
#!/bin/bash

set -e

echo "Stopping MCP Gateway..."

# Stop services
docker-compose down

echo "MCP Gateway stopped successfully!"
```

Make it executable:

```bash
chmod +x scripts/stop.sh
```

### Step 7: Create Restart Script

#### scripts/restart.sh

Create restart script:

```bash
#!/bin/bash

set -e

echo "Restarting MCP Gateway..."

# Stop services
./scripts/stop.sh

# Start services
./scripts/start.sh

echo "MCP Gateway restarted successfully!"
```

Make it executable:

```bash
chmod +x scripts/restart.sh
```

## Testing

### Test Docker Compose Configuration

Test Docker Compose configuration:

```bash
# Validate configuration
docker-compose config

# Start services
docker-compose up -d

# Check service status
docker-compose ps

# Check logs
docker-compose logs -f

# Test health checks
curl http://localhost/health
curl https://localhost/health

# Stop services
docker-compose down
```

### Test Production Configuration

Test production configuration:

```bash
# Start with production configuration
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# Check service status
docker-compose ps

# Test scaling
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d --scale gateway=2
```

## Verification

1. **Services Start**: All services start successfully
2. **Health Checks**: All health checks pass
3. **Networks**: All networks are created
4. **Volumes**: All volumes are created
5. **Logging**: Logging is configured
6. **Resource Limits**: Resource limits are set
7. **Security**: Security configurations are applied

## Troubleshooting

### Issue: Services won't start

**Solution**: Check Docker Compose configuration and logs:
```bash
docker-compose config
docker-compose logs
```

### Issue: Network creation fails

**Solution**: Check if networks already exist:
```bash
docker network ls
docker network rm mcp_gateway_network
```

### Issue: Volume mounting fails

**Solution**: Check volume paths and permissions:
```bash
ls -la nginx/ssl/
chmod 644 nginx/ssl/cert.pem
chmod 600 nginx/ssl/key.pem
```

## Next Steps

After completing this instruction, proceed to:
- **02-environment-config.md**: Configure environment variables and secrets

