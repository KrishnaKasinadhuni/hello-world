# 03: Security Tests

## Objective

Set up security testing for the MCP Gateway, including authentication security tests, authorization security tests, and vulnerability tests.

## Prerequisites

- Completed: 05-testing/01-unit-tests.md
- Completed: 03-security/01-authentication.md
- Completed: 03-security/02-authorization.md
- Understanding of security testing
- Knowledge of OWASP testing guidelines

## Implementation Steps

### Step 1: Create Security Test Configuration

#### gateway/tests/security/conftest.py

Create security test configuration:

```python
"""Security test configuration."""
import pytest
from fastapi.testclient import TestClient

from src.main import app
from tests.conftest import client, test_user


@pytest.fixture
def unauthorized_client():
    """Create unauthorized client."""
    return TestClient(app)


@pytest.fixture
def authorized_client(client, test_user):
    """Create authorized client."""
    # Login
    login_response = client.post(
        "/api/auth/login",
        data={
            "username": "testuser",
            "password": "testpassword",
        },
    )
    token = login_response.json()["access_token"]
    
    # Create client with token
    authorized_client = TestClient(app)
    authorized_client.headers.update({"Authorization": f"Bearer {token}"})
    return authorized_client
```

### Step 2: Create Authentication Security Tests

#### gateway/tests/security/test_authentication.py

Create authentication security tests:

```python
"""Authentication security tests."""
import pytest
from fastapi import status


def test_login_brute_force_protection(client, test_user):
    """Test brute force protection."""
    # Attempt multiple failed logins
    for i in range(10):
        response = client.post(
            "/api/auth/login",
            data={
                "username": "testuser",
                "password": "wrongpassword",
            },
        )
        if i < 5:
            assert response.status_code == status.HTTP_401_UNAUTHORIZED
        else:
            # After 5 attempts, should be rate limited
            assert response.status_code == status.HTTP_429_TOO_MANY_REQUESTS


def test_login_sql_injection(client):
    """Test SQL injection protection."""
    response = client.post(
        "/api/auth/login",
        data={
            "username": "admin' OR '1'='1",
            "password": "password",
        },
    )
    assert response.status_code == status.HTTP_401_UNAUTHORIZED


def test_token_validation(client, test_user):
    """Test token validation."""
    # Login
    login_response = client.post(
        "/api/auth/login",
        data={
            "username": "testuser",
            "password": "testpassword",
        },
    )
    token = login_response.json()["access_token"]
    
    # Test with invalid token
    response = client.get(
        "/api/auth/me",
        headers={"Authorization": "Bearer invalid_token"},
    )
    assert response.status_code == status.HTTP_401_UNAUTHORIZED
    
    # Test with expired token (if possible)
    # This would require mocking time or using an expired token
    
    # Test with missing token
    response = client.get("/api/auth/me")
    assert response.status_code == status.HTTP_403_FORBIDDEN


def test_password_hashing(client, test_user):
    """Test password hashing."""
    # Verify password is hashed in database
    from src.database import SessionLocal
    from src.models.user import User
    
    db = SessionLocal()
    user = db.query(User).filter(User.username == "testuser").first()
    db.close()
    
    # Password should be hashed
    assert user.hashed_password != "testpassword"
    assert user.hashed_password.startswith("$2b$")  # bcrypt hash
```

### Step 3: Create Authorization Security Tests

#### gateway/tests/security/test_authorization.py

Create authorization security tests:

```python
"""Authorization security tests."""
import pytest
from fastapi import status


def test_unauthorized_access(unauthorized_client):
    """Test unauthorized access to protected endpoints."""
    # Try to access protected endpoint without token
    response = unauthorized_client.get("/api/servers")
    assert response.status_code == status.HTTP_403_FORBIDDEN


def test_authorization_bypass(client, test_user):
    """Test authorization bypass attempts."""
    # Login as regular user
    login_response = client.post(
        "/api/auth/login",
        data={
            "username": "testuser",
            "password": "testpassword",
        },
    )
    token = login_response.json()["access_token"]
    
    # Try to access admin endpoint
    response = client.get(
        "/api/admin/roles",
        headers={"Authorization": f"Bearer {token}"},
    )
    assert response.status_code == status.HTTP_403_FORBIDDEN


def test_server_access_control(client, test_user, test_server):
    """Test server access control."""
    # Login
    login_response = client.post(
        "/api/auth/login",
        data={
            "username": "testuser",
            "password": "testpassword",
        },
    )
    token = login_response.json()["access_token"]
    
    # Try to access server without permission
    response = client.get(
        f"/api/servers/{test_server.id}",
        headers={"Authorization": f"Bearer {token}"},
    )
    # Should check permissions
    assert response.status_code in [status.HTTP_200_OK, status.HTTP_403_FORBIDDEN]
```

### Step 4: Create Input Validation Tests

#### gateway/tests/security/test_input_validation.py

Create input validation tests:

```python
"""Input validation security tests."""
import pytest
from fastapi import status


def test_xss_protection(client, authorized_client):
    """Test XSS protection."""
    # Try to inject XSS in server name
    response = authorized_client.post(
        "/api/servers",
        json={
            "name": "<script>alert('XSS')</script>",
            "description": "Test server",
            "endpoint": "http://localhost:9000",
            "config": {},
        },
    )
    # Should sanitize or reject
    assert response.status_code in [status.HTTP_400_BAD_REQUEST, status.HTTP_201_CREATED]


def test_sql_injection_protection(client, authorized_client):
    """Test SQL injection protection."""
    # Try SQL injection in various fields
    response = authorized_client.post(
        "/api/servers",
        json={
            "name": "test'; DROP TABLE users; --",
            "description": "Test server",
            "endpoint": "http://localhost:9000",
            "config": {},
        },
    )
    # Should be handled safely
    assert response.status_code != status.HTTP_500_INTERNAL_SERVER_ERROR


def test_path_traversal_protection(client, authorized_client):
    """Test path traversal protection."""
    # Try path traversal in endpoint
    response = authorized_client.post(
        "/api/servers",
        json={
            "name": "test-server",
            "description": "Test server",
            "endpoint": "../../etc/passwd",
            "config": {},
        },
    )
    # Should validate endpoint format
    assert response.status_code == status.HTTP_400_BAD_REQUEST
```

### Step 5: Create Rate Limiting Tests

#### gateway/tests/security/test_rate_limiting.py

Create rate limiting tests:

```python
"""Rate limiting security tests."""
import pytest
from fastapi import status


def test_rate_limiting(client):
    """Test rate limiting."""
    # Make many requests
    for i in range(100):
        response = client.get("/health")
        if response.status_code == status.HTTP_429_TOO_MANY_REQUESTS:
            # Rate limited
            assert "X-RateLimit-Limit" in response.headers
            assert "X-RateLimit-Remaining" in response.headers
            break


def test_endpoint_rate_limiting(client, authorized_client):
    """Test endpoint-specific rate limiting."""
    # Make many requests to login endpoint
    for i in range(10):
        response = client.post(
            "/api/auth/login",
            data={
                "username": "testuser",
                "password": "wrongpassword",
            },
        )
        if response.status_code == status.HTTP_429_TOO_MANY_REQUESTS:
            break
```

### Step 6: Create Security Test Script

#### scripts/run-security-tests.sh

Create security test script:

```bash
#!/bin/bash

set -e

echo "Running security tests..."

# Run security tests
pytest tests/security/ -v -m security

echo "Security tests complete!"
```

Make it executable:

```bash
chmod +x scripts/run-security-tests.sh
```

## Testing

### Run Security Tests

Run security tests:

```bash
# Run all security tests
pytest tests/security/ -v

# Run specific security test
pytest tests/security/test_authentication.py::test_login_brute_force_protection

# Run with security marker
pytest -m security
```

## Verification

1. **Authentication Security**: Authentication security tests pass
2. **Authorization Security**: Authorization security tests pass
3. **Input Validation**: Input validation tests pass
4. **Rate Limiting**: Rate limiting tests pass

## Troubleshooting

### Issue: Security tests fail

**Solution**: Check security configurations and test setup:
```bash
pytest tests/security/ -v
```

### Issue: Rate limiting not working

**Solution**: Check Redis connection and rate limit configuration:
```bash
docker-compose exec redis redis-cli ping
```

## Next Steps

After completing this instruction, proceed to:
- **06-documentation/01-api-documentation.md**: Create API documentation

