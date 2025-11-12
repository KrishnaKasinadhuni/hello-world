# 02: Dependencies

## Objective

Define and install all required dependencies for the MCP Gateway project, including Python packages, system dependencies, and development tools.

## Prerequisites

- Completed: 01-project-structure.md
- Python 3.11 or higher installed
- pip package manager
- Basic understanding of Python package management

## Implementation Steps

### Step 1: Update Gateway Requirements

#### gateway/requirements.txt

Create a comprehensive requirements file with all necessary dependencies:

```txt
# Web Framework
fastapi==0.104.1
uvicorn[standard]==0.24.0
pydantic==2.5.0
pydantic-settings==2.1.0
starlette==0.27.0

# Authentication & Security
python-jose[cryptography]==3.3.0
passlib[bcrypt]==1.7.4
cryptography==41.0.7
bcrypt==4.1.1

# Database
sqlalchemy==2.0.23
alembic==1.12.1
psycopg2-binary==2.9.9
asyncpg==0.29.0

# Cache & Rate Limiting
redis==5.0.1
hiredis==2.2.3

# HTTP Client
httpx==0.25.2
aiohttp==3.9.1

# WebSocket
websockets==12.0

# Environment & Configuration
python-dotenv==1.0.0
pydantic-settings==2.1.0

# Logging
structlog==23.2.0
python-json-logger==2.0.7

# Monitoring & Metrics
prometheus-client==0.19.0

# Validation
email-validator==2.1.0
python-multipart==0.0.6

# Utilities
pytz==2023.3
python-dateutil==2.8.2
```

#### gateway/requirements-dev.txt

Create development dependencies:

```txt
# Testing
pytest==7.4.3
pytest-asyncio==0.21.1
pytest-cov==4.1.0
pytest-mock==3.12.0
httpx==0.25.2
faker==20.1.0

# Code Quality
black==23.11.0
flake8==6.1.0
mypy==1.7.1
isort==5.12.0
pylint==3.0.2

# Type Stubs
types-redis==4.6.0.11
types-python-dateutil==2.8.19.14

# Documentation
mkdocs==1.5.3
mkdocs-material==9.4.14
```

### Step 2: Create Auth Service Requirements

#### auth-service/requirements.txt

If using a separate auth service:

```txt
# Web Framework
fastapi==0.104.1
uvicorn[standard]==0.24.0
pydantic==2.5.0

# Authentication
python-jose[cryptography]==3.3.0
passlib[bcrypt]==1.7.4
bcrypt==4.1.1

# Database
sqlalchemy==2.0.23
alembic==1.12.1
psycopg2-binary==2.9.9

# Redis
redis==5.0.1

# Environment
python-dotenv==1.0.0

# HTTP Client
httpx==0.25.2
```

### Step 3: Create System Dependencies File

#### gateway/system-requirements.txt

For system-level dependencies (used in Dockerfile):

```txt
# System packages needed for Python dependencies
build-essential
libpq-dev
python3-dev
curl
```

### Step 4: Create Dockerfile with Dependencies

#### gateway/Dockerfile

Create a multi-stage Dockerfile that handles dependencies:

```dockerfile
# Build stage
FROM python:3.11-slim as builder

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    libpq-dev \
    python3-dev \
    && rm -rf /var/lib/apt/lists/*

# Create virtual environment
RUN python -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

# Copy requirements
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir --upgrade pip && \
    pip install --no-cache-dir -r requirements.txt

# Production stage
FROM python:3.11-slim

WORKDIR /app

# Install runtime system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    libpq5 \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy virtual environment from builder
COPY --from=builder /opt/venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

# Copy application code
COPY src/ ./src/

# Set environment variables
ENV PYTHONPATH=/app
ENV PYTHONUNBUFFERED=1

# Expose port
EXPOSE 8000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8000/health || exit 1

# Run application
CMD ["uvicorn", "src.main:app", "--host", "0.0.0.0", "--port", "8000"]
```

### Step 5: Create Dependency Installation Script

#### scripts/install-dependencies.sh

Create a script to install dependencies:

```bash
#!/bin/bash

set -e

echo "Installing MCP Gateway dependencies..."

# Check Python version
python_version=$(python3 --version | cut -d' ' -f2 | cut -d'.' -f1,2)
required_version="3.11"

if [ "$(printf '%s\n' "$required_version" "$python_version" | sort -V | head -n1)" != "$required_version" ]; then
    echo "Error: Python 3.11 or higher is required. Found: $python_version"
    exit 1
fi

# Create virtual environment if it doesn't exist
if [ ! -d "venv" ]; then
    echo "Creating virtual environment..."
    python3 -m venv venv
fi

# Activate virtual environment
source venv/bin/activate

# Upgrade pip
echo "Upgrading pip..."
pip install --upgrade pip

# Install gateway dependencies
echo "Installing gateway dependencies..."
cd gateway
pip install -r requirements.txt

# Install development dependencies if in development mode
if [ "$1" == "dev" ]; then
    echo "Installing development dependencies..."
    pip install -r requirements-dev.txt
fi

cd ..

# Install auth service dependencies if it exists
if [ -d "auth-service" ]; then
    echo "Installing auth service dependencies..."
    cd auth-service
    pip install -r requirements.txt
    cd ..
fi

echo "Dependencies installed successfully!"
```

Make it executable:

```bash
chmod +x scripts/install-dependencies.sh
```

### Step 6: Create Dependency Verification Script

#### scripts/verify-dependencies.sh

Create a script to verify all dependencies are installed:

```bash
#!/bin/bash

set -e

echo "Verifying MCP Gateway dependencies..."

# Activate virtual environment if it exists
if [ -d "venv" ]; then
    source venv/bin/activate
fi

# Check Python version
python_version=$(python3 --version)
echo "Python: $python_version"

# Verify key packages
packages=(
    "fastapi"
    "uvicorn"
    "pydantic"
    "sqlalchemy"
    "redis"
    "python-jose"
    "passlib"
)

echo "Checking packages..."
for package in "${packages[@]}"; do
    if python -c "import ${package}" 2>/dev/null; then
        version=$(python -c "import ${package}; print(${package}.__version__)" 2>/dev/null || echo "installed")
        echo "✓ ${package} (${version})"
    else
        echo "✗ ${package} (not installed)"
        exit 1
    fi
done

echo "All dependencies verified successfully!"
```

Make it executable:

```bash
chmod +x scripts/verify-dependencies.sh
```

### Step 7: Create Requirements Lock File (Optional)

#### gateway/requirements.lock

Generate a lock file for reproducible builds:

```bash
cd gateway
pip freeze > requirements.lock
```

## Testing

### Test Dependency Installation

Install dependencies and verify:

```bash
# Install dependencies
./scripts/install-dependencies.sh

# Verify installation
./scripts/verify-dependencies.sh

# Test imports
python -c "from fastapi import FastAPI; print('FastAPI imported successfully')"
python -c "from sqlalchemy import create_engine; print('SQLAlchemy imported successfully')"
python -c "import redis; print('Redis imported successfully')"
```

### Test Docker Build

Build the Docker image to verify dependencies:

```bash
cd gateway
docker build -t mcp-gateway:test .
docker run --rm mcp-gateway:test python -c "from src import __version__; print(__version__)"
```

## Verification

1. **Requirements Files**: All requirements files should be created
2. **Dependencies Installed**: All packages should be installable
3. **Dockerfile**: Docker image should build successfully
4. **Scripts**: Installation and verification scripts should work
5. **Imports**: Key packages should be importable

## Troubleshooting

### Issue: pip install fails

**Solution**: Upgrade pip and ensure you have the latest version:
```bash
pip install --upgrade pip
pip install --upgrade setuptools wheel
```

### Issue: psycopg2-binary installation fails

**Solution**: Install system dependencies first:
```bash
sudo apt-get install libpq-dev python3-dev
```

### Issue: bcrypt compilation fails

**Solution**: Install build essentials:
```bash
sudo apt-get install build-essential
```

### Issue: Docker build fails

**Solution**: Check Dockerfile syntax and ensure all dependencies are listed:
```bash
docker build --no-cache -t mcp-gateway:test .
```

### Issue: Version conflicts

**Solution**: Use a virtual environment and pin specific versions:
```bash
python -m venv venv
source venv/bin/activate
pip install -r requirements.txt
```

## Next Steps

After completing this instruction, proceed to:
- **03-docker-base-config.md**: Set up base Docker Compose configuration

