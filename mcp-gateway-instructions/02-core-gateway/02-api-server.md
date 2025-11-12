# 02: API Server

## Objective

Implement the main API server for the MCP Gateway using FastAPI, including REST endpoints, WebSocket support, middleware, and error handling.

## Prerequisites

- Completed: 01-gateway-architecture.md
- Completed: 01-setup/01-project-structure.md
- Completed: 01-setup/02-dependencies.md
- Understanding of FastAPI framework
- Knowledge of REST API design

## Implementation Steps

### Step 1: Create Main Application File

#### gateway/src/main.py

Create the main FastAPI application:

```python
"""Main application entry point for MCP Gateway."""
import logging
from contextlib import asynccontextmanager
from fastapi import FastAPI, Request, status
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from fastapi.exceptions import RequestValidationError
from starlette.exceptions import HTTPException as StarletteHTTPException

from src.api import auth, servers, proxy
from src.config import settings
from src.database import init_db, close_db
from src.redis_client import init_redis, close_redis
from src.logging_config import setup_logging

# Setup logging
setup_logging()
logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Lifespan context manager for startup and shutdown."""
    # Startup
    logger.info("Starting MCP Gateway...")
    await init_db()
    await init_redis()
    logger.info("MCP Gateway started successfully")
    yield
    # Shutdown
    logger.info("Shutting down MCP Gateway...")
    await close_db()
    await close_redis()
    logger.info("MCP Gateway shut down successfully")


# Create FastAPI application
app = FastAPI(
    title="MCP Gateway",
    description="Secure gateway for hosting MCP servers",
    version="0.1.0",
    lifespan=lifespan,
    docs_url="/api/docs" if settings.DEBUG else None,
    redoc_url="/api/redoc" if settings.DEBUG else None,
)

# CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.CORS_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
    expose_headers=["*"],
)


# Exception handlers
@app.exception_handler(StarletteHTTPException)
async def http_exception_handler(request: Request, exc: StarletteHTTPException):
    """Handle HTTP exceptions."""
    logger.error(f"HTTP error: {exc.status_code} - {exc.detail}")
    return JSONResponse(
        status_code=exc.status_code,
        content={
            "error": True,
            "message": exc.detail,
            "status_code": exc.status_code,
        },
    )


@app.exception_handler(RequestValidationError)
async def validation_exception_handler(request: Request, exc: RequestValidationError):
    """Handle validation exceptions."""
    logger.error(f"Validation error: {exc.errors()}")
    return JSONResponse(
        status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
        content={
            "error": True,
            "message": "Validation error",
            "details": exc.errors(),
        },
    )


@app.exception_handler(Exception)
async def general_exception_handler(request: Request, exc: Exception):
    """Handle general exceptions."""
    logger.exception(f"Unhandled exception: {exc}")
    return JSONResponse(
        status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
        content={
            "error": True,
            "message": "Internal server error",
            "details": str(exc) if settings.DEBUG else None,
        },
    )


# Health check endpoint
@app.get("/health")
async def health_check():
    """Health check endpoint."""
    return {
        "status": "healthy",
        "service": "mcp-gateway",
        "version": "0.1.0",
    }


# Root endpoint
@app.get("/")
async def root():
    """Root endpoint."""
    return {
        "message": "MCP Gateway",
        "version": "0.1.0",
        "docs": "/api/docs" if settings.DEBUG else None,
    }


# Include routers
app.include_router(auth.router, prefix="/api/auth", tags=["authentication"])
app.include_router(servers.router, prefix="/api/servers", tags=["mcp-servers"])
app.include_router(proxy.router, prefix="/api/proxy", tags=["proxy"])


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(
        "src.main:app",
        host=settings.GATEWAY_HOST,
        port=settings.GATEWAY_PORT,
        reload=settings.DEBUG,
    )
```

### Step 2: Create Configuration Module

#### gateway/src/config.py

Create configuration settings:

```python
"""Configuration settings for MCP Gateway."""
from pydantic_settings import BaseSettings
from typing import List
import os


class Settings(BaseSettings):
    """Application settings."""
    
    # Application
    GATEWAY_HOST: str = "0.0.0.0"
    GATEWAY_PORT: int = 8000
    ENVIRONMENT: str = "development"
    DEBUG: bool = True
    
    # Database
    DATABASE_URL: str = "postgresql://postgres:postgres@localhost:5432/mcp_gateway"
    DATABASE_POOL_SIZE: int = 10
    DATABASE_MAX_OVERFLOW: int = 20
    
    # Redis
    REDIS_HOST: str = "localhost"
    REDIS_PORT: int = 6379
    REDIS_PASSWORD: str = ""
    REDIS_DB: int = 0
    
    # JWT
    JWT_SECRET_KEY: str = "change-me-in-production"
    JWT_ALGORITHM: str = "HS256"
    JWT_ACCESS_TOKEN_EXPIRE_MINUTES: int = 30
    JWT_REFRESH_TOKEN_EXPIRE_DAYS: int = 7
    
    # CORS
    CORS_ORIGINS: List[str] = ["*"]
    
    # Rate Limiting
    RATE_LIMIT_ENABLED: bool = True
    RATE_LIMIT_PER_MINUTE: int = 60
    RATE_LIMIT_PER_HOUR: int = 1000
    
    # Logging
    LOG_LEVEL: str = "INFO"
    LOG_FILE_PATH: str = "/var/log/mcp-gateway/gateway.log"
    
    # MCP Server Configuration
    MCP_SERVER_NETWORK: str = "mcp_servers"
    MCP_SERVER_MAX_INSTANCES: int = 10
    MCP_SERVER_TIMEOUT: int = 30
    
    class Config:
        env_file = ".env"
        case_sensitive = True


settings = Settings()
```

### Step 3: Create Database Module

#### gateway/src/database.py

Create database connection and session management:

```python
"""Database connection and session management."""
from sqlalchemy import create_engine
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import sessionmaker
from sqlalchemy.pool import NullPool
import logging

from src.config import settings

logger = logging.getLogger(__name__)

# Create database engine
engine = create_engine(
    settings.DATABASE_URL,
    pool_size=settings.DATABASE_POOL_SIZE,
    max_overflow=settings.DATABASE_MAX_OVERFLOW,
    pool_pre_ping=True,
    echo=settings.DEBUG,
)

# Create session factory
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)

# Create base class for models
Base = declarative_base()


async def init_db():
    """Initialize database connection."""
    try:
        # Test connection
        with engine.connect() as conn:
            conn.execute("SELECT 1")
        logger.info("Database connection established")
    except Exception as e:
        logger.error(f"Failed to connect to database: {e}")
        raise


async def close_db():
    """Close database connection."""
    try:
        engine.dispose()
        logger.info("Database connection closed")
    except Exception as e:
        logger.error(f"Error closing database connection: {e}")


def get_db():
    """Get database session."""
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()
```

### Step 4: Create Redis Client Module

#### gateway/src/redis_client.py

Create Redis client:

```python
"""Redis client for caching and rate limiting."""
import redis
import logging
from src.config import settings

logger = logging.getLogger(__name__)

redis_client = None


async def init_redis():
    """Initialize Redis connection."""
    global redis_client
    try:
        redis_client = redis.Redis(
            host=settings.REDIS_HOST,
            port=settings.REDIS_PORT,
            password=settings.REDIS_PASSWORD if settings.REDIS_PASSWORD else None,
            db=settings.REDIS_DB,
            decode_responses=True,
            socket_connect_timeout=5,
            socket_timeout=5,
        )
        # Test connection
        redis_client.ping()
        logger.info("Redis connection established")
    except Exception as e:
        logger.error(f"Failed to connect to Redis: {e}")
        raise


async def close_redis():
    """Close Redis connection."""
    global redis_client
    try:
        if redis_client:
            redis_client.close()
            logger.info("Redis connection closed")
    except Exception as e:
        logger.error(f"Error closing Redis connection: {e}")


def get_redis():
    """Get Redis client."""
    return redis_client
```

### Step 5: Create Logging Configuration

#### gateway/src/logging_config.py

Create logging configuration:

```python
"""Logging configuration."""
import logging
import sys
from pathlib import Path
from src.config import settings

def setup_logging():
    """Setup logging configuration."""
    # Create log directory if it doesn't exist
    log_file_path = Path(settings.LOG_FILE_PATH)
    log_file_path.parent.mkdir(parents=True, exist_ok=True)
    
    # Configure logging
    logging.basicConfig(
        level=getattr(logging, settings.LOG_LEVEL.upper()),
        format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
        handlers=[
            logging.FileHandler(settings.LOG_FILE_PATH),
            logging.StreamHandler(sys.stdout),
        ],
    )
    
    # Set log levels for specific libraries
    logging.getLogger("uvicorn").setLevel(logging.INFO)
    logging.getLogger("sqlalchemy").setLevel(logging.WARNING)
```

### Step 6: Create Authentication Router

#### gateway/src/api/auth.py

Create authentication endpoints (basic structure):

```python
"""Authentication endpoints."""
from fastapi import APIRouter, Depends, HTTPException, status
from fastapi.security import OAuth2PasswordBearer, OAuth2PasswordRequestForm
from pydantic import BaseModel

router = APIRouter()

oauth2_scheme = OAuth2PasswordBearer(tokenUrl="/api/auth/login")


class Token(BaseModel):
    """Token response model."""
    access_token: str
    token_type: str
    refresh_token: str


class User(BaseModel):
    """User model."""
    id: str
    username: str
    email: str


@router.post("/login", response_model=Token)
async def login(form_data: OAuth2PasswordRequestForm = Depends()):
    """Login endpoint."""
    # TODO: Implement authentication logic
    raise HTTPException(
        status_code=status.HTTP_501_NOT_IMPLEMENTED,
        detail="Authentication not yet implemented",
    )


@router.post("/logout")
async def logout():
    """Logout endpoint."""
    # TODO: Implement logout logic
    return {"message": "Logged out successfully"}


@router.post("/refresh")
async def refresh_token():
    """Refresh token endpoint."""
    # TODO: Implement token refresh logic
    raise HTTPException(
        status_code=status.HTTP_501_NOT_IMPLEMENTED,
        detail="Token refresh not yet implemented",
    )


@router.get("/me", response_model=User)
async def get_current_user():
    """Get current user endpoint."""
    # TODO: Implement user retrieval logic
    raise HTTPException(
        status_code=status.HTTP_501_NOT_IMPLEMENTED,
        detail="User retrieval not yet implemented",
    )
```

### Step 7: Create MCP Servers Router

#### gateway/src/api/servers.py

Create MCP server management endpoints (basic structure):

```python
"""MCP server management endpoints."""
from fastapi import APIRouter, HTTPException, status
from pydantic import BaseModel
from typing import List, Optional
from datetime import datetime

router = APIRouter()


class MCPServerCreate(BaseModel):
    """MCP server creation model."""
    name: str
    description: str
    endpoint: str
    config: dict


class MCPServer(BaseModel):
    """MCP server model."""
    id: str
    name: str
    description: str
    endpoint: str
    status: str
    container_id: Optional[str]
    network: str
    config: dict
    created_at: datetime
    updated_at: datetime
    health_status: str
    last_health_check: Optional[datetime]


@router.post("/", response_model=MCPServer, status_code=status.HTTP_201_CREATED)
async def register_server(server: MCPServerCreate):
    """Register MCP server."""
    # TODO: Implement server registration logic
    raise HTTPException(
        status_code=status.HTTP_501_NOT_IMPLEMENTED,
        detail="Server registration not yet implemented",
    )


@router.get("/", response_model=List[MCPServer])
async def list_servers():
    """List all MCP servers."""
    # TODO: Implement server listing logic
    return []


@router.get("/{server_id}", response_model=MCPServer)
async def get_server(server_id: str):
    """Get MCP server details."""
    # TODO: Implement server retrieval logic
    raise HTTPException(
        status_code=status.HTTP_404_NOT_FOUND,
        detail="Server not found",
    )


@router.put("/{server_id}", response_model=MCPServer)
async def update_server(server_id: str, server: MCPServerCreate):
    """Update MCP server."""
    # TODO: Implement server update logic
    raise HTTPException(
        status_code=status.HTTP_501_NOT_IMPLEMENTED,
        detail="Server update not yet implemented",
    )


@router.delete("/{server_id}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_server(server_id: str):
    """Delete MCP server."""
    # TODO: Implement server deletion logic
    raise HTTPException(
        status_code=status.HTTP_501_NOT_IMPLEMENTED,
        detail="Server deletion not yet implemented",
    )
```

### Step 8: Create Proxy Router

#### gateway/src/api/proxy.py

Create proxy endpoints (basic structure):

```python
"""Proxy endpoints for MCP servers."""
from fastapi import APIRouter, HTTPException, Request
from fastapi.responses import StreamingResponse
import httpx

router = APIRouter()


@router.post("/{server_id}/{path:path}")
async def proxy_request(server_id: str, path: str, request: Request):
    """Proxy HTTP request to MCP server."""
    # TODO: Implement request proxying logic
    raise HTTPException(
        status_code=status.HTTP_501_NOT_IMPLEMENTED,
        detail="Request proxying not yet implemented",
    )


@router.websocket("/{server_id}/{path:path}")
async def proxy_websocket(server_id: str, path: str, websocket):
    """Proxy WebSocket connection to MCP server."""
    # TODO: Implement WebSocket proxying logic
    await websocket.close(code=1003, reason="Not implemented")
```

### Step 9: Create API Init File

#### gateway/src/api/__init__.py

Create API package init file:

```python
"""API package."""
```

## Testing

### Test API Server Startup

Start the server and test basic endpoints:

```bash
# Start server
cd gateway
uvicorn src.main:app --reload

# Test health endpoint
curl http://localhost:8000/health

# Test root endpoint
curl http://localhost:8000/

# Test API docs
curl http://localhost:8000/api/docs
```

### Test Database Connection

Test database connectivity:

```bash
# Test database connection
python -c "from src.database import init_db; import asyncio; asyncio.run(init_db())"
```

### Test Redis Connection

Test Redis connectivity:

```bash
# Test Redis connection
python -c "from src.redis_client import init_redis; import asyncio; asyncio.run(init_redis())"
```

## Verification

1. **Server Starts**: API server starts without errors
2. **Health Check**: Health endpoint returns healthy status
3. **Database**: Database connection is established
4. **Redis**: Redis connection is established
5. **Endpoints**: All endpoints are accessible
6. **Error Handling**: Error handling works correctly
7. **Logging**: Logging is configured and working

## Troubleshooting

### Issue: Server won't start

**Solution**: Check dependencies are installed and configuration is correct:
```bash
pip install -r requirements.txt
python -m src.main
```

### Issue: Database connection fails

**Solution**: Verify database is running and credentials are correct:
```bash
docker-compose ps postgres
docker-compose exec postgres psql -U postgres -c "SELECT version();"
```

### Issue: Redis connection fails

**Solution**: Verify Redis is running and accessible:
```bash
docker-compose ps redis
docker-compose exec redis redis-cli ping
```

### Issue: Import errors

**Solution**: Ensure PYTHONPATH is set correctly:
```bash
export PYTHONPATH="${PYTHONPATH}:$(pwd)"
```

## Next Steps

After completing this instruction, proceed to:
- **03-mcp-server-registry.md**: Implement MCP server registry
- **04-request-routing.md**: Implement request routing

