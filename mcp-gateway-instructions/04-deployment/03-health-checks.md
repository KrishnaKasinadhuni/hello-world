# 03: Health Checks

## Objective

Configure comprehensive health checks for all services in the MCP Gateway, including service health endpoints, dependency health checks, and health monitoring.

## Prerequisites

- Completed: 04-deployment/01-docker-compose-full.md
- Understanding of health check patterns
- Knowledge of service monitoring

## Implementation Steps

### Step 1: Create Comprehensive Health Check Endpoint

#### gateway/src/api/health.py

Create health check endpoint:

```python
"""Health check endpoints."""
from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session
from typing import Dict
import time

from src.database import get_db, engine
from src.redis_client import get_redis
from src.config import settings

router = APIRouter()


@router.get("/health")
async def health_check() -> Dict:
    """Basic health check endpoint."""
    return {
        "status": "healthy",
        "service": "mcp-gateway",
        "version": "0.1.0",
    }


@router.get("/health/detailed")
async def detailed_health_check(db: Session = Depends(get_db)) -> Dict:
    """Detailed health check with dependency status."""
    health_status = {
        "status": "healthy",
        "service": "mcp-gateway",
        "version": "0.1.0",
        "timestamp": time.time(),
        "dependencies": {},
    }

    # Check database
    try:
        with engine.connect() as conn:
            conn.execute("SELECT 1")
        health_status["dependencies"]["database"] = {
            "status": "healthy",
            "type": "postgresql",
        }
    except Exception as e:
        health_status["dependencies"]["database"] = {
            "status": "unhealthy",
            "error": str(e),
        }
        health_status["status"] = "degraded"

    # Check Redis
    try:
        redis = get_redis()
        if redis:
            redis.ping()
            health_status["dependencies"]["redis"] = {
                "status": "healthy",
                "type": "redis",
            }
        else:
            health_status["dependencies"]["redis"] = {
                "status": "unhealthy",
                "error": "Redis client not initialized",
            }
            health_status["status"] = "degraded"
    except Exception as e:
        health_status["dependencies"]["redis"] = {
            "status": "unhealthy",
            "error": str(e),
        }
        health_status["status"] = "degraded"

    # Check Docker (for MCP server management)
    try:
        import docker
        client = docker.from_env()
        client.ping()
        health_status["dependencies"]["docker"] = {
            "status": "healthy",
            "type": "docker",
        }
    except Exception as e:
        health_status["dependencies"]["docker"] = {
            "status": "unhealthy",
            "error": str(e),
        }
        health_status["status"] = "degraded"

    return health_status


@router.get("/health/ready")
async def readiness_check(db: Session = Depends(get_db)) -> Dict:
    """Readiness check - indicates service is ready to accept traffic."""
    try:
        # Check database
        with engine.connect() as conn:
            conn.execute("SELECT 1")

        # Check Redis
        redis = get_redis()
        if redis:
            redis.ping()
        else:
            return {"status": "not_ready", "reason": "Redis not available"}

        return {"status": "ready"}
    except Exception as e:
        return {"status": "not_ready", "reason": str(e)}


@router.get("/health/live")
async def liveness_check() -> Dict:
    """Liveness check - indicates service is alive."""
    return {"status": "alive"}
```

### Step 2: Update Main Application

#### gateway/src/main.py

Add health check router:

```python
# Add to imports
from src.api import health

# Add router
app.include_router(health.router, tags=["health"])
```

### Step 3: Update Docker Compose Health Checks

#### docker-compose.yml

Update health checks in Docker Compose:

```yaml
services:
  gateway:
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

  postgres:
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER:-postgres}"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s

  redis:
    healthcheck:
      test: ["CMD", "redis-cli", "--no-auth-warning", "-a", "${REDIS_PASSWORD:-}", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 5s

  nginx:
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s
```

### Step 4: Create Health Check Script

#### scripts/health-check.sh

Create health check script:

```bash
#!/bin/bash

set -e

echo "Checking MCP Gateway health..."

# Check gateway health
GATEWAY_HEALTH=$(curl -s http://localhost:8000/health | jq -r '.status')
if [ "$GATEWAY_HEALTH" != "healthy" ]; then
    echo "ERROR: Gateway health check failed"
    exit 1
fi

# Check detailed health
DETAILED_HEALTH=$(curl -s http://localhost:8000/health/detailed | jq -r '.status')
if [ "$DETAILED_HEALTH" != "healthy" ]; then
    echo "WARNING: Gateway health check shows degraded status"
    curl -s http://localhost:8000/health/detailed | jq '.dependencies'
fi

# Check readiness
READY=$(curl -s http://localhost:8000/health/ready | jq -r '.status')
if [ "$READY" != "ready" ]; then
    echo "ERROR: Gateway is not ready"
    exit 1
fi

# Check liveness
LIVE=$(curl -s http://localhost:8000/health/live | jq -r '.status')
if [ "$LIVE" != "alive" ]; then
    echo "ERROR: Gateway is not alive"
    exit 1
fi

echo "All health checks passed!"
```

Make it executable:

```bash
chmod +x scripts/health-check.sh
```

## Testing

### Test Health Checks

Test health checks:

```bash
# Test basic health check
curl http://localhost:8000/health

# Test detailed health check
curl http://localhost:8000/health/detailed

# Test readiness check
curl http://localhost:8000/health/ready

# Test liveness check
curl http://localhost:8000/health/live

# Run health check script
./scripts/health-check.sh
```

## Verification

1. **Health Endpoints**: All health endpoints work
2. **Dependency Checks**: Dependency checks work correctly
3. **Docker Health Checks**: Docker health checks are configured
4. **Health Check Script**: Health check script works

## Troubleshooting

### Issue: Health check fails

**Solution**: Check service logs and dependency status:
```bash
docker-compose logs gateway
curl http://localhost:8000/health/detailed
```

### Issue: Dependency health check fails

**Solution**: Check dependency services:
```bash
docker-compose ps
docker-compose exec postgres pg_isready
docker-compose exec redis redis-cli ping
```

## Next Steps

After completing this instruction, proceed to:
- **04-production-deployment.md**: Production deployment guide

