# 07: Audit Logging

## Objective

Implement comprehensive audit logging for the MCP Gateway, including request logging, authentication logging, authorization logging, and security event logging with structured logging and log aggregation.

## Prerequisites

- Completed: 02-core-gateway/02-api-server.md
- Completed: 03-security/01-authentication.md
- Completed: 03-security/02-authorization.md
- Understanding of structured logging
- Knowledge of log aggregation patterns

## Implementation Steps

### Step 1: Create Audit Log Model

#### gateway/src/models/audit_log.py

Create audit log database model:

```python
"""Audit log database models."""
from sqlalchemy import Column, String, DateTime, Text, Integer, JSON
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.sql import func
import uuid

from src.database import Base


class AuditLog(Base):
    """Audit log model."""
    __tablename__ = "audit_logs"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    user_id = Column(UUID(as_uuid=True), nullable=True)
    username = Column(String(255), nullable=True)
    action = Column(String(255), nullable=False)
    resource = Column(String(255), nullable=False)
    method = Column(String(10), nullable=True)
    path = Column(String(500), nullable=True)
    status_code = Column(Integer, nullable=True)
    ip_address = Column(String(45), nullable=True)
    user_agent = Column(Text, nullable=True)
    request_body = Column(JSON, nullable=True)
    response_body = Column(JSON, nullable=True)
    error_message = Column(Text, nullable=True)
    timestamp = Column(DateTime, server_default=func.now(), nullable=False)
    duration_ms = Column(Integer, nullable=True)
    details = Column(JSON, nullable=True)

    def to_dict(self):
        """Convert model to dictionary."""
        return {
            "id": str(self.id),
            "user_id": str(self.user_id) if self.user_id else None,
            "username": self.username,
            "action": self.action,
            "resource": self.resource,
            "method": self.method,
            "path": self.path,
            "status_code": self.status_code,
            "ip_address": self.ip_address,
            "user_agent": self.user_agent,
            "request_body": self.request_body,
            "response_body": self.response_body,
            "error_message": self.error_message,
            "timestamp": self.timestamp.isoformat(),
            "duration_ms": self.duration_ms,
            "details": self.details,
        }
```

### Step 2: Create Audit Logger Service

#### gateway/src/security/audit_logger.py

Create audit logger service:

```python
"""Audit logger service."""
import logging
import time
from typing import Optional, Dict
from sqlalchemy.orm import Session
from fastapi import Request

from src.models.audit_log import AuditLog
from src.config import settings

logger = logging.getLogger(__name__)


class AuditLogger:
    """Service for audit logging."""

    def __init__(self, db: Session):
        """Initialize audit logger."""
        self.db = db

    async def log_request(
        self,
        request: Request,
        user_id: Optional[str] = None,
        username: Optional[str] = None,
        status_code: Optional[int] = None,
        response_body: Optional[Dict] = None,
        error_message: Optional[str] = None,
        duration_ms: Optional[int] = None,
        details: Optional[Dict] = None,
    ):
        """Log HTTP request."""
        try:
            # Get request body (if available)
            request_body = None
            if hasattr(request, "_body"):
                try:
                    import json
                    request_body = json.loads(request._body)
                except:
                    request_body = None

            # Create audit log entry
            audit_log = AuditLog(
                user_id=user_id,
                username=username,
                action="request",
                resource=request.url.path,
                method=request.method,
                path=str(request.url),
                status_code=status_code,
                ip_address=request.client.host if request.client else None,
                user_agent=request.headers.get("user-agent"),
                request_body=request_body,
                response_body=response_body,
                error_message=error_message,
                duration_ms=duration_ms,
                details=details,
            )
            self.db.add(audit_log)
            self.db.commit()
        except Exception as e:
            logger.error(f"Error logging request: {e}")
            self.db.rollback()

    async def log_authentication(
        self,
        username: str,
        ip_address: str,
        success: bool,
        error_message: Optional[str] = None,
    ):
        """Log authentication event."""
        try:
            audit_log = AuditLog(
                username=username,
                action="authentication",
                resource="auth",
                method="POST",
                path="/api/auth/login",
                status_code=200 if success else 401,
                ip_address=ip_address,
                error_message=error_message,
                details={"success": success},
            )
            self.db.add(audit_log)
            self.db.commit()
        except Exception as e:
            logger.error(f"Error logging authentication: {e}")
            self.db.rollback()

    async def log_authorization(
        self,
        user_id: str,
        username: str,
        resource: str,
        action: str,
        allowed: bool,
        ip_address: Optional[str] = None,
    ):
        """Log authorization event."""
        try:
            audit_log = AuditLog(
                user_id=user_id,
                username=username,
                action="authorization",
                resource=resource,
                method=action,
                path=resource,
                status_code=200 if allowed else 403,
                ip_address=ip_address,
                details={"allowed": allowed, "action": action},
            )
            self.db.add(audit_log)
            self.db.commit()
        except Exception as e:
            logger.error(f"Error logging authorization: {e}")
            self.db.rollback()

    async def log_security_event(
        self,
        event_type: str,
        description: str,
        user_id: Optional[str] = None,
        username: Optional[str] = None,
        ip_address: Optional[str] = None,
        details: Optional[Dict] = None,
    ):
        """Log security event."""
        try:
            audit_log = AuditLog(
                user_id=user_id,
                username=username,
                action="security_event",
                resource=event_type,
                method="SECURITY",
                path=event_type,
                ip_address=ip_address,
                error_message=description,
                details=details,
            )
            self.db.add(audit_log)
            self.db.commit()
        except Exception as e:
            logger.error(f"Error logging security event: {e}")
            self.db.rollback()

    async def log_server_operation(
        self,
        user_id: str,
        username: str,
        server_id: str,
        operation: str,
        success: bool,
        ip_address: Optional[str] = None,
        details: Optional[Dict] = None,
    ):
        """Log server operation."""
        try:
            audit_log = AuditLog(
                user_id=user_id,
                username=username,
                action="server_operation",
                resource=f"server:{server_id}",
                method=operation,
                path=f"/api/servers/{server_id}",
                status_code=200 if success else 500,
                ip_address=ip_address,
                details={"operation": operation, "success": success, **details or {}},
            )
            self.db.add(audit_log)
            self.db.commit()
        except Exception as e:
            logger.error(f"Error logging server operation: {e}")
            self.db.rollback()
```

### Step 3: Create Audit Log Middleware

#### gateway/src/middleware/audit_log.py

Create audit log middleware:

```python
"""Audit log middleware."""
import logging
import time
from fastapi import Request
from starlette.middleware.base import BaseHTTPMiddleware
from sqlalchemy.orm import Session

from src.security.audit_logger import AuditLogger
from src.database import SessionLocal

logger = logging.getLogger(__name__)


class AuditLogMiddleware(BaseHTTPMiddleware):
    """Middleware for audit logging."""

    async def dispatch(self, request: Request, call_next):
        """Process request with audit logging."""
        start_time = time.time()
        db = SessionLocal()
        audit_logger = AuditLogger(db)

        try:
            # Get user from request state (set by auth middleware)
            user_id = None
            username = None
            if hasattr(request.state, "user"):
                user_id = str(request.state.user.id)
                username = request.state.user.username

            # Process request
            response = await call_next(request)

            # Calculate duration
            duration_ms = int((time.time() - start_time) * 1000)

            # Log request
            await audit_logger.log_request(
                request=request,
                user_id=user_id,
                username=username,
                status_code=response.status_code,
                duration_ms=duration_ms,
            )

            return response
        except Exception as e:
            # Log error
            duration_ms = int((time.time() - start_time) * 1000)
            await audit_logger.log_request(
                request=request,
                user_id=user_id,
                username=username,
                status_code=500,
                error_message=str(e),
                duration_ms=duration_ms,
            )
            raise
        finally:
            db.close()
```

### Step 4: Update Authentication to Log Events

#### gateway/src/api/auth.py

Update authentication endpoints to log events:

```python
# Add to imports
from src.security.audit_logger import AuditLogger
from src.database import SessionLocal

# Update login endpoint
@router.post("/login", response_model=Token)
async def login(
    form_data: OAuth2PasswordRequestForm = Depends(),
    db: Session = Depends(get_db),
    request: Request = None,
):
    """Login endpoint."""
    db_audit = SessionLocal()
    audit_logger = AuditLogger(db_audit)
    
    try:
        auth_service = AuthService(db)
        user = await auth_service.authenticate_user(form_data.username, form_data.password)
        
        if not user:
            # Log failed authentication
            ip_address = request.client.host if request and request.client else None
            await audit_logger.log_authentication(
                username=form_data.username,
                ip_address=ip_address or "unknown",
                success=False,
                error_message="Invalid credentials",
            )
            raise HTTPException(...)
        
        # Log successful authentication
        ip_address = request.client.host if request and request.client else None
        await audit_logger.log_authentication(
            username=user.username,
            ip_address=ip_address or "unknown",
            success=True,
        )
        
        # ... rest of login logic ...
    finally:
        db_audit.close()
```

### Step 5: Create Audit Log API Endpoints

#### gateway/src/api/audit_logs.py

Create audit log query endpoints:

```python
"""Audit log query endpoints."""
from fastapi import APIRouter, Depends, HTTPException, Query
from typing import Optional, List
from datetime import datetime
from sqlalchemy.orm import Session
from sqlalchemy import and_, or_

from src.models.audit_log import AuditLog
from src.auth.dependencies import get_current_superuser
from src.database import get_db
from src.models.user import User

router = APIRouter()


@router.get("/audit-logs")
async def get_audit_logs(
    user_id: Optional[str] = Query(None),
    action: Optional[str] = Query(None),
    resource: Optional[str] = Query(None),
    start_date: Optional[datetime] = Query(None),
    end_date: Optional[datetime] = Query(None),
    limit: int = Query(100, le=1000),
    offset: int = Query(0),
    db: Session = Depends(get_db),
    current_user: User = Depends(get_current_superuser),
):
    """Get audit logs."""
    query = db.query(AuditLog)

    # Apply filters
    if user_id:
        query = query.filter(AuditLog.user_id == user_id)
    if action:
        query = query.filter(AuditLog.action == action)
    if resource:
        query = query.filter(AuditLog.resource == resource)
    if start_date:
        query = query.filter(AuditLog.timestamp >= start_date)
    if end_date:
        query = query.filter(AuditLog.timestamp <= end_date)

    # Order by timestamp
    query = query.order_by(AuditLog.timestamp.desc())

    # Apply pagination
    total = query.count()
    logs = query.offset(offset).limit(limit).all()

    return {
        "total": total,
        "limit": limit,
        "offset": offset,
        "logs": [log.to_dict() for log in logs],
    }
```

### Step 6: Create Structured Logging Configuration

#### gateway/src/logging_config.py

Update logging configuration for structured logging:

```python
"""Structured logging configuration."""
import logging
import sys
import json
from pathlib import Path
from src.config import settings

class JSONFormatter(logging.Formatter):
    """JSON formatter for structured logging."""
    
    def format(self, record):
        """Format log record as JSON."""
        log_data = {
            "timestamp": self.formatTime(record, self.datefmt),
            "level": record.levelname,
            "logger": record.name,
            "message": record.getMessage(),
            "module": record.module,
            "function": record.funcName,
            "line": record.lineno,
        }
        
        # Add exception info if present
        if record.exc_info:
            log_data["exception"] = self.formatException(record.exc_info)
        
        # Add extra fields
        if hasattr(record, "user_id"):
            log_data["user_id"] = record.user_id
        if hasattr(record, "ip_address"):
            log_data["ip_address"] = record.ip_address
        if hasattr(record, "request_id"):
            log_data["request_id"] = record.request_id
        
        return json.dumps(log_data)

def setup_logging():
    """Setup structured logging."""
    # Create log directory
    log_file_path = Path(settings.LOG_FILE_PATH)
    log_file_path.parent.mkdir(parents=True, exist_ok=True)
    
    # Configure logging
    logger = logging.getLogger()
    logger.setLevel(getattr(logging, settings.LOG_LEVEL.upper()))
    
    # File handler with JSON formatting
    file_handler = logging.FileHandler(settings.LOG_FILE_PATH)
    file_handler.setFormatter(JSONFormatter())
    logger.addHandler(file_handler)
    
    # Console handler with standard formatting
    console_handler = logging.StreamHandler(sys.stdout)
    console_handler.setFormatter(logging.Formatter(
        "%(asctime)s - %(name)s - %(levelname)s - %(message)s"
    ))
    logger.addHandler(console_handler)
    
    # Set log levels for specific libraries
    logging.getLogger("uvicorn").setLevel(logging.INFO)
    logging.getLogger("sqlalchemy").setLevel(logging.WARNING)
```

### Step 7: Create Database Migration

Create migration for audit logs table:

```bash
cd gateway
alembic revision --autogenerate -m "Create audit_logs table"
alembic upgrade head
```

## Testing

### Test Audit Logging

Test audit logging:

```bash
# Make a request
curl -X GET http://localhost:8000/api/servers \
  -H "Authorization: Bearer $TOKEN"

# Query audit logs
curl -X GET http://localhost:8000/api/audit-logs \
  -H "Authorization: Bearer $SUPERUSER_TOKEN"

# Query logs for specific user
curl -X GET "http://localhost:8000/api/audit-logs?user_id=$USER_ID" \
  -H "Authorization: Bearer $SUPERUSER_TOKEN"
```

## Verification

1. **Audit Logging**: All requests are logged
2. **Authentication Logging**: Authentication events are logged
3. **Authorization Logging**: Authorization events are logged
4. **Security Events**: Security events are logged
5. **Structured Logging**: Logs are in structured format
6. **Query API**: Audit logs can be queried

## Troubleshooting

### Issue: Audit logs not being created

**Solution**: Check database connection and table existence:
```bash
docker-compose exec postgres psql -U postgres -d mcp_gateway -c "SELECT COUNT(*) FROM audit_logs;"
```

### Issue: Logging performance issues

**Solution**: Use async logging or batch inserts:
```python
# Use async database operations
await db.execute(insert(AuditLog).values(...))
```

### Issue: Logs too verbose

**Solution**: Adjust log level and filter unnecessary logs:
```python
# In logging_config.py
logging.getLogger("uvicorn.access").setLevel(logging.WARNING)
```

## Next Steps

After completing this instruction, proceed to:
- **04-deployment/01-docker-compose-full.md**: Create complete Docker Compose configuration

