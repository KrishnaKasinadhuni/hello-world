# 01: Authentication

## Objective

Implement JWT-based authentication system for the MCP Gateway, including user login, token generation, token validation, and refresh token support.

## Prerequisites

- Completed: 02-core-gateway/02-api-server.md
- Understanding of JWT (JSON Web Tokens)
- Knowledge of OAuth2 and password hashing
- Understanding of authentication flows

## Implementation Steps

### Step 1: Create User Model

#### gateway/src/models/user.py

Create user database model:

```python
"""User database models."""
from sqlalchemy import Column, String, DateTime, Boolean, Text
from sqlalchemy.dialects.postgresql import UUID, ARRAY
from sqlalchemy.sql import func
import uuid

from src.database import Base


class User(Base):
    """User model."""
    __tablename__ = "users"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    username = Column(String(255), nullable=False, unique=True)
    email = Column(String(255), nullable=False, unique=True)
    hashed_password = Column(String(255), nullable=False)
    is_active = Column(Boolean, default=True, nullable=False)
    is_superuser = Column(Boolean, default=False, nullable=False)
    roles = Column(ARRAY(String), default=[], nullable=False)
    created_at = Column(DateTime, server_default=func.now(), nullable=False)
    updated_at = Column(DateTime, server_default=func.now(), onupdate=func.now(), nullable=False)
    last_login = Column(DateTime, nullable=True)

    def to_dict(self):
        """Convert model to dictionary."""
        return {
            "id": str(self.id),
            "username": self.username,
            "email": self.email,
            "is_active": self.is_active,
            "is_superuser": self.is_superuser,
            "roles": self.roles,
            "created_at": self.created_at.isoformat(),
            "updated_at": self.updated_at.isoformat(),
            "last_login": self.last_login.isoformat() if self.last_login else None,
        }
```

### Step 2: Create Password Hashing Utility

#### gateway/src/auth/password.py

Create password hashing utilities:

```python
"""Password hashing utilities."""
from passlib.context import CryptContext
from src.config import settings

pwd_context = CryptContext(schemes=["bcrypt"], deprecated="auto")


def verify_password(plain_password: str, hashed_password: str) -> bool:
    """Verify a password against a hash."""
    return pwd_context.verify(plain_password, hashed_password)


def get_password_hash(password: str) -> str:
    """Hash a password."""
    return pwd_context.hash(password)
```

### Step 3: Create JWT Utilities

#### gateway/src/auth/jwt.py

Create JWT token utilities:

```python
"""JWT token utilities."""
from datetime import datetime, timedelta
from typing import Optional, Dict
from jose import JWTError, jwt
from src.config import settings

ALGORITHM = settings.JWT_ALGORITHM
SECRET_KEY = settings.JWT_SECRET_KEY
ACCESS_TOKEN_EXPIRE_MINUTES = settings.JWT_ACCESS_TOKEN_EXPIRE_MINUTES
REFRESH_TOKEN_EXPIRE_DAYS = settings.JWT_REFRESH_TOKEN_EXPIRE_DAYS


def create_access_token(data: Dict, expires_delta: Optional[timedelta] = None) -> str:
    """Create access token."""
    to_encode = data.copy()
    if expires_delta:
        expire = datetime.utcnow() + expires_delta
    else:
        expire = datetime.utcnow() + timedelta(minutes=ACCESS_TOKEN_EXPIRE_MINUTES)
    to_encode.update({"exp": expire, "type": "access"})
    encoded_jwt = jwt.encode(to_encode, SECRET_KEY, algorithm=ALGORITHM)
    return encoded_jwt


def create_refresh_token(data: Dict) -> str:
    """Create refresh token."""
    to_encode = data.copy()
    expire = datetime.utcnow() + timedelta(days=REFRESH_TOKEN_EXPIRE_DAYS)
    to_encode.update({"exp": expire, "type": "refresh"})
    encoded_jwt = jwt.encode(to_encode, SECRET_KEY, algorithm=ALGORITHM)
    return encoded_jwt


def verify_token(token: str, token_type: str = "access") -> Optional[Dict]:
    """Verify token and return payload."""
    try:
        payload = jwt.decode(token, SECRET_KEY, algorithms=[ALGORITHM])
        if payload.get("type") != token_type:
            return None
        return payload
    except JWTError:
        return None


def get_user_from_token(token: str) -> Optional[str]:
    """Get user ID from token."""
    payload = verify_token(token)
    if payload:
        return payload.get("sub")
    return None
```

### Step 4: Create Authentication Service

#### gateway/src/auth/service.py

Create authentication service:

```python
"""Authentication service."""
import logging
from typing import Optional
from datetime import datetime
from sqlalchemy.orm import Session
from sqlalchemy import and_

from src.models.user import User
from src.auth.password import verify_password, get_password_hash
from src.auth.jwt import create_access_token, create_refresh_token, verify_token

logger = logging.getLogger(__name__)


class AuthService:
    """Service for authentication."""

    def __init__(self, db: Session):
        """Initialize authentication service."""
        self.db = db

    async def authenticate_user(self, username: str, password: str) -> Optional[User]:
        """Authenticate user with username and password."""
        user = self.db.query(User).filter(User.username == username).first()
        if not user:
            return None
        if not verify_password(password, user.hashed_password):
            return None
        if not user.is_active:
            return None
        return user

    async def create_user(
        self,
        username: str,
        email: str,
        password: str,
        is_superuser: bool = False,
        roles: list = None,
    ) -> User:
        """Create a new user."""
        if roles is None:
            roles = []
        
        # Check if user exists
        existing_user = self.db.query(User).filter(
            (User.username == username) | (User.email == email)
        ).first()
        if existing_user:
            raise ValueError("User with this username or email already exists")

        # Create user
        user = User(
            username=username,
            email=email,
            hashed_password=get_password_hash(password),
            is_superuser=is_superuser,
            roles=roles,
        )
        self.db.add(user)
        self.db.commit()
        self.db.refresh(user)

        logger.info(f"Created user: {username}")
        return user

    async def get_user(self, user_id: str) -> Optional[User]:
        """Get user by ID."""
        return self.db.query(User).filter(User.id == user_id).first()

    async def get_user_by_username(self, username: str) -> Optional[User]:
        """Get user by username."""
        return self.db.query(User).filter(User.username == username).first()

    async def update_last_login(self, user_id: str):
        """Update user's last login time."""
        user = await self.get_user(user_id)
        if user:
            user.last_login = datetime.utcnow()
            self.db.commit()

    async def change_password(self, user_id: str, old_password: str, new_password: str) -> bool:
        """Change user password."""
        user = await self.get_user(user_id)
        if not user:
            return False
        if not verify_password(old_password, user.hashed_password):
            return False
        user.hashed_password = get_password_hash(new_password)
        self.db.commit()
        return True
```

### Step 5: Create Authentication Dependencies

#### gateway/src/auth/dependencies.py

Create FastAPI dependencies for authentication:

```python
"""Authentication dependencies."""
from fastapi import Depends, HTTPException, status
from fastapi.security import OAuth2PasswordBearer
from sqlalchemy.orm import Session

from src.database import get_db
from src.auth.jwt import verify_token
from src.auth.service import AuthService
from src.models.user import User

oauth2_scheme = OAuth2PasswordBearer(tokenUrl="/api/auth/login")


async def get_current_user(
    token: str = Depends(oauth2_scheme),
    db: Session = Depends(get_db),
) -> User:
    """Get current authenticated user."""
    credentials_exception = HTTPException(
        status_code=status.HTTP_401_UNAUTHORIZED,
        detail="Could not validate credentials",
        headers={"WWW-Authenticate": "Bearer"},
    )
    
    payload = verify_token(token)
    if payload is None:
        raise credentials_exception
    
    user_id: str = payload.get("sub")
    if user_id is None:
        raise credentials_exception
    
    auth_service = AuthService(db)
    user = await auth_service.get_user(user_id)
    if user is None:
        raise credentials_exception
    
    if not user.is_active:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="User is inactive",
        )
    
    return user


async def get_current_active_user(
    current_user: User = Depends(get_current_user),
) -> User:
    """Get current active user."""
    if not current_user.is_active:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="User is inactive",
        )
    return current_user


async def get_current_superuser(
    current_user: User = Depends(get_current_user),
) -> User:
    """Get current superuser."""
    if not current_user.is_superuser:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Not enough permissions",
        )
    return current_user
```

### Step 6: Update Authentication API

#### gateway/src/api/auth.py

Update authentication endpoints:

```python
"""Authentication endpoints."""
from fastapi import APIRouter, Depends, HTTPException, status
from fastapi.security import OAuth2PasswordRequestForm
from pydantic import BaseModel, EmailStr
from sqlalchemy.orm import Session
from datetime import timedelta

from src.auth.service import AuthService
from src.auth.dependencies import get_current_user, get_current_active_user
from src.auth.jwt import create_access_token, create_refresh_token, verify_token
from src.database import get_db
from src.models.user import User
from src.config import settings

router = APIRouter()


class Token(BaseModel):
    """Token response model."""
    access_token: str
    token_type: str
    refresh_token: str


class TokenRefresh(BaseModel):
    """Token refresh request model."""
    refresh_token: str


class UserCreate(BaseModel):
    """User creation model."""
    username: str
    email: EmailStr
    password: str
    roles: list = []


class UserResponse(BaseModel):
    """User response model."""
    id: str
    username: str
    email: str
    is_active: bool
    is_superuser: bool
    roles: list
    created_at: str
    updated_at: str
    last_login: str = None

    class Config:
        from_attributes = True


@router.post("/login", response_model=Token)
async def login(
    form_data: OAuth2PasswordRequestForm = Depends(),
    db: Session = Depends(get_db),
):
    """Login endpoint."""
    auth_service = AuthService(db)
    user = await auth_service.authenticate_user(form_data.username, form_data.password)
    if not user:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Incorrect username or password",
            headers={"WWW-Authenticate": "Bearer"},
        )
    
    # Update last login
    await auth_service.update_last_login(str(user.id))
    
    # Create tokens
    access_token = create_access_token(data={"sub": str(user.id), "username": user.username})
    refresh_token = create_refresh_token(data={"sub": str(user.id), "username": user.username})
    
    return Token(
        access_token=access_token,
        token_type="bearer",
        refresh_token=refresh_token,
    )


@router.post("/refresh", response_model=Token)
async def refresh_token(
    token_refresh: TokenRefresh,
    db: Session = Depends(get_db),
):
    """Refresh token endpoint."""
    payload = verify_token(token_refresh.refresh_token, token_type="refresh")
    if payload is None:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid refresh token",
        )
    
    user_id = payload.get("sub")
    auth_service = AuthService(db)
    user = await auth_service.get_user(user_id)
    if not user or not user.is_active:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="User not found or inactive",
        )
    
    # Create new tokens
    access_token = create_access_token(data={"sub": str(user.id), "username": user.username})
    refresh_token = create_refresh_token(data={"sub": str(user.id), "username": user.username})
    
    return Token(
        access_token=access_token,
        token_type="bearer",
        refresh_token=refresh_token,
    )


@router.post("/logout")
async def logout(
    current_user: User = Depends(get_current_active_user),
):
    """Logout endpoint."""
    # In a stateless JWT system, logout is handled client-side
    # You can implement token blacklisting here if needed
    return {"message": "Logged out successfully"}


@router.get("/me", response_model=UserResponse)
async def get_current_user_info(
    current_user: User = Depends(get_current_active_user),
):
    """Get current user information."""
    return UserResponse(**current_user.to_dict())


@router.post("/register", response_model=UserResponse, status_code=status.HTTP_201_CREATED)
async def register(
    user_data: UserCreate,
    db: Session = Depends(get_db),
):
    """Register a new user."""
    auth_service = AuthService(db)
    try:
        user = await auth_service.create_user(
            username=user_data.username,
            email=user_data.email,
            password=user_data.password,
            roles=user_data.roles,
        )
        return UserResponse(**user.to_dict())
    except ValueError as e:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=str(e),
        )
```

### Step 7: Create Database Migration

Create migration for users table:

```bash
cd gateway
alembic revision --autogenerate -m "Create users table"
alembic upgrade head
```

## Testing

### Test Authentication

Test authentication endpoints:

```bash
# Register a user
curl -X POST http://localhost:8000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "email": "test@example.com",
    "password": "testpassword123",
    "roles": []
  }'

# Login
curl -X POST http://localhost:8000/api/auth/login \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "username=testuser&password=testpassword123"

# Get current user (use token from login)
TOKEN="your-access-token-here"
curl -X GET http://localhost:8000/api/auth/me \
  -H "Authorization: Bearer $TOKEN"

# Refresh token
curl -X POST http://localhost:8000/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refresh_token": "your-refresh-token-here"
  }'
```

## Verification

1. **User Registration**: Users can be registered
2. **Login**: Users can login and receive tokens
3. **Token Validation**: Tokens are validated correctly
4. **Token Refresh**: Refresh tokens work
5. **User Info**: Current user info can be retrieved
6. **Password Hashing**: Passwords are hashed securely

## Troubleshooting

### Issue: Token validation fails

**Solution**: Check JWT secret key and algorithm:
```python
# Verify settings
from src.config import settings
print(settings.JWT_SECRET_KEY)
print(settings.JWT_ALGORITHM)
```

### Issue: Password hashing fails

**Solution**: Ensure bcrypt is installed:
```bash
pip install passlib[bcrypt]
```

### Issue: User not found

**Solution**: Verify user exists in database:
```bash
docker-compose exec postgres psql -U postgres -d mcp_gateway -c "SELECT * FROM users;"
```

## Next Steps

After completing this instruction, proceed to:
- **02-authorization.md**: Implement authorization (RBAC)

