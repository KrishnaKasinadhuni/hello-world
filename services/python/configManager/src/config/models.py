from pydantic import BaseModel, Field, validator
from typing import Dict, Optional, List
from datetime import datetime
import json

class SecretValue(BaseModel):
    """Model for secret values stored in AWS Secrets Manager."""
    value: str
    description: Optional[str] = None
    tags: Optional[Dict[str, str]] = None
    last_modified: Optional[datetime] = None

class ConfigValue(BaseModel):
    """Model for configuration values stored in DynamoDB."""
    key: str
    value: str
    description: Optional[str] = None
    environment: str = Field(default="default")
    tags: Optional[Dict[str, str]] = None
    created_at: datetime = Field(default_factory=datetime.utcnow)
    updated_at: datetime = Field(default_factory=datetime.utcnow)
    
    @validator('value')
    def validate_value(cls, v):
        """Validate that the value is a valid JSON string."""
        try:
            json.loads(v)
        except json.JSONDecodeError:
            raise ValueError("Value must be a valid JSON string")
        return v

class ConfigUpdate(BaseModel):
    """Model for updating configuration values."""
    value: str
    description: Optional[str] = None
    tags: Optional[Dict[str, str]] = None
    
    @validator('value')
    def validate_value(cls, v):
        """Validate that the value is a valid JSON string."""
        try:
            json.loads(v)
        except json.JSONDecodeError:
            raise ValueError("Value must be a valid JSON string")
        return v

class SecretUpdate(BaseModel):
    """Model for updating secret values."""
    value: str
    description: Optional[str] = None
    tags: Optional[Dict[str, str]] = None

class ConfigResponse(BaseModel):
    """Model for configuration response."""
    key: str
    value: str
    description: Optional[str] = None
    environment: str
    tags: Optional[Dict[str, str]] = None
    created_at: datetime
    updated_at: datetime

class SecretResponse(BaseModel):
    """Model for secret response."""
    name: str
    value: str
    description: Optional[str] = None
    tags: Optional[Dict[str, str]] = None
    last_modified: Optional[datetime] = None

class ConfigList(BaseModel):
    """Model for listing configurations."""
    items: List[ConfigResponse]
    total: int
    next_token: Optional[str] = None

class SecretList(BaseModel):
    """Model for listing secrets."""
    items: List[SecretResponse]
    total: int
    next_token: Optional[str] = None 