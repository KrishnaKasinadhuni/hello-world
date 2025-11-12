# 01: Project Structure

## Objective

Initialize the project structure for the MCP Gateway. This instruction sets up the directory hierarchy, basic configuration files, and project organization.

## Prerequisites

- Basic understanding of project organization
- Knowledge of directory structures for web applications
- Understanding of containerized applications

## Implementation Steps

### Step 1: Create Root Directory Structure

Create the following directory structure for the MCP Gateway project:

```
mcp-gateway/
├── gateway/                    # Main gateway application
│   ├── src/
│   │   ├── api/               # API endpoints
│   │   ├── auth/              # Authentication logic
│   │   ├── registry/          # MCP server registry
│   │   ├── router/            # Request routing
│   │   ├── security/          # Security utilities
│   │   ├── models/            # Data models
│   │   ├── utils/             # Utility functions
│   │   └── main.py            # Application entry point
│   ├── tests/                 # Test files
│   ├── requirements.txt       # Python dependencies
│   └── Dockerfile             # Gateway Docker image
├── auth-service/              # Authentication service (optional, can be integrated)
│   ├── src/
│   │   ├── auth/
│   │   ├── models/
│   │   └── main.py
│   ├── tests/
│   ├── requirements.txt
│   └── Dockerfile
├── nginx/                     # Nginx configuration
│   ├── nginx.conf
│   ├── ssl/                   # SSL certificates (not in git)
│   └── Dockerfile
├── docker-compose.yml         # Docker Compose configuration
├── docker-compose.prod.yml    # Production Docker Compose
├── .env.example               # Example environment variables
├── .env                       # Environment variables (not in git)
├── .gitignore                 # Git ignore file
├── README.md                  # Project README
└── scripts/                   # Utility scripts
    ├── setup.sh               # Setup script
    ├── generate-certs.sh      # Certificate generation
    └── deploy.sh              # Deployment script
```

### Step 2: Create Gateway Directory Structure

Create the gateway application structure:

```bash
mkdir -p mcp-gateway/gateway/src/{api,auth,registry,router,security,models,utils}
mkdir -p mcp-gateway/gateway/tests
mkdir -p mcp-gateway/auth-service/src/{auth,models}
mkdir -p mcp-gateway/auth-service/tests
mkdir -p mcp-gateway/nginx/ssl
mkdir -p mcp-gateway/scripts
```

### Step 3: Create Basic Configuration Files

#### .gitignore

Create `.gitignore` file:

```gitignore
# Python
__pycache__/
*.py[cod]
*$py.class
*.so
.Python
venv/
env/
ENV/
.venv
pip-log.txt
pip-delete-this-directory.txt
.pytest_cache/
.coverage
htmlcov/
*.egg-info/
dist/
build/

# Environment variables
.env
.env.local
.env.*.local

# SSL Certificates
*.pem
*.key
*.crt
*.csr
nginx/ssl/

# IDE
.vscode/
.idea/
*.swp
*.swo
*~
.DS_Store

# Logs
*.log
logs/

# Docker
.docker/

# Database
*.db
*.sqlite
*.sqlite3

# Secrets
secrets/
*.secret
```

#### README.md

Create a basic README.md for the project:

```markdown
# MCP Gateway

A secure gateway for hosting Model Context Protocol (MCP) servers.

## Features

- Secure MCP server hosting
- Authentication and authorization
- Rate limiting
- Audit logging
- Docker Compose deployment

## Quick Start

1. Copy `.env.example` to `.env` and configure
2. Run `docker-compose up -d`
3. Access gateway at https://localhost

## Documentation

See the instruction sets in `mcp-gateway-instructions/` for detailed implementation guides.
```

### Step 4: Create Python Package Files

#### gateway/src/__init__.py

Create an empty `__init__.py` file:

```python
"""MCP Gateway Application."""
__version__ = "0.1.0"
```

#### gateway/src/api/__init__.py

```python
"""API endpoints."""
```

#### gateway/src/auth/__init__.py

```python
"""Authentication module."""
```

#### gateway/src/registry/__init__.py

```python
"""MCP server registry."""
```

#### gateway/src/router/__init__.py

```python
"""Request routing."""
```

#### gateway/src/security/__init__.py

```python
"""Security utilities."""
```

#### gateway/src/models/__init__.py

```python
"""Data models."""
```

#### gateway/src/utils/__init__.py

```python
"""Utility functions."""
```

### Step 5: Create Basic Python Files

#### gateway/src/main.py

Create the main application entry point:

```python
"""Main application entry point for MCP Gateway."""
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

app = FastAPI(
    title="MCP Gateway",
    description="Secure gateway for hosting MCP servers",
    version="0.1.0"
)

# CORS middleware (configure appropriately for production)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Configure for production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.get("/")
async def root():
    """Root endpoint."""
    return {"message": "MCP Gateway", "version": "0.1.0"}

@app.get("/health")
async def health():
    """Health check endpoint."""
    return {"status": "healthy"}

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
```

#### gateway/requirements.txt

Create initial requirements file:

```txt
fastapi==0.104.1
uvicorn[standard]==0.24.0
pydantic==2.5.0
pydantic-settings==2.1.0
python-jose[cryptography]==3.3.0
passlib[bcrypt]==1.7.4
python-multipart==0.0.6
sqlalchemy==2.0.23
alembic==1.12.1
psycopg2-binary==2.9.9
redis==5.0.1
httpx==0.25.2
websockets==12.0
python-dotenv==1.0.0
```

### Step 6: Create Environment Template

#### .env.example

Create environment variables template:

```env
# Application
GATEWAY_HOST=0.0.0.0
GATEWAY_PORT=8000
ENVIRONMENT=development
DEBUG=True

# Database
DATABASE_URL=postgresql://postgres:postgres@postgres:5432/mcp_gateway
DATABASE_POOL_SIZE=10
DATABASE_MAX_OVERFLOW=20

# Redis
REDIS_HOST=redis
REDIS_PORT=6379
REDIS_DB=0
REDIS_PASSWORD=

# JWT
JWT_SECRET_KEY=your-secret-key-change-in-production
JWT_ALGORITHM=HS256
JWT_ACCESS_TOKEN_EXPIRE_MINUTES=30
JWT_REFRESH_TOKEN_EXPIRE_DAYS=7

# TLS
TLS_ENABLED=True
TLS_CERT_PATH=/etc/nginx/ssl/cert.pem
TLS_KEY_PATH=/etc/nginx/ssl/key.pem

# Rate Limiting
RATE_LIMIT_ENABLED=True
RATE_LIMIT_PER_MINUTE=60
RATE_LIMIT_PER_HOUR=1000

# Logging
LOG_LEVEL=INFO
LOG_FILE_PATH=/var/log/mcp-gateway/gateway.log

# MCP Server Configuration
MCP_SERVER_NETWORK=mcp_servers
MCP_SERVER_MAX_INSTANCES=10
MCP_SERVER_TIMEOUT=30
```

## Testing

### Test Project Structure

Verify the directory structure was created correctly:

```bash
# Check directory structure
tree mcp-gateway -L 3

# Verify Python files are created
find mcp-gateway -name "*.py" -type f

# Check configuration files
ls -la mcp-gateway/.gitignore
ls -la mcp-gateway/.env.example
ls -la mcp-gateway/README.md
```

### Test Python Import

Test that Python can import the package:

```bash
cd mcp-gateway/gateway
python -c "from src import __version__; print(__version__)"
```

## Verification

1. **Directory Structure**: All directories should be created as specified
2. **Configuration Files**: `.gitignore`, `.env.example`, and `README.md` should exist
3. **Python Package**: `__init__.py` files should be in all package directories
4. **Main Application**: `gateway/src/main.py` should be executable
5. **Dependencies**: `requirements.txt` should list all necessary packages

## Troubleshooting

### Issue: Directory permissions

**Solution**: Ensure you have write permissions in the project directory:
```bash
chmod -R 755 mcp-gateway
```

### Issue: Python import errors

**Solution**: Ensure you're in the correct directory and Python path is set:
```bash
export PYTHONPATH="${PYTHONPATH}:$(pwd)/gateway"
```

### Issue: Missing __init__.py files

**Solution**: Ensure all package directories have `__init__.py` files:
```bash
find mcp-gateway -type d -name "__pycache__" -prune -o -type d -exec touch {}/__init__.py \;
```

## Next Steps

After completing this instruction, proceed to:
- **02-dependencies.md**: Define and install all project dependencies
- **03-docker-base-config.md**: Set up base Docker Compose configuration

