# 04: Request Routing

## Objective

Implement request routing system that routes client requests to appropriate MCP servers, handles load balancing, failover, and request transformation.

## Prerequisites

- Completed: 01-gateway-architecture.md
- Completed: 02-api-server.md
- Completed: 03-mcp-server-registry.md
- Understanding of HTTP proxying and WebSocket connections
- Knowledge of request transformation patterns

## Implementation Steps

### Step 1: Create Router Service

#### gateway/src/router/service.py

Create router service for request routing:

```python
"""Request router service."""
import logging
from typing import Optional, Dict
from fastapi import Request, Response
from sqlalchemy.orm import Session
import httpx

from src.registry.service import RegistryService
from src.models.server import MCPServer, ServerStatus, HealthStatus

logger = logging.getLogger(__name__)


class RouterService:
    """Service for routing requests to MCP servers."""

    def __init__(self, db: Session):
        """Initialize router service."""
        self.db = db
        self.registry = RegistryService(db)
        self.client = httpx.AsyncClient(timeout=30.0, follow_redirects=True)

    async def route_request(
        self,
        server_id: str,
        path: str,
        request: Request,
    ) -> Response:
        """Route HTTP request to MCP server."""
        try:
            # Get server from registry
            server = await self.registry.get_server(server_id)
            if not server:
                raise ValueError(f"Server {server_id} not found")

            # Check server status
            if server.status != ServerStatus.ACTIVE:
                raise ValueError(f"Server {server_id} is not active")

            # Check health status
            if server.health_status != HealthStatus.HEALTHY:
                logger.warning(f"Server {server_id} health status is {server.health_status}")

            # Build target URL
            target_url = f"{server.endpoint}/{path.lstrip('/')}"
            
            # Get query parameters
            query_params = dict(request.query_params)
            
            # Get request body
            body = await request.body()
            
            # Get headers (filter out hop-by-hop headers)
            headers = self._filter_headers(dict(request.headers))
            
            # Make request to MCP server
            response = await self.client.request(
                method=request.method,
                url=target_url,
                params=query_params,
                headers=headers,
                content=body,
            )

            # Create response
            return Response(
                content=response.content,
                status_code=response.status_code,
                headers=dict(response.headers),
                media_type=response.headers.get("content-type"),
            )
        except httpx.HTTPError as e:
            logger.error(f"HTTP error routing request: {e}")
            raise
        except Exception as e:
            logger.error(f"Error routing request: {e}")
            raise

    async def route_websocket(
        self,
        server_id: str,
        path: str,
        websocket,
    ):
        """Route WebSocket connection to MCP server."""
        try:
            # Get server from registry
            server = await self.registry.get_server(server_id)
            if not server:
                await websocket.close(code=1008, reason="Server not found")
                return

            # Check server status
            if server.status != ServerStatus.ACTIVE:
                await websocket.close(code=1008, reason="Server not active")
                return

            # Build target URL
            target_url = f"{server.endpoint}/{path.lstrip('/')}"
            # Convert http to ws
            if target_url.startswith("http://"):
                target_url = target_url.replace("http://", "ws://", 1)
            elif target_url.startswith("https://"):
                target_url = target_url.replace("https://", "wss://", 1)

            # Connect to MCP server WebSocket
            async with httpx.AsyncClient() as client:
                async with client.stream(
                    "GET",
                    target_url,
                    headers=dict(websocket.headers),
                ) as response:
                    # Forward messages bidirectionally
                    # This is a simplified version - full implementation would require
                    # proper WebSocket proxying
                    await websocket.accept()
                    async for chunk in response.aiter_bytes():
                        await websocket.send_bytes(chunk)
        except Exception as e:
            logger.error(f"Error routing WebSocket: {e}")
            await websocket.close(code=1011, reason="Internal server error")

    def _filter_headers(self, headers: Dict[str, str]) -> Dict[str, str]:
        """Filter out hop-by-hop headers."""
        hop_by_hop = {
            "connection",
            "keep-alive",
            "proxy-authenticate",
            "proxy-authorization",
            "te",
            "trailers",
            "transfer-encoding",
            "upgrade",
            "host",
        }
        return {k: v for k, v in headers.items() if k.lower() not in hop_by_hop}

    async def get_server_for_request(self, server_name: Optional[str] = None) -> Optional[MCPServer]:
        """Get server for request (with load balancing)."""
        if server_name:
            return await self.registry.get_server_by_name(server_name)
        
        # Load balancing logic - get healthy servers
        servers = await self.registry.list_servers(
            status=ServerStatus.ACTIVE,
            health_status=HealthStatus.HEALTHY,
        )
        
        if not servers:
            return None
        
        # Simple round-robin (can be enhanced with more sophisticated algorithms)
        # For now, return first server
        return servers[0] if servers else None
```

### Step 2: Create Connection Pool Manager

#### gateway/src/router/connection_pool.py

Create connection pool manager:

```python
"""Connection pool manager for MCP servers."""
import logging
from typing import Dict
import httpx
from src.config import settings

logger = logging.getLogger(__name__)


class ConnectionPoolManager:
    """Manager for HTTP connection pools."""

    def __init__(self):
        """Initialize connection pool manager."""
        self.pools: Dict[str, httpx.AsyncClient] = {}

    async def get_client(self, server_id: str, endpoint: str) -> httpx.AsyncClient:
        """Get HTTP client for server."""
        if server_id not in self.pools:
            self.pools[server_id] = httpx.AsyncClient(
                base_url=endpoint,
                timeout=settings.MCP_SERVER_TIMEOUT,
                limits=httpx.Limits(max_keepalive_connections=10, max_connections=20),
            )
        return self.pools[server_id]

    async def close_client(self, server_id: str):
        """Close HTTP client for server."""
        if server_id in self.pools:
            await self.pools[server_id].aclose()
            del self.pools[server_id]

    async def close_all(self):
        """Close all HTTP clients."""
        for server_id in list(self.pools.keys()):
            await self.close_client(server_id)
```

### Step 3: Create Load Balancer

#### gateway/src/router/load_balancer.py

Create load balancer for multiple server instances:

```python
"""Load balancer for MCP servers."""
import logging
from typing import List
import random
from src.models.server import MCPServer

logger = logging.getLogger(__name__)


class LoadBalancer:
    """Load balancer for MCP servers."""

    @staticmethod
    def round_robin(servers: List[MCPServer], current_index: int = 0) -> MCPServer:
        """Round-robin load balancing."""
        if not servers:
            raise ValueError("No servers available")
        return servers[current_index % len(servers)]

    @staticmethod
    def random(servers: List[MCPServer]) -> MCPServer:
        """Random load balancing."""
        if not servers:
            raise ValueError("No servers available")
        return random.choice(servers)

    @staticmethod
    def least_connections(servers: List[MCPServer], connection_counts: Dict[str, int]) -> MCPServer:
        """Least connections load balancing."""
        if not servers:
            raise ValueError("No servers available")
        
        # Sort servers by connection count
        sorted_servers = sorted(servers, key=lambda s: connection_counts.get(str(s.id), 0))
        return sorted_servers[0]

    @staticmethod
    def health_based(servers: List[MCPServer]) -> MCPServer:
        """Health-based load balancing."""
        if not servers:
            raise ValueError("No servers available")
        
        # Filter healthy servers
        healthy_servers = [s for s in servers if s.health_status == "healthy"]
        if healthy_servers:
            return random.choice(healthy_servers)
        
        # Fallback to any server
        return servers[0]
```

### Step 4: Create Request Transformer

#### gateway/src/router/transformer.py

Create request transformer for modifying requests:

```python
"""Request transformer for modifying requests."""
import logging
from typing import Dict, Optional
from fastapi import Request

logger = logging.getLogger(__name__)


class RequestTransformer:
    """Transformer for modifying requests."""

    @staticmethod
    def add_headers(request: Request, headers: Dict[str, str]) -> Dict[str, str]:
        """Add headers to request."""
        request_headers = dict(request.headers)
        request_headers.update(headers)
        return request_headers

    @staticmethod
    def remove_headers(request: Request, headers_to_remove: List[str]) -> Dict[str, str]:
        """Remove headers from request."""
        request_headers = dict(request.headers)
        for header in headers_to_remove:
            request_headers.pop(header, None)
        return request_headers

    @staticmethod
    def transform_path(path: str, transformations: Dict[str, str]) -> str:
        """Transform request path."""
        transformed_path = path
        for old, new in transformations.items():
            transformed_path = transformed_path.replace(old, new)
        return transformed_path

    @staticmethod
    def add_query_params(query_params: Dict[str, str], new_params: Dict[str, str]) -> Dict[str, str]:
        """Add query parameters."""
        query_params.update(new_params)
        return query_params
```

### Step 5: Update Proxy Endpoints

#### gateway/src/api/proxy.py

Update proxy endpoints to use router service:

```python
"""Proxy endpoints for MCP servers."""
from fastapi import APIRouter, HTTPException, Request, WebSocket, Depends, status
from fastapi.responses import Response
from sqlalchemy.orm import Session

from src.router.service import RouterService
from src.database import get_db

router = APIRouter()


@router.api_route(
    "/{server_id}/{path:path}",
    methods=["GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"],
)
async def proxy_request(
    server_id: str,
    path: str,
    request: Request,
    db: Session = Depends(get_db),
):
    """Proxy HTTP request to MCP server."""
    try:
        router_service = RouterService(db)
        response = await router_service.route_request(server_id, path, request)
        return response
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e))


@router.websocket("/{server_id}/{path:path}")
async def proxy_websocket(
    server_id: str,
    path: str,
    websocket: WebSocket,
    db: Session = Depends(get_db),
):
    """Proxy WebSocket connection to MCP server."""
    try:
        router_service = RouterService(db)
        await router_service.route_websocket(server_id, path, websocket)
    except Exception as e:
        await websocket.close(code=1011, reason=str(e))
```

### Step 6: Create Middleware for Request Logging

#### gateway/src/middleware/request_logging.py

Create middleware for logging requests:

```python
"""Request logging middleware."""
import logging
import time
from fastapi import Request
from starlette.middleware.base import BaseHTTPMiddleware

logger = logging.getLogger(__name__)


class RequestLoggingMiddleware(BaseHTTPMiddleware):
    """Middleware for logging requests."""

    async def dispatch(self, request: Request, call_next):
        """Process request and log."""
        start_time = time.time()
        
        # Log request
        logger.info(
            f"Request: {request.method} {request.url.path} "
            f"from {request.client.host if request.client else 'unknown'}"
        )
        
        # Process request
        response = await call_next(request)
        
        # Calculate duration
        duration = time.time() - start_time
        
        # Log response
        logger.info(
            f"Response: {response.status_code} "
            f"for {request.method} {request.url.path} "
            f"took {duration:.3f}s"
        )
        
        # Add duration header
        response.headers["X-Response-Time"] = f"{duration:.3f}s"
        
        return response
```

### Step 7: Update Main Application

#### gateway/src/main.py

Add middleware and update main application:

```python
# Add to imports
from src.middleware.request_logging import RequestLoggingMiddleware

# Add middleware after CORS middleware
app.add_middleware(RequestLoggingMiddleware)
```

## Testing

### Test Request Routing

Test HTTP request routing:

```bash
# Register a test server
curl -X POST http://localhost:8000/api/servers \
  -H "Content-Type: application/json" \
  -d '{
    "name": "test-server",
    "description": "Test server",
    "endpoint": "http://httpbin.org",
    "config": {}
  }'

# Get server ID
SERVER_ID=$(curl -s http://localhost:8000/api/servers | jq -r '.[0].id')

# Start server
curl -X POST http://localhost:8000/api/servers/$SERVER_ID/start

# Test routing
curl http://localhost:8000/api/proxy/$SERVER_ID/get
```

### Test WebSocket Routing

Test WebSocket routing (requires WebSocket client):

```python
import asyncio
import websockets

async def test_websocket():
    uri = "ws://localhost:8000/api/proxy/{server_id}/ws"
    async with websockets.connect(uri) as websocket:
        await websocket.send("Hello")
        response = await websocket.recv()
        print(f"Received: {response}")

asyncio.run(test_websocket())
```

## Verification

1. **HTTP Routing**: HTTP requests are routed correctly
2. **WebSocket Routing**: WebSocket connections are routed correctly
3. **Load Balancing**: Load balancing works (if multiple servers)
4. **Error Handling**: Errors are handled gracefully
5. **Request Transformation**: Requests can be transformed
6. **Logging**: Requests are logged correctly

## Troubleshooting

### Issue: Request routing fails

**Solution**: Check server is registered and active:
```bash
curl http://localhost:8000/api/servers
curl http://localhost:8000/api/servers/{server_id}
```

### Issue: Connection timeout

**Solution**: Check server endpoint is accessible and increase timeout:
```python
# In router/service.py
self.client = httpx.AsyncClient(timeout=60.0)
```

### Issue: WebSocket connection fails

**Solution**: Verify WebSocket endpoint and check server supports WebSocket:
```bash
curl -i -N -H "Connection: Upgrade" -H "Upgrade: websocket" \
  http://localhost:8000/api/proxy/{server_id}/ws
```

## Next Steps

After completing this instruction, proceed to:
- **03-security/01-authentication.md**: Implement authentication
- **03-security/02-authorization.md**: Implement authorization

