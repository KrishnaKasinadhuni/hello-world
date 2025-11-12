# 05: Rate Limiting

## Objective

Implement rate limiting for the MCP Gateway using Redis, including per-user rate limiting, per-endpoint rate limiting, and configurable rate limit policies.

## Prerequisites

- Completed: 02-core-gateway/02-api-server.md
- Redis installed and configured
- Understanding of rate limiting patterns
- Knowledge of Redis data structures

## Implementation Steps

### Step 1: Create Rate Limiter Service

#### gateway/src/security/rate_limiter.py

Create rate limiter service:

```python
"""Rate limiter service using Redis."""
import logging
import time
from typing import Optional
from src.redis_client import get_redis
from src.config import settings

logger = logging.getLogger(__name__)


class RateLimiter:
    """Rate limiter using Redis."""

    def __init__(self):
        """Initialize rate limiter."""
        self.redis = get_redis()
        self.enabled = settings.RATE_LIMIT_ENABLED
        self.default_limit_per_minute = settings.RATE_LIMIT_PER_MINUTE
        self.default_limit_per_hour = settings.RATE_LIMIT_PER_HOUR

    async def check_rate_limit(
        self,
        key: str,
        limit: int,
        window: int,
    ) -> tuple[bool, int, int]:
        """Check rate limit for a key.
        
        Returns:
            tuple: (is_allowed, remaining, reset_time)
        """
        if not self.enabled:
            return True, limit, int(time.time()) + window

        try:
            current = int(time.time())
            window_start = current - (current % window)
            redis_key = f"rate_limit:{key}:{window_start}"

            # Increment counter
            count = self.redis.incr(redis_key)
            
            # Set expiration
            if count == 1:
                self.redis.expire(redis_key, window)

            # Check limit
            remaining = max(0, limit - count)
            reset_time = window_start + window
            is_allowed = count <= limit

            return is_allowed, remaining, reset_time
        except Exception as e:
            logger.error(f"Rate limit check failed: {e}")
            # Allow request on error
            return True, limit, int(time.time()) + window

    async def check_user_rate_limit(
        self,
        user_id: str,
        limit_per_minute: Optional[int] = None,
        limit_per_hour: Optional[int] = None,
    ) -> tuple[bool, dict]:
        """Check rate limit for a user."""
        if limit_per_minute is None:
            limit_per_minute = self.default_limit_per_minute
        if limit_per_hour is None:
            limit_per_hour = self.default_limit_per_hour

        # Check per-minute limit
        allowed_minute, remaining_minute, reset_minute = await self.check_rate_limit(
            f"user:{user_id}:minute",
            limit_per_minute,
            60,
        )

        # Check per-hour limit
        allowed_hour, remaining_hour, reset_hour = await self.check_rate_limit(
            f"user:{user_id}:hour",
            limit_per_hour,
            3600,
        )

        is_allowed = allowed_minute and allowed_hour
        remaining = min(remaining_minute, remaining_hour)
        reset_time = min(reset_minute, reset_hour)

        return is_allowed, {
            "allowed": is_allowed,
            "remaining": remaining,
            "reset_time": reset_time,
            "limit_per_minute": limit_per_minute,
            "limit_per_hour": limit_per_hour,
        }

    async def check_endpoint_rate_limit(
        self,
        endpoint: str,
        user_id: Optional[str] = None,
        limit: int = 100,
        window: int = 60,
    ) -> tuple[bool, dict]:
        """Check rate limit for an endpoint."""
        if user_id:
            key = f"endpoint:{endpoint}:user:{user_id}"
        else:
            key = f"endpoint:{endpoint}"

        allowed, remaining, reset_time = await self.check_rate_limit(key, limit, window)

        return allowed, {
            "allowed": allowed,
            "remaining": remaining,
            "reset_time": reset_time,
            "limit": limit,
            "window": window,
        }

    async def check_ip_rate_limit(
        self,
        ip_address: str,
        limit: int = 60,
        window: int = 60,
    ) -> tuple[bool, dict]:
        """Check rate limit for an IP address."""
        key = f"ip:{ip_address}"
        allowed, remaining, reset_time = await self.check_rate_limit(key, limit, window)

        return allowed, {
            "allowed": allowed,
            "remaining": remaining,
            "reset_time": reset_time,
            "limit": limit,
            "window": window,
        }

    async def reset_rate_limit(self, key: str):
        """Reset rate limit for a key."""
        try:
            pattern = f"rate_limit:{key}:*"
            keys = self.redis.keys(pattern)
            if keys:
                self.redis.delete(*keys)
        except Exception as e:
            logger.error(f"Rate limit reset failed: {e}")
```

### Step 2: Create Rate Limit Middleware

#### gateway/src/middleware/rate_limit.py

Create rate limit middleware:

```python
"""Rate limit middleware."""
import logging
from fastapi import Request, HTTPException, status
from starlette.middleware.base import BaseHTTPMiddleware
from starlette.responses import Response

from src.security.rate_limiter import RateLimiter
from src.auth.dependencies import get_current_user_optional
from src.config import settings

logger = logging.getLogger(__name__)


class RateLimitMiddleware(BaseHTTPMiddleware):
    """Middleware for rate limiting."""

    def __init__(self, app, rate_limiter: RateLimiter = None):
        """Initialize rate limit middleware."""
        super().__init__(app)
        self.rate_limiter = rate_limiter or RateLimiter()

    async def dispatch(self, request: Request, call_next):
        """Process request with rate limiting."""
        # Skip rate limiting for health checks
        if request.url.path == "/health":
            return await call_next(request)

        # Get client IP
        client_ip = request.client.host if request.client else "unknown"

        # Check IP rate limit
        ip_allowed, ip_info = await self.rate_limiter.check_ip_rate_limit(
            client_ip,
            limit=settings.RATE_LIMIT_PER_MINUTE,
            window=60,
        )

        if not ip_allowed:
            logger.warning(f"Rate limit exceeded for IP: {client_ip}")
            return Response(
                content="Rate limit exceeded",
                status_code=status.HTTP_429_TOO_MANY_REQUESTS,
                headers={
                    "X-RateLimit-Limit": str(ip_info["limit"]),
                    "X-RateLimit-Remaining": str(ip_info["remaining"]),
                    "X-RateLimit-Reset": str(ip_info["reset_time"]),
                    "Retry-After": str(ip_info["reset_time"] - int(time.time())),
                },
            )

        # Check endpoint rate limit
        endpoint = request.url.path
        endpoint_allowed, endpoint_info = await self.rate_limiter.check_endpoint_rate_limit(
            endpoint,
            limit=100,
            window=60,
        )

        if not endpoint_allowed:
            logger.warning(f"Rate limit exceeded for endpoint: {endpoint}")
            return Response(
                content="Rate limit exceeded for endpoint",
                status_code=status.HTTP_429_TOO_MANY_REQUESTS,
                headers={
                    "X-RateLimit-Limit": str(endpoint_info["limit"]),
                    "X-RateLimit-Remaining": str(endpoint_info["remaining"]),
                    "X-RateLimit-Reset": str(endpoint_info["reset_time"]),
                    "Retry-After": str(endpoint_info["reset_time"] - int(time.time())),
                },
            )

        # Process request
        response = await call_next(request)

        # Add rate limit headers
        response.headers["X-RateLimit-Limit"] = str(ip_info["limit"])
        response.headers["X-RateLimit-Remaining"] = str(ip_info["remaining"])
        response.headers["X-RateLimit-Reset"] = str(ip_info["reset_time"])

        return response
```

### Step 3: Create Rate Limit Decorator

#### gateway/src/security/decorators.py

Create rate limit decorator:

```python
"""Security decorators."""
import logging
from functools import wraps
from fastapi import Request, HTTPException, status
from typing import Optional

from src.security.rate_limiter import RateLimiter

logger = logging.getLogger(__name__)


def rate_limit(limit: int = 60, window: int = 60):
    """Decorator for rate limiting."""
    def decorator(func):
        @wraps(func)
        async def wrapper(*args, **kwargs):
            # Get request from kwargs
            request = kwargs.get("request")
            if not request:
                return await func(*args, **kwargs)

            # Get user ID if authenticated
            user_id = None
            if hasattr(request.state, "user"):
                user_id = str(request.state.user.id)

            # Check rate limit
            rate_limiter = RateLimiter()
            if user_id:
                allowed, info = await rate_limiter.check_user_rate_limit(user_id)
            else:
                client_ip = request.client.host if request.client else "unknown"
                allowed, info = await rate_limiter.check_ip_rate_limit(client_ip, limit, window)

            if not allowed:
                raise HTTPException(
                    status_code=status.HTTP_429_TOO_MANY_REQUESTS,
                    detail="Rate limit exceeded",
                    headers={
                        "X-RateLimit-Limit": str(info["limit"]),
                        "X-RateLimit-Remaining": str(info["remaining"]),
                        "X-RateLimit-Reset": str(info["reset_time"]),
                        "Retry-After": str(info["reset_time"] - int(time.time())),
                    },
                )

            return await func(*args, **kwargs)
        return wrapper
    return decorator
```

### Step 4: Update Main Application

#### gateway/src/main.py

Add rate limit middleware:

```python
# Add to imports
from src.middleware.rate_limit import RateLimitMiddleware
import time

# Add middleware after CORS middleware
app.add_middleware(RateLimitMiddleware)
```

### Step 5: Create Rate Limit Configuration

#### gateway/src/config.py

Add rate limit configuration:

```python
# Add to Settings class
RATE_LIMIT_ENABLED: bool = True
RATE_LIMIT_PER_MINUTE: int = 60
RATE_LIMIT_PER_HOUR: int = 1000
RATE_LIMIT_PER_DAY: int = 10000

# Per-endpoint rate limits
RATE_LIMIT_ENDPOINTS: dict = {
    "/api/auth/login": {"limit": 5, "window": 60},
    "/api/auth/register": {"limit": 3, "window": 3600},
    "/api/servers": {"limit": 100, "window": 60},
}
```

### Step 6: Create Rate Limit API Endpoints

#### gateway/src/api/rate_limit.py

Create rate limit management endpoints:

```python
"""Rate limit management endpoints."""
from fastapi import APIRouter, Depends, HTTPException
from src.auth.dependencies import get_current_user
from src.security.rate_limiter import RateLimiter
from src.models.user import User

router = APIRouter()


@router.get("/rate-limit/status")
async def get_rate_limit_status(
    current_user: User = Depends(get_current_user),
):
    """Get rate limit status for current user."""
    rate_limiter = RateLimiter()
    allowed, info = await rate_limiter.check_user_rate_limit(str(current_user.id))
    return info
```

## Testing

### Test Rate Limiting

Test rate limiting:

```bash
# Test IP rate limiting
for i in {1..70}; do
    curl http://localhost:8000/health
done

# Test user rate limiting
TOKEN="your-token"
for i in {1..70}; do
    curl -H "Authorization: Bearer $TOKEN" http://localhost:8000/api/servers
done

# Check rate limit status
curl -H "Authorization: Bearer $TOKEN" http://localhost:8000/api/rate-limit/status
```

## Verification

1. **Rate Limiter**: Rate limiter service works correctly
2. **Middleware**: Rate limit middleware is active
3. **IP Rate Limiting**: IP-based rate limiting works
4. **User Rate Limiting**: User-based rate limiting works
5. **Endpoint Rate Limiting**: Endpoint-based rate limiting works
6. **Headers**: Rate limit headers are set correctly

## Troubleshooting

### Issue: Rate limiting not working

**Solution**: Check Redis connection and rate limit configuration:
```bash
docker-compose exec redis redis-cli ping
# Check rate limit keys
docker-compose exec redis redis-cli keys "rate_limit:*"
```

### Issue: Rate limit too strict

**Solution**: Adjust rate limit configuration:
```python
# In config.py
RATE_LIMIT_PER_MINUTE = 120
RATE_LIMIT_PER_HOUR = 2000
```

### Issue: Rate limit not resetting

**Solution**: Check Redis expiration and key format:
```bash
docker-compose exec redis redis-cli TTL rate_limit:user:123:minute:1234567890
```

## Next Steps

After completing this instruction, proceed to:
- **06-server-isolation.md**: Implement server isolation

