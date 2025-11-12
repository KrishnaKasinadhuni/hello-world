# 02: Authorization

## Objective

Implement role-based access control (RBAC) system for the MCP Gateway, including permission checks, role management, and fine-grained access control for MCP servers.

## Prerequisites

- Completed: 01-authentication.md
- Understanding of RBAC (Role-Based Access Control)
- Knowledge of permission systems
- Understanding of access control patterns

## Implementation Steps

### Step 1: Create Permission Model

#### gateway/src/models/permission.py

Create permission database model:

```python
"""Permission database models."""
from sqlalchemy import Column, String, DateTime, Boolean, ForeignKey, Table
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import relationship
from sqlalchemy.sql import func
import uuid

from src.database import Base

# Association table for user roles
user_roles = Table(
    "user_roles",
    Base.metadata,
    Column("user_id", UUID(as_uuid=True), ForeignKey("users.id"), primary_key=True),
    Column("role_id", UUID(as_uuid=True), ForeignKey("roles.id"), primary_key=True),
)

# Association table for role permissions
role_permissions = Table(
    "role_permissions",
    Base.metadata,
    Column("role_id", UUID(as_uuid=True), ForeignKey("roles.id"), primary_key=True),
    Column("permission_id", UUID(as_uuid=True), ForeignKey("permissions.id"), primary_key=True),
)


class Role(Base):
    """Role model."""
    __tablename__ = "roles"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    name = Column(String(255), nullable=False, unique=True)
    description = Column(String(500), nullable=True)
    created_at = Column(DateTime, server_default=func.now(), nullable=False)
    updated_at = Column(DateTime, server_default=func.now(), onupdate=func.now(), nullable=False)

    permissions = relationship("Permission", secondary=role_permissions, back_populates="roles")
    users = relationship("User", secondary=user_roles, back_populates="roles")

    def to_dict(self):
        """Convert model to dictionary."""
        return {
            "id": str(self.id),
            "name": self.name,
            "description": self.description,
            "permissions": [p.to_dict() for p in self.permissions],
            "created_at": self.created_at.isoformat(),
            "updated_at": self.updated_at.isoformat(),
        }


class Permission(Base):
    """Permission model."""
    __tablename__ = "permissions"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    name = Column(String(255), nullable=False, unique=True)
    resource = Column(String(255), nullable=False)
    action = Column(String(255), nullable=False)
    description = Column(String(500), nullable=True)
    created_at = Column(DateTime, server_default=func.now(), nullable=False)

    roles = relationship("Role", secondary=role_permissions, back_populates="permissions")

    def to_dict(self):
        """Convert model to dictionary."""
        return {
            "id": str(self.id),
            "name": self.name,
            "resource": self.resource,
            "action": self.action,
            "description": self.description,
            "created_at": self.created_at.isoformat(),
        }
```

### Step 2: Update User Model

#### gateway/src/models/user.py

Update user model to include roles:

```python
# Add to imports
from sqlalchemy.orm import relationship
from src.models.permission import user_roles, Role

# Add to User class
roles_rel = relationship("Role", secondary=user_roles, back_populates="users")
```

### Step 3: Create Authorization Service

#### gateway/src/auth/authorization.py

Create authorization service:

```python
"""Authorization service."""
import logging
from typing import List, Optional
from sqlalchemy.orm import Session
from sqlalchemy import and_, or_

from src.models.user import User
from src.models.permission import Role, Permission

logger = logging.getLogger(__name__)


class AuthorizationService:
    """Service for authorization."""

    def __init__(self, db: Session):
        """Initialize authorization service."""
        self.db = db

    async def has_permission(self, user: User, resource: str, action: str) -> bool:
        """Check if user has permission for resource and action."""
        # Superusers have all permissions
        if user.is_superuser:
            return True

        # Check user roles and permissions
        for role in user.roles_rel:
            for permission in role.permissions:
                if permission.resource == resource and permission.action == action:
                    return True

        return False

    async def has_any_permission(self, user: User, permissions: List[tuple]) -> bool:
        """Check if user has any of the specified permissions."""
        for resource, action in permissions:
            if await self.has_permission(user, resource, action):
                return True
        return False

    async def has_all_permissions(self, user: User, permissions: List[tuple]) -> bool:
        """Check if user has all of the specified permissions."""
        for resource, action in permissions:
            if not await self.has_permission(user, resource, action):
                return False
        return True

    async def can_access_server(self, user: User, server_id: str) -> bool:
        """Check if user can access MCP server."""
        # Superusers can access all servers
        if user.is_superuser:
            return True

        # Check if user has server access permission
        return await self.has_permission(user, "server", "access") or \
               await self.has_permission(user, f"server:{server_id}", "access")

    async def can_manage_server(self, user: User, server_id: Optional[str] = None) -> bool:
        """Check if user can manage MCP server."""
        # Superusers can manage all servers
        if user.is_superuser:
            return True

        # Check if user has server management permission
        if server_id:
            return await self.has_permission(user, f"server:{server_id}", "manage")
        return await self.has_permission(user, "server", "manage")

    async def create_role(
        self,
        name: str,
        description: Optional[str] = None,
        permission_ids: Optional[List[str]] = None,
    ) -> Role:
        """Create a new role."""
        # Check if role exists
        existing_role = self.db.query(Role).filter(Role.name == name).first()
        if existing_role:
            raise ValueError(f"Role with name '{name}' already exists")

        # Create role
        role = Role(name=name, description=description)
        self.db.add(role)
        self.db.flush()

        # Add permissions
        if permission_ids:
            permissions = self.db.query(Permission).filter(
                Permission.id.in_(permission_ids)
            ).all()
            role.permissions.extend(permissions)

        self.db.commit()
        self.db.refresh(role)

        logger.info(f"Created role: {name}")
        return role

    async def create_permission(
        self,
        name: str,
        resource: str,
        action: str,
        description: Optional[str] = None,
    ) -> Permission:
        """Create a new permission."""
        # Check if permission exists
        existing_permission = self.db.query(Permission).filter(
            and_(Permission.resource == resource, Permission.action == action)
        ).first()
        if existing_permission:
            raise ValueError(f"Permission for {resource}:{action} already exists")

        # Create permission
        permission = Permission(
            name=name,
            resource=resource,
            action=action,
            description=description,
        )
        self.db.add(permission)
        self.db.commit()
        self.db.refresh(permission)

        logger.info(f"Created permission: {name}")
        return permission

    async def assign_role_to_user(self, user_id: str, role_id: str) -> bool:
        """Assign role to user."""
        user = self.db.query(User).filter(User.id == user_id).first()
        role = self.db.query(Role).filter(Role.id == role_id).first()

        if not user or not role:
            return False

        if role not in user.roles_rel:
            user.roles_rel.append(role)
            self.db.commit()
            logger.info(f"Assigned role {role.name} to user {user.username}")
        return True

    async def remove_role_from_user(self, user_id: str, role_id: str) -> bool:
        """Remove role from user."""
        user = self.db.query(User).filter(User.id == user_id).first()
        role = self.db.query(Role).filter(Role.id == role_id).first()

        if not user or not role:
            return False

        if role in user.roles_rel:
            user.roles_rel.remove(role)
            self.db.commit()
            logger.info(f"Removed role {role.name} from user {user.username}")
        return True
```

### Step 4: Create Authorization Dependencies

#### gateway/src/auth/dependencies.py

Add authorization dependencies:

```python
# Add to imports
from src.auth.authorization import AuthorizationService
from fastapi import HTTPException, status

# Add new dependencies
async def require_permission(
    resource: str,
    action: str,
    current_user: User = Depends(get_current_active_user),
    db: Session = Depends(get_db),
) -> User:
    """Require specific permission."""
    auth_service = AuthorizationService(db)
    if not await auth_service.has_permission(current_user, resource, action):
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail=f"Permission denied: {resource}:{action}",
        )
    return current_user


async def require_server_access(
    server_id: str,
    current_user: User = Depends(get_current_active_user),
    db: Session = Depends(get_db),
) -> User:
    """Require server access permission."""
    auth_service = AuthorizationService(db)
    if not await auth_service.can_access_server(current_user, server_id):
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail=f"Access denied to server: {server_id}",
        )
    return current_user


async def require_server_management(
    server_id: Optional[str] = None,
    current_user: User = Depends(get_current_active_user),
    db: Session = Depends(get_db),
) -> User:
    """Require server management permission."""
    auth_service = AuthorizationService(db)
    if not await auth_service.can_manage_server(current_user, server_id):
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Permission denied: server management",
        )
    return current_user
```

### Step 5: Update API Endpoints with Authorization

#### gateway/src/api/servers.py

Update server endpoints to include authorization:

```python
# Add to imports
from src.auth.dependencies import require_server_access, require_server_management

# Update endpoints
@router.post("/", response_model=MCPServerResponse, status_code=status.HTTP_201_CREATED)
async def register_server(
    server: MCPServerCreate,
    db: Session = Depends(get_db),
    current_user: User = Depends(require_server_management),
):
    """Register MCP server."""
    # ... existing code ...

@router.get("/{server_id}", response_model=MCPServerResponse)
async def get_server(
    server_id: str,
    db: Session = Depends(get_db),
    current_user: User = Depends(require_server_access),
):
    """Get MCP server details."""
    # ... existing code ...

@router.put("/{server_id}", response_model=MCPServerResponse)
async def update_server(
    server_id: str,
    server: MCPServerUpdate,
    db: Session = Depends(get_db),
    current_user: User = Depends(require_server_management),
):
    """Update MCP server."""
    # ... existing code ...

@router.delete("/{server_id}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_server(
    server_id: str,
    db: Session = Depends(get_db),
    current_user: User = Depends(require_server_management),
):
    """Delete MCP server."""
    # ... existing code ...
```

### Step 6: Create Role Management API

#### gateway/src/api/roles.py

Create role management endpoints:

```python
"""Role and permission management endpoints."""
from fastapi import APIRouter, Depends, HTTPException, status
from pydantic import BaseModel
from typing import List, Optional
from sqlalchemy.orm import Session

from src.auth.authorization import AuthorizationService
from src.auth.dependencies import get_current_superuser
from src.database import get_db
from src.models.user import User
from src.models.permission import Role, Permission

router = APIRouter()


class RoleCreate(BaseModel):
    """Role creation model."""
    name: str
    description: Optional[str] = None
    permission_ids: Optional[List[str]] = []


class PermissionCreate(BaseModel):
    """Permission creation model."""
    name: str
    resource: str
    action: str
    description: Optional[str] = None


class RoleResponse(BaseModel):
    """Role response model."""
    id: str
    name: str
    description: Optional[str]
    permissions: List[dict]
    created_at: str
    updated_at: str

    class Config:
        from_attributes = True


@router.post("/roles", response_model=RoleResponse, status_code=status.HTTP_201_CREATED)
async def create_role(
    role: RoleCreate,
    db: Session = Depends(get_db),
    current_user: User = Depends(get_current_superuser),
):
    """Create a new role."""
    auth_service = AuthorizationService(db)
    try:
        created_role = await auth_service.create_role(
            name=role.name,
            description=role.description,
            permission_ids=role.permission_ids,
        )
        return RoleResponse(**created_role.to_dict())
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))


@router.post("/permissions", response_model=dict, status_code=status.HTTP_201_CREATED)
async def create_permission(
    permission: PermissionCreate,
    db: Session = Depends(get_db),
    current_user: User = Depends(get_current_superuser),
):
    """Create a new permission."""
    auth_service = AuthorizationService(db)
    try:
        created_permission = await auth_service.create_permission(
            name=permission.name,
            resource=permission.resource,
            action=permission.action,
            description=permission.description,
        )
        return created_permission.to_dict()
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))


@router.post("/users/{user_id}/roles/{role_id}")
async def assign_role_to_user(
    user_id: str,
    role_id: str,
    db: Session = Depends(get_db),
    current_user: User = Depends(get_current_superuser),
):
    """Assign role to user."""
    auth_service = AuthorizationService(db)
    success = await auth_service.assign_role_to_user(user_id, role_id)
    if not success:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="User or role not found")
    return {"message": "Role assigned successfully"}


@router.delete("/users/{user_id}/roles/{role_id}")
async def remove_role_from_user(
    user_id: str,
    role_id: str,
    db: Session = Depends(get_db),
    current_user: User = Depends(get_current_superuser),
):
    """Remove role from user."""
    auth_service = AuthorizationService(db)
    success = await auth_service.remove_role_from_user(user_id, role_id)
    if not success:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="User or role not found")
    return {"message": "Role removed successfully"}
```

### Step 7: Update Main Application

#### gateway/src/main.py

Add roles router:

```python
# Add to imports
from src.api import roles

# Add router
app.include_router(roles.router, prefix="/api/admin", tags=["admin"])
```

### Step 8: Create Database Migration

Create migration for roles and permissions:

```bash
cd gateway
alembic revision --autogenerate -m "Create roles and permissions tables"
alembic upgrade head
```

### Step 9: Seed Initial Permissions

#### gateway/src/scripts/seed_permissions.py

Create script to seed initial permissions:

```python
"""Seed initial permissions and roles."""
import asyncio
from src.database import SessionLocal
from src.auth.authorization import AuthorizationService

async def seed_permissions():
    """Seed initial permissions."""
    db = SessionLocal()
    auth_service = AuthorizationService(db)

    # Create permissions
    permissions = [
        ("server:access", "server", "access", "Access MCP servers"),
        ("server:manage", "server", "manage", "Manage MCP servers"),
        ("server:create", "server", "create", "Create MCP servers"),
        ("server:delete", "server", "delete", "Delete MCP servers"),
        ("user:manage", "user", "manage", "Manage users"),
        ("role:manage", "role", "manage", "Manage roles"),
    ]

    for name, resource, action, description in permissions:
        try:
            await auth_service.create_permission(name, resource, action, description)
            print(f"Created permission: {name}")
        except ValueError as e:
            print(f"Permission {name} already exists: {e}")

    # Create default roles
    admin_role = await auth_service.create_role(
        name="admin",
        description="Administrator role with all permissions",
    )
    user_role = await auth_service.create_role(
        name="user",
        description="Standard user role",
    )

    print("Seeded permissions and roles successfully")
    db.close()

if __name__ == "__main__":
    asyncio.run(seed_permissions())
```

## Testing

### Test Authorization

Test authorization endpoints:

```bash
# Create permission (requires superuser)
curl -X POST http://localhost:8000/api/admin/permissions \
  -H "Authorization: Bearer $SUPERUSER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "server:access",
    "resource": "server",
    "action": "access",
    "description": "Access MCP servers"
  }'

# Create role
curl -X POST http://localhost:8000/api/admin/roles \
  -H "Authorization: Bearer $SUPERUSER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "server_admin",
    "description": "Server administrator",
    "permission_ids": ["permission-id"]
  }'

# Assign role to user
curl -X POST http://localhost:8000/api/admin/users/$USER_ID/roles/$ROLE_ID \
  -H "Authorization: Bearer $SUPERUSER_TOKEN"
```

## Verification

1. **Permissions**: Permissions can be created and managed
2. **Roles**: Roles can be created and assigned permissions
3. **User Roles**: Users can be assigned roles
4. **Access Control**: Access control works correctly
5. **Server Access**: Server access is controlled by permissions

## Troubleshooting

### Issue: Permission check fails

**Solution**: Verify user has required permissions:
```python
auth_service = AuthorizationService(db)
has_permission = await auth_service.has_permission(user, "server", "access")
```

### Issue: Role assignment fails

**Solution**: Check role and user exist:
```bash
docker-compose exec postgres psql -U postgres -d mcp_gateway -c "SELECT * FROM roles;"
docker-compose exec postgres psql -U postgres -d mcp_gateway -c "SELECT * FROM users;"
```

## Next Steps

After completing this instruction, proceed to:
- **03-tls-ssl.md**: Implement TLS/SSL configuration

