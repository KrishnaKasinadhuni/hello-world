from fastapi import FastAPI, HTTPException, Depends, Query
from fastapi.middleware.cors import CORSMiddleware
from typing import List, Optional
from datetime import datetime

from .services.config_service import ConfigService
from .config.models import (
    ConfigValue,
    SecretValue,
    ConfigUpdate,
    SecretUpdate,
    ConfigList,
    SecretList
)

app = FastAPI(
    title="Configuration Manager",
    description="A service for managing configurations and secrets using AWS DynamoDB and Secrets Manager",
    version="1.0.0"
)

# Configure CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Initialize services
config_service = ConfigService()

@app.get("/health")
async def health_check():
    """Health check endpoint."""
    return {
        "status": "healthy",
        "timestamp": datetime.utcnow().isoformat()
    }

# Configuration endpoints
@app.get("/configs/{key}", response_model=ConfigValue)
async def get_config(
    key: str,
    environment: str = Query("default", description="Environment name")
):
    """Get configuration value by key."""
    try:
        return config_service.get_config(key, environment)
    except ValueError as e:
        raise HTTPException(status_code=404, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/configs", response_model=ConfigValue)
async def create_config(config: ConfigValue):
    """Create new configuration."""
    try:
        return config_service.create_config(config)
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.put("/configs/{key}", response_model=ConfigValue)
async def update_config(
    key: str,
    update: ConfigUpdate,
    environment: str = Query("default", description="Environment name")
):
    """Update configuration by key."""
    try:
        return config_service.update_config(key, update, environment)
    except ValueError as e:
        raise HTTPException(status_code=404, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.delete("/configs/{key}")
async def delete_config(
    key: str,
    environment: str = Query("default", description="Environment name")
):
    """Delete configuration by key."""
    try:
        config_service.delete_config(key, environment)
        return {"message": f"Configuration {key} deleted successfully"}
    except ValueError as e:
        raise HTTPException(status_code=404, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/configs", response_model=ConfigList)
async def list_configs(
    environment: str = Query("default", description="Environment name"),
    limit: int = Query(100, description="Maximum number of items to return")
):
    """List all configurations for an environment."""
    try:
        configs = config_service.list_configs(environment, limit)
        return ConfigList(items=configs, total=len(configs))
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

# Secret endpoints
@app.get("/secrets/{secret_name}", response_model=SecretValue)
async def get_secret(secret_name: str):
    """Get secret value by name."""
    try:
        return config_service.get_secret(secret_name)
    except ValueError as e:
        raise HTTPException(status_code=404, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/secrets/{secret_name}", response_model=SecretValue)
async def create_secret(secret_name: str, secret: SecretValue):
    """Create new secret."""
    try:
        return config_service.create_secret(secret_name, secret)
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.put("/secrets/{secret_name}", response_model=SecretValue)
async def update_secret(secret_name: str, update: SecretUpdate):
    """Update secret by name."""
    try:
        return config_service.update_secret(secret_name, update)
    except ValueError as e:
        raise HTTPException(status_code=404, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.delete("/secrets/{secret_name}")
async def delete_secret(secret_name: str):
    """Delete secret by name."""
    try:
        config_service.delete_secret(secret_name)
        return {"message": f"Secret {secret_name} deleted successfully"}
    except ValueError as e:
        raise HTTPException(status_code=404, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/secrets", response_model=SecretList)
async def list_secrets(
    limit: int = Query(100, description="Maximum number of items to return")
):
    """List all secrets."""
    try:
        secrets = config_service.list_secrets(limit)
        return SecretList(items=secrets, total=len(secrets))
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e)) 