# 01: API Documentation

## Objective

Create comprehensive API documentation for the MCP Gateway using FastAPI's automatic documentation and additional documentation tools.

## Prerequisites

- Completed: 02-core-gateway/02-api-server.md
- FastAPI application with endpoints
- Understanding of API documentation standards
- Knowledge of OpenAPI/Swagger

## Implementation Steps

### Step 1: Enhance FastAPI Documentation

#### gateway/src/main.py

Update FastAPI application with enhanced documentation:

```python
"""Main application with enhanced documentation."""
from fastapi import FastAPI
from fastapi.openapi.utils import get_openapi

app = FastAPI(
    title="MCP Gateway API",
    description="""
    Secure gateway for hosting Model Context Protocol (MCP) servers.
    
    ## Features
    
    * **Authentication**: JWT-based authentication with refresh tokens
    * **Authorization**: Role-based access control (RBAC)
    * **MCP Server Management**: Register, manage, and monitor MCP servers
    * **Request Routing**: Route requests to MCP servers
    * **Security**: Rate limiting, audit logging, and security monitoring
    
    ## Authentication
    
    Most endpoints require authentication. Use the `/api/auth/login` endpoint to obtain a JWT token.
    Include the token in the Authorization header: `Authorization: Bearer <token>`
    """,
    version="0.1.0",
    terms_of_service="https://example.com/terms/",
    contact={
        "name": "MCP Gateway Support",
        "email": "support@example.com",
    },
    license_info={
        "name": "Apache 2.0",
        "url": "https://www.apache.org/licenses/LICENSE-2.0.html",
    },
)


def custom_openapi():
    """Custom OpenAPI schema."""
    if app.openapi_schema:
        return app.openapi_schema
    openapi_schema = get_openapi(
        title="MCP Gateway API",
        version="0.1.0",
        description="Secure gateway for hosting MCP servers",
        routes=app.routes,
    )
    openapi_schema["info"]["x-logo"] = {
        "url": "https://fastapi.tiangolo.com/img/logo-margin/logo-teal.png"
    }
    app.openapi_schema = openapi_schema
    return app.openapi_schema


app.openapi = custom_openapi
```

### Step 2: Add API Documentation to Endpoints

#### gateway/src/api/auth.py

Add documentation to authentication endpoints:

```python
"""Authentication endpoints with documentation."""
from fastapi import APIRouter, Depends, HTTPException, status
from fastapi.security import OAuth2PasswordRequestForm
from pydantic import BaseModel, EmailStr

router = APIRouter()


class Token(BaseModel):
    """Token response model."""
    access_token: str
    token_type: str
    refresh_token: str

    class Config:
        schema_extra = {
            "example": {
                "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
                "token_type": "bearer",
                "refresh_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
            }
        }


@router.post(
    "/login",
    response_model=Token,
    summary="User Login",
    description="""
    Authenticate a user and receive JWT tokens.
    
    - **username**: User's username
    - **password**: User's password
    
    Returns access token and refresh token.
    """,
    responses={
        200: {
            "description": "Successful login",
            "content": {
                "application/json": {
                    "example": {
                        "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
                        "token_type": "bearer",
                        "refresh_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
                    }
                }
            }
        },
        401: {"description": "Invalid credentials"},
    },
    tags=["Authentication"],
)
async def login(form_data: OAuth2PasswordRequestForm = Depends()):
    """Login endpoint."""
    # ... implementation ...
```

### Step 3: Create API Documentation File

#### docs/api.md

Create API documentation file:

```markdown
# MCP Gateway API Documentation

## Overview

The MCP Gateway API provides endpoints for managing MCP servers, authentication, and request routing.

## Base URL

- Development: `http://localhost:8000`
- Production: `https://yourdomain.com`

## Authentication

Most endpoints require authentication using JWT tokens.

### Login

```bash
POST /api/auth/login
Content-Type: application/x-www-form-urlencoded

username=your_username&password=your_password
```

### Using Tokens

Include the token in the Authorization header:

```bash
Authorization: Bearer <access_token>
```

## Endpoints

### Authentication

#### POST /api/auth/login
Authenticate user and receive tokens.

#### POST /api/auth/refresh
Refresh access token using refresh token.

#### GET /api/auth/me
Get current user information.

### MCP Servers

#### POST /api/servers
Register a new MCP server.

#### GET /api/servers
List all MCP servers.

#### GET /api/servers/{server_id}
Get MCP server details.

#### PUT /api/servers/{server_id}
Update MCP server.

#### DELETE /api/servers/{server_id}
Delete MCP server.

#### POST /api/servers/{server_id}/start
Start MCP server.

#### POST /api/servers/{server_id}/stop
Stop MCP server.

### Proxy

#### POST /api/proxy/{server_id}/{path}
Proxy HTTP request to MCP server.

#### WebSocket /api/proxy/{server_id}/{path}
Proxy WebSocket connection to MCP server.

## Error Responses

### 400 Bad Request
Invalid request data.

### 401 Unauthorized
Authentication required or invalid token.

### 403 Forbidden
Insufficient permissions.

### 404 Not Found
Resource not found.

### 429 Too Many Requests
Rate limit exceeded.

### 500 Internal Server Error
Server error.

## Rate Limiting

API requests are rate limited:
- Per user: 60 requests per minute
- Per endpoint: Varies by endpoint
- Per IP: 60 requests per minute

Rate limit headers:
- `X-RateLimit-Limit`: Rate limit
- `X-RateLimit-Remaining`: Remaining requests
- `X-RateLimit-Reset`: Reset time
```

### Step 4: Create OpenAPI Schema Export

#### scripts/export-openapi.py

Create script to export OpenAPI schema:

```python
"""Export OpenAPI schema."""
import json
from src.main import app

# Generate OpenAPI schema
openapi_schema = app.openapi()

# Export to JSON
with open("docs/openapi.json", "w") as f:
    json.dump(openapi_schema, f, indent=2)

print("OpenAPI schema exported to docs/openapi.json")
```

### Step 5: Create API Documentation Site

#### docs/api/index.html

Create API documentation site:

```html
<!DOCTYPE html>
<html>
<head>
    <title>MCP Gateway API Documentation</title>
    <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@5.0.0/swagger-ui.css">
</head>
<body>
    <div id="swagger-ui"></div>
    <script src="https://unpkg.com/swagger-ui-dist@5.0.0/swagger-ui-bundle.js"></script>
    <script>
        SwaggerUIBundle({
            url: "/openapi.json",
            dom_id: "#swagger-ui",
        });
    </script>
</body>
</html>
```

## Testing

### Test API Documentation

Test API documentation:

```bash
# Start server
uvicorn src.main:app --reload

# Access Swagger UI
open http://localhost:8000/api/docs

# Access ReDoc
open http://localhost:8000/api/redoc

# Export OpenAPI schema
python scripts/export-openapi.py
```

## Verification

1. **API Documentation**: API documentation is accessible
2. **Swagger UI**: Swagger UI works correctly
3. **ReDoc**: ReDoc works correctly
4. **OpenAPI Schema**: OpenAPI schema is exported

## Troubleshooting

### Issue: API documentation not showing

**Solution**: Check FastAPI configuration and enable docs:
```python
app = FastAPI(docs_url="/api/docs", redoc_url="/api/redoc")
```

### Issue: OpenAPI schema export fails

**Solution**: Check application routes and OpenAPI generation:
```bash
python scripts/export-openapi.py
```

## Next Steps

After completing this instruction, proceed to:
- **02-operations-guide.md**: Create operations guide

