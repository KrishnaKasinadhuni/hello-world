# 03: Docker Base Configuration

## Objective

Set up the base Docker Compose configuration for the MCP Gateway, including services for the gateway, database, Redis, and Nginx reverse proxy.

## Prerequisites

- Completed: 01-project-structure.md
- Completed: 02-dependencies.md
- Docker and Docker Compose installed
- Basic understanding of Docker and Docker Compose

## Implementation Steps

### Step 1: Create Base Docker Compose File

#### docker-compose.yml

Create the base Docker Compose configuration:

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
    volumes:
      - postgres_data:/var/lib/postgresql/data
    ports:
      - "${POSTGRES_PORT:-5432}:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER:-postgres}"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - gateway_network
    restart: unless-stopped

  # Redis Cache
  redis:
    image: redis:7-alpine
    container_name: mcp-gateway-redis
    command: redis-server --requirepass ${REDIS_PASSWORD:-}
    ports:
      - "${REDIS_PORT:-6379}:6379"
    volumes:
      - redis_data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - gateway_network
    restart: unless-stopped

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
      - ENVIRONMENT=${ENVIRONMENT:-development}
      - DEBUG=${DEBUG:-True}
    ports:
      - "${GATEWAY_PORT:-8000}:8000"
    volumes:
      - ./gateway/src:/app/src
      - gateway_logs:/var/log/mcp-gateway
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
    restart: unless-stopped

  # Nginx Reverse Proxy
  nginx:
    build:
      context: ./nginx
      dockerfile: Dockerfile
    container_name: mcp-gateway-nginx
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
    networks:
      - gateway_network
    restart: unless-stopped

networks:
  gateway_network:
    driver: bridge
    name: mcp_gateway_network

volumes:
  postgres_data:
    name: mcp_gateway_postgres_data
  redis_data:
    name: mcp_gateway_redis_data
  gateway_logs:
    name: mcp_gateway_logs
  nginx_logs:
    name: mcp_gateway_nginx_logs
```

### Step 2: Create Nginx Configuration

#### nginx/nginx.conf

Create Nginx configuration file:

```nginx
user nginx;
worker_processes auto;
error_log /var/log/nginx/error.log warn;
pid /var/run/nginx.pid;

events {
    worker_connections 1024;
    use epoll;
}

http {
    include /etc/nginx/mime.types;
    default_type application/octet-stream;

    log_format main '$remote_addr - $remote_user [$time_local] "$request" '
                    '$status $body_bytes_sent "$http_referer" '
                    '"$http_user_agent" "$http_x_forwarded_for"';

    access_log /var/log/nginx/access.log main;

    sendfile on;
    tcp_nopush on;
    tcp_nodelay on;
    keepalive_timeout 65;
    types_hash_max_size 2048;
    client_max_body_size 100M;

    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_proxied any;
    gzip_comp_level 6;
    gzip_types text/plain text/css text/xml text/javascript 
               application/json application/javascript application/xml+rss 
               application/rss+xml font/truetype font/opentype 
               application/vnd.ms-fontobject image/svg+xml;

    # Rate limiting
    limit_req_zone $binary_remote_addr zone=api_limit:10m rate=10r/s;
    limit_req_zone $binary_remote_addr zone=auth_limit:10m rate=5r/s;

    # Upstream gateway
    upstream gateway {
        server gateway:8000;
        keepalive 32;
    }

    # HTTP server (redirect to HTTPS in production)
    server {
        listen 80;
        server_name _;

        # Health check endpoint (no redirect)
        location /health {
            proxy_pass http://gateway;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }

        # Redirect all other traffic to HTTPS (when TLS is enabled)
        location / {
            if ($ssl_protocol = "") {
                return 301 https://$host$request_uri;
            }
            proxy_pass http://gateway;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
    }

    # HTTPS server (when TLS is configured)
    server {
        listen 443 ssl http2;
        server_name _;

        # SSL configuration (will be configured in security section)
        # ssl_certificate /etc/nginx/ssl/cert.pem;
        # ssl_certificate_key /etc/nginx/ssl/key.pem;
        # ssl_protocols TLSv1.2 TLSv1.3;
        # ssl_ciphers HIGH:!aNULL:!MD5;
        # ssl_prefer_server_ciphers on;

        # Security headers
        add_header X-Frame-Options "SAMEORIGIN" always;
        add_header X-Content-Type-Options "nosniff" always;
        add_header X-XSS-Protection "1; mode=block" always;
        add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;

        # API endpoints with rate limiting
        location /api/ {
            limit_req zone=api_limit burst=20 nodelay;
            proxy_pass http://gateway;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_set_header X-Forwarded-Host $host;
            proxy_connect_timeout 60s;
            proxy_send_timeout 60s;
            proxy_read_timeout 60s;
        }

        # Auth endpoints with stricter rate limiting
        location /api/auth/ {
            limit_req zone=auth_limit burst=10 nodelay;
            proxy_pass http://gateway;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }

        # WebSocket support
        location /ws/ {
            proxy_pass http://gateway;
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection "upgrade";
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_read_timeout 86400;
        }

        # Health check
        location /health {
            proxy_pass http://gateway;
            proxy_set_header Host $host;
            access_log off;
        }

        # Default location
        location / {
            proxy_pass http://gateway;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
    }
}
```

### Step 3: Create Nginx Dockerfile

#### nginx/Dockerfile

Create Dockerfile for Nginx:

```dockerfile
FROM nginx:alpine

# Copy configuration
COPY nginx.conf /etc/nginx/nginx.conf

# Create SSL directory
RUN mkdir -p /etc/nginx/ssl

# Expose ports
EXPOSE 80 443

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost/health || exit 1

# Start Nginx
CMD ["nginx", "-g", "daemon off;"]
```

### Step 4: Update Environment Variables

#### .env.example

Update environment variables for Docker Compose:

```env
# Application
GATEWAY_HOST=0.0.0.0
GATEWAY_PORT=8000
ENVIRONMENT=development
DEBUG=True

# Database
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_DB=mcp_gateway
POSTGRES_PORT=5432

# Redis
REDIS_HOST=redis
REDIS_PORT=6379
REDIS_PASSWORD=
REDIS_DB=0

# Nginx
NGINX_HTTP_PORT=80
NGINX_HTTPS_PORT=443

# JWT
JWT_SECRET_KEY=your-secret-key-change-in-production
JWT_ALGORITHM=HS256
JWT_ACCESS_TOKEN_EXPIRE_MINUTES=30
JWT_REFRESH_TOKEN_EXPIRE_DAYS=7

# TLS
TLS_ENABLED=False
TLS_CERT_PATH=/etc/nginx/ssl/cert.pem
TLS_KEY_PATH=/etc/nginx/ssl/key.pem

# Rate Limiting
RATE_LIMIT_ENABLED=True
RATE_LIMIT_PER_MINUTE=60
RATE_LIMIT_PER_HOUR=1000

# Logging
LOG_LEVEL=INFO
LOG_FILE_PATH=/var/log/mcp-gateway/gateway.log

# MCP Server Configuration
MCP_SERVER_NETWORK=mcp_servers
MCP_SERVER_MAX_INSTANCES=10
MCP_SERVER_TIMEOUT=30
```

### Step 5: Create Docker Compose Override for Development

#### docker-compose.override.yml

Create development override file (optional, automatically used by Docker Compose):

```yaml
version: '3.8'

services:
  gateway:
    volumes:
      - ./gateway/src:/app/src
    environment:
      - DEBUG=True
      - ENVIRONMENT=development
    command: uvicorn src.main:app --host 0.0.0.0 --port 8000 --reload

  postgres:
    ports:
      - "5432:5432"

  redis:
    ports:
      - "6379:6379"
```

### Step 6: Create Docker Compose Production File

#### docker-compose.prod.yml

Create production configuration:

```yaml
version: '3.8'

services:
  gateway:
    environment:
      - DEBUG=False
      - ENVIRONMENT=production
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 2G
        reservations:
          cpus: '1'
          memory: 1G
    restart: always

  postgres:
    environment:
      - POSTGRES_PASSWORD_FILE=/run/secrets/postgres_password
    secrets:
      - postgres_password
    deploy:
      resources:
        limits:
          cpus: '1'
          memory: 1G
    restart: always

  redis:
    command: redis-server --requirepass ${REDIS_PASSWORD} --maxmemory 512mb --maxmemory-policy allkeys-lru
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
    restart: always

  nginx:
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 256M
    restart: always

secrets:
  postgres_password:
    external: true
```

### Step 7: Create Docker Management Scripts

#### scripts/docker-setup.sh

Create script to set up Docker environment:

```bash
#!/bin/bash

set -e

echo "Setting up MCP Gateway Docker environment..."

# Check if .env exists
if [ ! -f .env ]; then
    echo "Creating .env file from .env.example..."
    cp .env.example .env
    echo "Please edit .env file with your configuration"
fi

# Create necessary directories
mkdir -p nginx/ssl
mkdir -p gateway/logs

# Set permissions
chmod 755 nginx/ssl
chmod 755 gateway/logs

echo "Docker environment setup complete!"
```

Make it executable:

```bash
chmod +x scripts/docker-setup.sh
```

#### scripts/docker-start.sh

Create script to start services:

```bash
#!/bin/bash

set -e

echo "Starting MCP Gateway services..."

# Check if .env exists
if [ ! -f .env ]; then
    echo "Error: .env file not found. Run ./scripts/docker-setup.sh first"
    exit 1
fi

# Start services
docker-compose up -d

echo "Services started. Use 'docker-compose logs -f' to view logs"
```

Make it executable:

```bash
chmod +x scripts/docker-start.sh
```

#### scripts/docker-stop.sh

Create script to stop services:

```bash
#!/bin/bash

set -e

echo "Stopping MCP Gateway services..."

docker-compose down

echo "Services stopped"
```

Make it executable:

```bash
chmod +x scripts/docker-stop.sh
```

## Testing

### Test Docker Compose Configuration

Validate Docker Compose configuration:

```bash
# Validate configuration
docker-compose config

# Check services
docker-compose ps

# View logs
docker-compose logs
```

### Test Service Health

Check if all services are healthy:

```bash
# Check gateway health
curl http://localhost:8000/health

# Check through Nginx
curl http://localhost/health

# Check database connection
docker-compose exec postgres pg_isready -U postgres

# Check Redis connection
docker-compose exec redis redis-cli ping
```

### Test Service Communication

Verify services can communicate:

```bash
# Test gateway to database
docker-compose exec gateway python -c "from sqlalchemy import create_engine; engine = create_engine('postgresql://postgres:postgres@postgres:5432/mcp_gateway'); conn = engine.connect(); print('Database connection successful')"

# Test gateway to Redis
docker-compose exec gateway python -c "import redis; r = redis.Redis(host='redis', port=6379); r.ping(); print('Redis connection successful')"
```

## Verification

1. **Docker Compose File**: Configuration should be valid
2. **Services Start**: All services should start successfully
3. **Health Checks**: All health checks should pass
4. **Network Connectivity**: Services should be able to communicate
5. **Nginx Proxy**: Nginx should proxy requests to gateway
6. **Volumes**: Data volumes should be created and persistent

## Troubleshooting

### Issue: Docker Compose fails to start

**Solution**: Check Docker and Docker Compose versions:
```bash
docker --version
docker-compose --version
```

### Issue: Port already in use

**Solution**: Change port mappings in docker-compose.yml or stop conflicting services:
```bash
# Find process using port
lsof -i :8000
# Kill process or change port in docker-compose.yml
```

### Issue: Services can't communicate

**Solution**: Verify services are on the same network:
```bash
docker network inspect mcp_gateway_network
```

### Issue: Database connection fails

**Solution**: Check database is healthy and credentials are correct:
```bash
docker-compose exec postgres psql -U postgres -c "SELECT version();"
```

### Issue: Nginx can't connect to gateway

**Solution**: Verify gateway is running and accessible:
```bash
docker-compose exec nginx wget -O- http://gateway:8000/health
```

## Next Steps

After completing this instruction, proceed to:
- **02-core-gateway/01-gateway-architecture.md**: Design the gateway architecture
- **02-core-gateway/02-api-server.md**: Implement the main API server

