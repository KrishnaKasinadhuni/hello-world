# 02: Integration Tests

## Objective

Set up integration testing for the MCP Gateway, including end-to-end tests, API integration tests, and database integration tests.

## Prerequisites

- Completed: 05-testing/01-unit-tests.md
- Docker and Docker Compose installed
- Understanding of integration testing
- Knowledge of test containers

## Implementation Steps

### Step 1: Create Integration Test Configuration

#### gateway/tests/integration/conftest.py

Create integration test configuration:

```python
"""Integration test configuration."""
import pytest
import docker
from fastapi.testclient import TestClient
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker

from src.main import app
from src.database import Base, get_db

# Docker client for test containers
docker_client = docker.from_env()


@pytest.fixture(scope="session")
def postgres_container():
    """Create PostgreSQL test container."""
    container = docker_client.containers.run(
        "postgres:15-alpine",
        environment={
            "POSTGRES_USER": "test",
            "POSTGRES_PASSWORD": "test",
            "POSTGRES_DB": "test_db",
        },
        ports={"5432/tcp": None},
        detach=True,
    )
    yield container
    container.stop()
    container.remove()


@pytest.fixture(scope="session")
def redis_container():
    """Create Redis test container."""
    container = docker_client.containers.run(
        "redis:7-alpine",
        ports={"6379/tcp": None},
        detach=True,
    )
    yield container
    container.stop()
    container.remove()


@pytest.fixture
def test_db(postgres_container):
    """Create test database session."""
    # Get container port
    port = postgres_container.attrs["NetworkSettings"]["Ports"]["5432/tcp"][0]["HostPort"]
    database_url = f"postgresql://test:test@localhost:{port}/test_db"
    
    engine = create_engine(database_url)
    Base.metadata.create_all(bind=engine)
    SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()
        Base.metadata.drop_all(bind=engine)


@pytest.fixture
def client(test_db):
    """Create test client."""
    def override_get_db():
        try:
            yield test_db
        finally:
            test_db.close()
    
    app.dependency_overrides[get_db] = override_get_db
    with TestClient(app) as test_client:
        yield test_client
    app.dependency_overrides.clear()
```

### Step 2: Create API Integration Tests

#### gateway/tests/integration/test_api.py

Create API integration tests:

```python
"""API integration tests."""
import pytest
from fastapi import status


def test_api_health_check(client):
    """Test API health check."""
    response = client.get("/health")
    assert response.status_code == status.HTTP_200_OK
    assert response.json()["status"] == "healthy"


def test_api_authentication_flow(client):
    """Test complete authentication flow."""
    # Register user
    register_response = client.post(
        "/api/auth/register",
        json={
            "username": "integration_test_user",
            "email": "integration_test@example.com",
            "password": "password123",
            "roles": [],
        },
    )
    assert register_response.status_code == status.HTTP_201_CREATED
    
    # Login
    login_response = client.post(
        "/api/auth/login",
        data={
            "username": "integration_test_user",
            "password": "password123",
        },
    )
    assert login_response.status_code == status.HTTP_200_OK
    token = login_response.json()["access_token"]
    
    # Get current user
    user_response = client.get(
        "/api/auth/me",
        headers={"Authorization": f"Bearer {token}"},
    )
    assert user_response.status_code == status.HTTP_200_OK
    assert user_response.json()["username"] == "integration_test_user"


def test_api_server_management_flow(client):
    """Test complete server management flow."""
    # Register and login
    client.post(
        "/api/auth/register",
        json={
            "username": "server_test_user",
            "email": "server_test@example.com",
            "password": "password123",
            "roles": [],
        },
    )
    login_response = client.post(
        "/api/auth/login",
        data={
            "username": "server_test_user",
            "password": "password123",
        },
    )
    token = login_response.json()["access_token"]
    
    # Register server
    register_response = client.post(
        "/api/servers",
        headers={"Authorization": f"Bearer {token}"},
        json={
            "name": "integration-test-server",
            "description": "Integration test server",
            "endpoint": "http://localhost:9000",
            "config": {"image": "test:latest"},
        },
    )
    assert register_response.status_code == status.HTTP_201_CREATED
    server_id = register_response.json()["id"]
    
    # List servers
    list_response = client.get(
        "/api/servers",
        headers={"Authorization": f"Bearer {token}"},
    )
    assert list_response.status_code == status.HTTP_200_OK
    assert len(list_response.json()) > 0
    
    # Get server
    get_response = client.get(
        f"/api/servers/{server_id}",
        headers={"Authorization": f"Bearer {token}"},
    )
    assert get_response.status_code == status.HTTP_200_OK
    assert get_response.json()["id"] == server_id
    
    # Delete server
    delete_response = client.delete(
        f"/api/servers/{server_id}",
        headers={"Authorization": f"Bearer {token}"},
    )
    assert delete_response.status_code == status.HTTP_204_NO_CONTENT
```

### Step 3: Create Database Integration Tests

#### gateway/tests/integration/test_database.py

Create database integration tests:

```python
"""Database integration tests."""
import pytest
from src.models.user import User
from src.models.server import MCPServer
from src.auth.password import get_password_hash


def test_user_creation(test_db):
    """Test user creation in database."""
    user = User(
        username="db_test_user",
        email="db_test@example.com",
        hashed_password=get_password_hash("password123"),
        is_active=True,
    )
    test_db.add(user)
    test_db.commit()
    test_db.refresh(user)
    
    assert user.id is not None
    assert user.username == "db_test_user"


def test_server_creation(test_db):
    """Test server creation in database."""
    server = MCPServer(
        name="db_test_server",
        description="Database test server",
        endpoint="http://localhost:9000",
        config={"image": "test:latest"},
        status="active",
    )
    test_db.add(server)
    test_db.commit()
    test_db.refresh(server)
    
    assert server.id is not None
    assert server.name == "db_test_server"
```

### Step 4: Create Docker Compose Test Configuration

#### docker-compose.test.yml

Create test Docker Compose configuration:

```yaml
version: '3.8'

services:
  postgres-test:
    image: postgres:15-alpine
    environment:
      POSTGRES_USER: test
      POSTGRES_PASSWORD: test
      POSTGRES_DB: test_db
    ports:
      - "5433:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U test"]
      interval: 5s
      timeout: 5s
      retries: 5

  redis-test:
    image: redis:7-alpine
    ports:
      - "6380:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 5s
      retries: 5
```

### Step 5: Create Integration Test Script

#### scripts/run-integration-tests.sh

Create integration test script:

```bash
#!/bin/bash

set -e

echo "Running integration tests..."

# Start test containers
docker-compose -f docker-compose.test.yml up -d

# Wait for containers to be ready
sleep 10

# Run integration tests
pytest tests/integration/ -v

# Stop test containers
docker-compose -f docker-compose.test.yml down

echo "Integration tests complete!"
```

Make it executable:

```bash
chmod +x scripts/run-integration-tests.sh
```

## Testing

### Run Integration Tests

Run integration tests:

```bash
# Run all integration tests
pytest tests/integration/

# Run with test containers
./scripts/run-integration-tests.sh

# Run specific test
pytest tests/integration/test_api.py::test_api_authentication_flow
```

## Verification

1. **Integration Tests**: Integration tests pass
2. **Test Containers**: Test containers work correctly
3. **API Tests**: API integration tests pass
4. **Database Tests**: Database integration tests pass

## Troubleshooting

### Issue: Test containers won't start

**Solution**: Check Docker and port availability:
```bash
docker ps
docker-compose -f docker-compose.test.yml up -d
```

### Issue: Integration tests fail

**Solution**: Check test database and containers:
```bash
docker-compose -f docker-compose.test.yml logs
pytest tests/integration/ -v
```

## Next Steps

After completing this instruction, proceed to:
- **03-security-tests.md**: Set up security tests

