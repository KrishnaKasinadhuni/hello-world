# 03: MCP Server Registry

## Objective

Implement the MCP server registry system that manages registration, discovery, health monitoring, and lifecycle management of MCP servers.

## Prerequisites

- Completed: 01-gateway-architecture.md
- Completed: 02-api-server.md
- Understanding of database models and SQLAlchemy
- Knowledge of Docker container management

## Implementation Steps

### Step 1: Create Database Models

#### gateway/src/models/server.py

Create MCP server database model:

```python
"""MCP server database models."""
from sqlalchemy import Column, String, Text, DateTime, JSON, Enum
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.sql import func
import uuid
import enum

from src.database import Base


class ServerStatus(str, enum.Enum):
    """Server status enumeration."""
    ACTIVE = "active"
    INACTIVE = "inactive"
    ERROR = "error"
    STARTING = "starting"
    STOPPING = "stopping"


class HealthStatus(str, enum.Enum):
    """Health status enumeration."""
    HEALTHY = "healthy"
    UNHEALTHY = "unhealthy"
    UNKNOWN = "unknown"


class MCPServer(Base):
    """MCP server model."""
    __tablename__ = "mcp_servers"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    name = Column(String(255), nullable=False, unique=True)
    description = Column(Text, nullable=True)
    endpoint = Column(String(500), nullable=False)
    status = Column(Enum(ServerStatus), default=ServerStatus.INACTIVE, nullable=False)
    container_id = Column(String(255), nullable=True)
    network = Column(String(255), nullable=False, default="mcp_servers")
    config = Column(JSON, nullable=False, default=dict)
    health_status = Column(Enum(HealthStatus), default=HealthStatus.UNKNOWN, nullable=False)
    last_health_check = Column(DateTime, nullable=True)
    created_at = Column(DateTime, server_default=func.now(), nullable=False)
    updated_at = Column(DateTime, server_default=func.now(), onupdate=func.now(), nullable=False)

    def to_dict(self):
        """Convert model to dictionary."""
        return {
            "id": str(self.id),
            "name": self.name,
            "description": self.description,
            "endpoint": self.endpoint,
            "status": self.status.value,
            "container_id": self.container_id,
            "network": self.network,
            "config": self.config,
            "health_status": self.health_status.value,
            "last_health_check": self.last_health_check.isoformat() if self.last_health_check else None,
            "created_at": self.created_at.isoformat(),
            "updated_at": self.updated_at.isoformat(),
        }
```

### Step 2: Create Database Schema Migration

#### gateway/alembic.ini

Create Alembic configuration:

```ini
[alembic]
script_location = alembic
prepend_sys_path = .
version_path_separator = os

[alembic:exclude]
tables = alembic_version

[sqlalchemy]
url = postgresql://postgres:postgres@localhost:5432/mcp_gateway
```

#### gateway/alembic/env.py

Create Alembic environment:

```python
"""Alembic environment configuration."""
from logging.config import fileConfig
from sqlalchemy import engine_from_config, pool
from alembic import context
import os
import sys

# Add parent directory to path
sys.path.insert(0, os.path.dirname(os.path.dirname(__file__)))

from src.database import Base
from src.models.server import MCPServer
from src.config import settings

config = context.config
config.set_main_option("sqlalchemy.url", settings.DATABASE_URL)

if config.config_file_name is not None:
    fileConfig(config.config_file_name)

target_metadata = Base.metadata


def run_migrations_offline():
    """Run migrations in 'offline' mode."""
    url = config.get_main_option("sqlalchemy.url")
    context.configure(
        url=url,
        target_metadata=target_metadata,
        literal_binds=True,
        dialect_opts={"paramstyle": "named"},
    )
    with context.begin_transaction():
        context.run_migrations()


def run_migrations_online():
    """Run migrations in 'online' mode."""
    connectable = engine_from_config(
        config.get_section(config.config_ini_section),
        prefix="sqlalchemy.",
        poolclass=pool.NullPool,
    )
    with connectable.connect() as connection:
        context.configure(
            connection=connection, target_metadata=target_metadata
        )
        with context.begin_transaction():
            context.run_migrations()


if context.is_offline_mode():
    run_migrations_offline()
else:
    run_migrations_online()
```

### Step 3: Create Registry Service

#### gateway/src/registry/service.py

Create registry service for managing MCP servers:

```python
"""MCP server registry service."""
import logging
from typing import List, Optional
from sqlalchemy.orm import Session
from sqlalchemy import and_

from src.models.server import MCPServer, ServerStatus, HealthStatus
from src.registry.container_manager import ContainerManager
from src.registry.health_checker import HealthChecker

logger = logging.getLogger(__name__)


class RegistryService:
    """Service for managing MCP server registry."""

    def __init__(self, db: Session):
        """Initialize registry service."""
        self.db = db
        self.container_manager = ContainerManager()
        self.health_checker = HealthChecker()

    async def register_server(
        self,
        name: str,
        description: str,
        endpoint: str,
        config: dict,
        network: str = "mcp_servers",
    ) -> MCPServer:
        """Register a new MCP server."""
        try:
            # Check if server with same name exists
            existing = self.db.query(MCPServer).filter(MCPServer.name == name).first()
            if existing:
                raise ValueError(f"Server with name '{name}' already exists")

            # Create server record
            server = MCPServer(
                name=name,
                description=description,
                endpoint=endpoint,
                config=config,
                network=network,
                status=ServerStatus.INACTIVE,
                health_status=HealthStatus.UNKNOWN,
            )
            self.db.add(server)
            self.db.commit()
            self.db.refresh(server)

            logger.info(f"Registered MCP server: {name}")
            return server
        except Exception as e:
            self.db.rollback()
            logger.error(f"Error registering server: {e}")
            raise

    async def list_servers(
        self,
        status: Optional[ServerStatus] = None,
        health_status: Optional[HealthStatus] = None,
    ) -> List[MCPServer]:
        """List all MCP servers."""
        query = self.db.query(MCPServer)
        
        if status:
            query = query.filter(MCPServer.status == status)
        if health_status:
            query = query.filter(MCPServer.health_status == health_status)
        
        return query.all()

    async def get_server(self, server_id: str) -> Optional[MCPServer]:
        """Get MCP server by ID."""
        return self.db.query(MCPServer).filter(MCPServer.id == server_id).first()

    async def get_server_by_name(self, name: str) -> Optional[MCPServer]:
        """Get MCP server by name."""
        return self.db.query(MCPServer).filter(MCPServer.name == name).first()

    async def update_server(
        self,
        server_id: str,
        name: Optional[str] = None,
        description: Optional[str] = None,
        endpoint: Optional[str] = None,
        config: Optional[dict] = None,
    ) -> Optional[MCPServer]:
        """Update MCP server."""
        try:
            server = await self.get_server(server_id)
            if not server:
                return None

            if name:
                server.name = name
            if description:
                server.description = description
            if endpoint:
                server.endpoint = endpoint
            if config:
                server.config = config

            self.db.commit()
            self.db.refresh(server)

            logger.info(f"Updated MCP server: {server_id}")
            return server
        except Exception as e:
            self.db.rollback()
            logger.error(f"Error updating server: {e}")
            raise

    async def delete_server(self, server_id: str) -> bool:
        """Delete MCP server."""
        try:
            server = await self.get_server(server_id)
            if not server:
                return False

            # Stop and remove container if running
            if server.container_id:
                await self.container_manager.stop_container(server.container_id)
                await self.container_manager.remove_container(server.container_id)

            self.db.delete(server)
            self.db.commit()

            logger.info(f"Deleted MCP server: {server_id}")
            return True
        except Exception as e:
            self.db.rollback()
            logger.error(f"Error deleting server: {e}")
            raise

    async def start_server(self, server_id: str) -> Optional[MCPServer]:
        """Start MCP server."""
        try:
            server = await self.get_server(server_id)
            if not server:
                return None

            if server.status == ServerStatus.ACTIVE:
                logger.warning(f"Server {server_id} is already active")
                return server

            # Start container
            container_id = await self.container_manager.start_container(
                server.name,
                server.endpoint,
                server.config,
                server.network,
            )

            # Update server status
            server.status = ServerStatus.ACTIVE
            server.container_id = container_id
            self.db.commit()
            self.db.refresh(server)

            logger.info(f"Started MCP server: {server_id}")
            return server
        except Exception as e:
            self.db.rollback()
            logger.error(f"Error starting server: {e}")
            server.status = ServerStatus.ERROR
            self.db.commit()
            raise

    async def stop_server(self, server_id: str) -> Optional[MCPServer]:
        """Stop MCP server."""
        try:
            server = await self.get_server(server_id)
            if not server:
                return None

            if server.status == ServerStatus.INACTIVE:
                logger.warning(f"Server {server_id} is already inactive")
                return server

            # Stop container
            if server.container_id:
                await self.container_manager.stop_container(server.container_id)

            # Update server status
            server.status = ServerStatus.INACTIVE
            self.db.commit()
            self.db.refresh(server)

            logger.info(f"Stopped MCP server: {server_id}")
            return server
        except Exception as e:
            self.db.rollback()
            logger.error(f"Error stopping server: {e}")
            raise

    async def check_health(self, server_id: str) -> Optional[MCPServer]:
        """Check health of MCP server."""
        try:
            server = await self.get_server(server_id)
            if not server:
                return None

            # Check health
            is_healthy = await self.health_checker.check_health(server.endpoint)

            # Update health status
            server.health_status = HealthStatus.HEALTHY if is_healthy else HealthStatus.UNHEALTHY
            server.last_health_check = func.now()
            self.db.commit()
            self.db.refresh(server)

            return server
        except Exception as e:
            logger.error(f"Error checking health: {e}")
            if server:
                server.health_status = HealthStatus.UNHEALTHY
                self.db.commit()
            raise
```

### Step 4: Create Container Manager

#### gateway/src/registry/container_manager.py

Create container manager for Docker operations:

```python
"""Container manager for MCP servers."""
import docker
import logging
from typing import Optional, Dict
from src.config import settings

logger = logging.getLogger(__name__)


class ContainerManager:
    """Manager for Docker containers."""

    def __init__(self):
        """Initialize container manager."""
        try:
            self.client = docker.from_env()
        except Exception as e:
            logger.error(f"Failed to initialize Docker client: {e}")
            raise

    async def start_container(
        self,
        name: str,
        endpoint: str,
        config: Dict,
        network: str = "mcp_servers",
    ) -> str:
        """Start container for MCP server."""
        try:
            # Parse endpoint to get image and command
            # This is a simplified version - adapt to your MCP server format
            image = config.get("image", "mcp-server:latest")
            command = config.get("command", [])
            env = config.get("environment", {})
            ports = config.get("ports", {})

            # Create container
            container = self.client.containers.run(
                image=image,
                command=command,
                environment=env,
                ports=ports,
                network=network,
                name=f"mcp-server-{name}",
                detach=True,
                restart_policy={"Name": "unless-stopped"},
                mem_limit=config.get("memory", "512m"),
                cpu_period=100000,
                cpu_quota=config.get("cpu_quota", 50000),
            )

            logger.info(f"Started container: {container.id}")
            return container.id
        except Exception as e:
            logger.error(f"Error starting container: {e}")
            raise

    async def stop_container(self, container_id: str) -> bool:
        """Stop container."""
        try:
            container = self.client.containers.get(container_id)
            container.stop(timeout=10)
            logger.info(f"Stopped container: {container_id}")
            return True
        except docker.errors.NotFound:
            logger.warning(f"Container not found: {container_id}")
            return False
        except Exception as e:
            logger.error(f"Error stopping container: {e}")
            raise

    async def remove_container(self, container_id: str) -> bool:
        """Remove container."""
        try:
            container = self.client.containers.get(container_id)
            container.remove(force=True)
            logger.info(f"Removed container: {container_id}")
            return True
        except docker.errors.NotFound:
            logger.warning(f"Container not found: {container_id}")
            return False
        except Exception as e:
            logger.error(f"Error removing container: {e}")
            raise

    async def get_container_status(self, container_id: str) -> Optional[str]:
        """Get container status."""
        try:
            container = self.client.containers.get(container_id)
            return container.status
        except docker.errors.NotFound:
            return None
        except Exception as e:
            logger.error(f"Error getting container status: {e}")
            return None
```

### Step 5: Create Health Checker

#### gateway/src/registry/health_checker.py

Create health checker for MCP servers:

```python
"""Health checker for MCP servers."""
import httpx
import logging
from src.config import settings

logger = logging.getLogger(__name__)


class HealthChecker:
    """Health checker for MCP servers."""

    def __init__(self):
        """Initialize health checker."""
        self.timeout = settings.MCP_SERVER_TIMEOUT

    async def check_health(self, endpoint: str) -> bool:
        """Check health of MCP server."""
        try:
            # Try to connect to health endpoint
            health_url = f"{endpoint}/health"
            async with httpx.AsyncClient(timeout=self.timeout) as client:
                response = await client.get(health_url)
                return response.status_code == 200
        except Exception as e:
            logger.error(f"Health check failed for {endpoint}: {e}")
            return False

    async def check_health_with_details(self, endpoint: str) -> dict:
        """Check health with detailed information."""
        try:
            health_url = f"{endpoint}/health"
            async with httpx.AsyncClient(timeout=self.timeout) as client:
                response = await client.get(health_url)
                return {
                    "healthy": response.status_code == 200,
                    "status_code": response.status_code,
                    "response": response.json() if response.status_code == 200 else None,
                }
        except Exception as e:
            logger.error(f"Health check failed for {endpoint}: {e}")
            return {
                "healthy": False,
                "error": str(e),
            }
```

### Step 6: Update API Endpoints

#### gateway/src/api/servers.py

Update server endpoints to use registry service:

```python
"""MCP server management endpoints."""
from fastapi import APIRouter, HTTPException, status, Depends
from pydantic import BaseModel
from typing import List, Optional
from datetime import datetime
from sqlalchemy.orm import Session

from src.registry.service import RegistryService
from src.database import get_db
from src.models.server import ServerStatus, HealthStatus

router = APIRouter()


class MCPServerCreate(BaseModel):
    """MCP server creation model."""
    name: str
    description: str
    endpoint: str
    config: dict
    network: Optional[str] = "mcp_servers"


class MCPServerUpdate(BaseModel):
    """MCP server update model."""
    name: Optional[str] = None
    description: Optional[str] = None
    endpoint: Optional[str] = None
    config: Optional[dict] = None


class MCPServerResponse(BaseModel):
    """MCP server response model."""
    id: str
    name: str
    description: Optional[str]
    endpoint: str
    status: str
    container_id: Optional[str]
    network: str
    config: dict
    health_status: str
    last_health_check: Optional[str]
    created_at: str
    updated_at: str

    class Config:
        from_attributes = True


@router.post("/", response_model=MCPServerResponse, status_code=status.HTTP_201_CREATED)
async def register_server(server: MCPServerCreate, db: Session = Depends(get_db)):
    """Register MCP server."""
    try:
        registry = RegistryService(db)
        registered_server = await registry.register_server(
            name=server.name,
            description=server.description,
            endpoint=server.endpoint,
            config=server.config,
            network=server.network,
        )
        return MCPServerResponse(**registered_server.to_dict())
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e))


@router.get("/", response_model=List[MCPServerResponse])
async def list_servers(
    status: Optional[ServerStatus] = None,
    health_status: Optional[HealthStatus] = None,
    db: Session = Depends(get_db),
):
    """List all MCP servers."""
    registry = RegistryService(db)
    servers = await registry.list_servers(status=status, health_status=health_status)
    return [MCPServerResponse(**server.to_dict()) for server in servers]


@router.get("/{server_id}", response_model=MCPServerResponse)
async def get_server(server_id: str, db: Session = Depends(get_db)):
    """Get MCP server details."""
    registry = RegistryService(db)
    server = await registry.get_server(server_id)
    if not server:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Server not found")
    return MCPServerResponse(**server.to_dict())


@router.put("/{server_id}", response_model=MCPServerResponse)
async def update_server(
    server_id: str,
    server: MCPServerUpdate,
    db: Session = Depends(get_db),
):
    """Update MCP server."""
    registry = RegistryService(db)
    updated_server = await registry.update_server(
        server_id=server_id,
        name=server.name,
        description=server.description,
        endpoint=server.endpoint,
        config=server.config,
    )
    if not updated_server:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Server not found")
    return MCPServerResponse(**updated_server.to_dict())


@router.delete("/{server_id}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_server(server_id: str, db: Session = Depends(get_db)):
    """Delete MCP server."""
    registry = RegistryService(db)
    deleted = await registry.delete_server(server_id)
    if not deleted:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Server not found")


@router.post("/{server_id}/start", response_model=MCPServerResponse)
async def start_server(server_id: str, db: Session = Depends(get_db)):
    """Start MCP server."""
    registry = RegistryService(db)
    server = await registry.start_server(server_id)
    if not server:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Server not found")
    return MCPServerResponse(**server.to_dict())


@router.post("/{server_id}/stop", response_model=MCPServerResponse)
async def stop_server(server_id: str, db: Session = Depends(get_db)):
    """Stop MCP server."""
    registry = RegistryService(db)
    server = await registry.stop_server(server_id)
    if not server:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Server not found")
    return MCPServerResponse(**server.to_dict())


@router.post("/{server_id}/health", response_model=MCPServerResponse)
async def check_health(server_id: str, db: Session = Depends(get_db)):
    """Check health of MCP server."""
    registry = RegistryService(db)
    server = await registry.check_health(server_id)
    if not server:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Server not found")
    return MCPServerResponse(**server.to_dict())
```

## Testing

### Test Database Models

Create and test database models:

```bash
# Create migration
cd gateway
alembic revision --autogenerate -m "Create MCP servers table"

# Apply migration
alembic upgrade head

# Test model
python -c "from src.models.server import MCPServer; print('Model imported successfully')"
```

### Test Registry Service

Test registry service:

```bash
# Test server registration
python -c "
from src.database import SessionLocal
from src.registry.service import RegistryService
import asyncio

async def test():
    db = SessionLocal()
    registry = RegistryService(db)
    server = await registry.register_server(
        name='test-server',
        description='Test server',
        endpoint='http://localhost:9000',
        config={'image': 'test:latest'}
    )
    print(f'Registered server: {server.id}')
    db.close()

asyncio.run(test())
"
```

## Verification

1. **Database Models**: Models are created and migrations work
2. **Registry Service**: Service can register, list, and manage servers
3. **Container Manager**: Can start and stop containers
4. **Health Checker**: Can check server health
5. **API Endpoints**: All endpoints work correctly

## Troubleshooting

### Issue: Database migration fails

**Solution**: Check database connection and ensure PostgreSQL is running:
```bash
docker-compose ps postgres
alembic upgrade head
```

### Issue: Docker client fails

**Solution**: Ensure Docker is running and accessible:
```bash
docker ps
docker info
```

### Issue: Container start fails

**Solution**: Check Docker network exists and image is available:
```bash
docker network ls
docker images
```

## Next Steps

After completing this instruction, proceed to:
- **04-request-routing.md**: Implement request routing

