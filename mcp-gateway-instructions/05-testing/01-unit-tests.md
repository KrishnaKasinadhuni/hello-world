# 01: Unit Tests

## Objective

Set up unit testing for the MCP Gateway using pytest, including test structure, fixtures, mocking, and test coverage.

## Prerequisites

- Completed: 02-core-gateway/02-api-server.md
- pytest installed
- Understanding of unit testing principles
- Knowledge of pytest framework

## Implementation Steps

### Step 1: Create Test Structure

#### gateway/tests/__init__.py

Create test package:

```python
"""Test package."""
```

#### gateway/tests/conftest.py

Create pytest configuration:

```python
"""Pytest configuration and fixtures."""
import pytest
from fastapi.testclient import TestClient
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker
from sqlalchemy.pool import StaticPool

from src.main import app
from src.database import Base, get_db
from src.models.user import User
from src.models.server import MCPServer
from src.auth.password import get_password_hash

# Test database
SQLALCHEMY_TEST_DATABASE_URL = "sqlite:///./test.db"

engine = create_engine(
    SQLALCHEMY_TEST_DATABASE_URL,
    connect_args={"check_same_thread": False},
    poolclass=StaticPool,
)

TestingSessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)


@pytest.fixture
def db():
    """Create test database session."""
    Base.metadata.create_all(bind=engine)
    db = TestingSessionLocal()
    try:
        yield db
    finally:
        db.close()
        Base.metadata.drop_all(bind=engine)


@pytest.fixture
def client(db):
    """Create test client."""
    def override_get_db():
        try:
            yield db
        finally:
            db.close()
    
    app.dependency_overrides[get_db] = override_get_db
    with TestClient(app) as test_client:
        yield test_client
    app.dependency_overrides.clear()


@pytest.fixture
def test_user(db):
    """Create test user."""
    user = User(
        username="testuser",
        email="test@example.com",
        hashed_password=get_password_hash("testpassword"),
        is_active=True,
    )
    db.add(user)
    db.commit()
    db.refresh(user)
    return user


@pytest.fixture
def test_server(db):
    """Create test MCP server."""
    server = MCPServer(
        name="test-server",
        description="Test server",
        endpoint="http://localhost:9000",
        config={"image": "test:latest"},
        status="active",
    )
    db.add(server)
    db.commit()
    db.refresh(server)
    return server
```

### Step 2: Create Authentication Tests

#### gateway/tests/test_auth.py

Create authentication tests:

```python
"""Authentication tests."""
import pytest
from fastapi import status


def test_register_user(client):
    """Test user registration."""
    response = client.post(
        "/api/auth/register",
        json={
            "username": "newuser",
            "email": "newuser@example.com",
            "password": "password123",
            "roles": [],
        },
    )
    assert response.status_code == status.HTTP_201_CREATED
    assert response.json()["username"] == "newuser"


def test_login(client, test_user):
    """Test user login."""
    response = client.post(
        "/api/auth/login",
        data={
            "username": "testuser",
            "password": "testpassword",
        },
    )
    assert response.status_code == status.HTTP_200_OK
    assert "access_token" in response.json()
    assert "refresh_token" in response.json()


def test_login_invalid_credentials(client, test_user):
    """Test login with invalid credentials."""
    response = client.post(
        "/api/auth/login",
        data={
            "username": "testuser",
            "password": "wrongpassword",
        },
    )
    assert response.status_code == status.HTTP_401_UNAUTHORIZED


def test_get_current_user(client, test_user):
    """Test getting current user."""
    # Login first
    login_response = client.post(
        "/api/auth/login",
        data={
            "username": "testuser",
            "password": "testpassword",
        },
    )
    token = login_response.json()["access_token"]
    
    # Get current user
    response = client.get(
        "/api/auth/me",
        headers={"Authorization": f"Bearer {token}"},
    )
    assert response.status_code == status.HTTP_200_OK
    assert response.json()["username"] == "testuser"


def test_refresh_token(client, test_user):
    """Test token refresh."""
    # Login first
    login_response = client.post(
        "/api/auth/login",
        data={
            "username": "testuser",
            "password": "testpassword",
        },
    )
    refresh_token = login_response.json()["refresh_token"]
    
    # Refresh token
    response = client.post(
        "/api/auth/refresh",
        json={"refresh_token": refresh_token},
    )
    assert response.status_code == status.HTTP_200_OK
    assert "access_token" in response.json()
```

### Step 3: Create Server Registry Tests

#### gateway/tests/test_registry.py

Create server registry tests:

```python
"""Server registry tests."""
import pytest
from fastapi import status


def test_register_server(client, test_user):
    """Test server registration."""
    # Login first
    login_response = client.post(
        "/api/auth/login",
        data={
            "username": "testuser",
            "password": "testpassword",
        },
    )
    token = login_response.json()["access_token"]
    
    # Register server
    response = client.post(
        "/api/servers",
        headers={"Authorization": f"Bearer {token}"},
        json={
            "name": "test-server",
            "description": "Test server",
            "endpoint": "http://localhost:9000",
            "config": {"image": "test:latest"},
        },
    )
    assert response.status_code == status.HTTP_201_CREATED
    assert response.json()["name"] == "test-server"


def test_list_servers(client, test_user, test_server):
    """Test listing servers."""
    # Login first
    login_response = client.post(
        "/api/auth/login",
        data={
            "username": "testuser",
            "password": "testpassword",
        },
    )
    token = login_response.json()["access_token"]
    
    # List servers
    response = client.get(
        "/api/servers",
        headers={"Authorization": f"Bearer {token}"},
    )
    assert response.status_code == status.HTTP_200_OK
    assert len(response.json()) > 0


def test_get_server(client, test_user, test_server):
    """Test getting server details."""
    # Login first
    login_response = client.post(
        "/api/auth/login",
        data={
            "username": "testuser",
            "password": "testpassword",
        },
    )
    token = login_response.json()["access_token"]
    
    # Get server
    response = client.get(
        f"/api/servers/{test_server.id}",
        headers={"Authorization": f"Bearer {token}"},
    )
    assert response.status_code == status.HTTP_200_OK
    assert response.json()["id"] == str(test_server.id)
```

### Step 4: Create pytest Configuration

#### gateway/pytest.ini

Create pytest configuration:

```ini
[pytest]
testpaths = tests
python_files = test_*.py
python_classes = Test*
python_functions = test_*
addopts = 
    -v
    --strict-markers
    --tb=short
    --cov=src
    --cov-report=html
    --cov-report=term-missing
markers =
    unit: Unit tests
    integration: Integration tests
    security: Security tests
```

### Step 5: Create Test Requirements

#### gateway/requirements-test.txt

Create test requirements:

```txt
pytest==7.4.3
pytest-asyncio==0.21.1
pytest-cov==4.1.0
pytest-mock==3.12.0
httpx==0.25.2
faker==20.1.0
```

## Testing

### Run Unit Tests

Run unit tests:

```bash
# Run all tests
pytest

# Run specific test file
pytest tests/test_auth.py

# Run with coverage
pytest --cov=src --cov-report=html

# Run with markers
pytest -m unit
```

## Verification

1. **Test Structure**: Test structure is created
2. **Fixtures**: Fixtures work correctly
3. **Tests**: Tests pass
4. **Coverage**: Test coverage is adequate

## Troubleshooting

### Issue: Tests fail

**Solution**: Check test database and fixtures:
```bash
pytest -v
```

### Issue: Coverage too low

**Solution**: Add more tests to increase coverage:
```bash
pytest --cov=src --cov-report=term-missing
```

## Next Steps

After completing this instruction, proceed to:
- **02-integration-tests.md**: Set up integration tests

