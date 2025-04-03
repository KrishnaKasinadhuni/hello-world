from typing import Optional, List, Dict, Any
from datetime import datetime
import json
from .aws_clients import AWSClient
from ..config.models import ConfigValue, SecretValue, ConfigUpdate, SecretUpdate

class ConfigService:
    def __init__(
        self,
        table_name: str = "config_values",
        region: str = "us-east-1",
        endpoint_url: Optional[str] = None,
        role_arn: Optional[str] = None,
        role_session_name: str = "config_manager_service"
    ):
        """Initialize the configuration service."""
        self.table_name = table_name
        self.aws_client = AWSClient(
            region=region,
            endpoint_url=endpoint_url,
            role_arn=role_arn,
            role_session_name=role_session_name
        )
        
    def get_config(self, key: str, environment: str = "default") -> ConfigValue:
        """Get configuration value from DynamoDB."""
        try:
            response = self.aws_client.get_item(
                table_name=self.table_name,
                key={
                    "key": {"S": key},
                    "environment": {"S": environment}
                }
            )
            
            if "Item" not in response:
                raise ValueError(f"Configuration {key} not found for environment {environment}")
                
            item = response["Item"]
            return ConfigValue(
                key=item["key"]["S"],
                value=json.loads(item["value"]["S"]),
                description=item.get("description", {}).get("S"),
                environment=item["environment"]["S"],
                tags=[tag["S"] for tag in item.get("tags", {}).get("L", [])],
                created_at=datetime.fromisoformat(item["created_at"]["S"]),
                updated_at=datetime.fromisoformat(item["updated_at"]["S"])
            )
        except Exception as e:
            raise ValueError(f"Failed to get configuration: {str(e)}")
            
    def create_config(self, config: ConfigValue) -> ConfigValue:
        """Create new configuration in DynamoDB."""
        try:
            now = datetime.utcnow().isoformat()
            item = {
                "key": {"S": config.key},
                "value": {"S": json.dumps(config.value)},
                "environment": {"S": config.environment},
                "created_at": {"S": now},
                "updated_at": {"S": now}
            }
            
            if config.description:
                item["description"] = {"S": config.description}
                
            if config.tags:
                item["tags"] = {"L": [{"S": tag} for tag in config.tags]}
                
            self.aws_client.put_item(
                table_name=self.table_name,
                item=item
            )
            
            return config
        except Exception as e:
            raise ValueError(f"Failed to create configuration: {str(e)}")
            
    def update_config(self, key: str, update: ConfigUpdate, environment: str = "default") -> ConfigValue:
        """Update configuration in DynamoDB."""
        try:
            update_expr = "SET updated_at = :updated_at"
            expr_values = {
                ":updated_at": {"S": datetime.utcnow().isoformat()}
            }
            
            if update.value is not None:
                update_expr += ", #value = :value"
                expr_values[":value"] = {"S": json.dumps(update.value)}
                
            if update.description is not None:
                update_expr += ", description = :description"
                expr_values[":description"] = {"S": update.description}
                
            if update.tags is not None:
                update_expr += ", tags = :tags"
                expr_values[":tags"] = {"L": [{"S": tag} for tag in update.tags]}
                
            self.aws_client.update_item(
                table_name=self.table_name,
                key={
                    "key": {"S": key},
                    "environment": {"S": environment}
                },
                update_expression=update_expr,
                expression_values=expr_values
            )
            
            return self.get_config(key, environment)
        except Exception as e:
            raise ValueError(f"Failed to update configuration: {str(e)}")
            
    def delete_config(self, key: str, environment: str = "default") -> None:
        """Delete configuration from DynamoDB."""
        try:
            self.aws_client.delete_item(
                table_name=self.table_name,
                key={
                    "key": {"S": key},
                    "environment": {"S": environment}
                }
            )
        except Exception as e:
            raise ValueError(f"Failed to delete configuration: {str(e)}")
            
    def list_configs(self, environment: str = "default", limit: int = 100) -> List[ConfigValue]:
        """List configurations from DynamoDB."""
        try:
            response = self.aws_client.query(
                table_name=self.table_name,
                key_condition_expression="environment = :environment",
                expression_values={
                    ":environment": {"S": environment}
                },
                limit=limit
            )
            
            configs = []
            for item in response.get("Items", []):
                configs.append(ConfigValue(
                    key=item["key"]["S"],
                    value=json.loads(item["value"]["S"]),
                    description=item.get("description", {}).get("S"),
                    environment=item["environment"]["S"],
                    tags=[tag["S"] for tag in item.get("tags", {}).get("L", [])],
                    created_at=datetime.fromisoformat(item["created_at"]["S"]),
                    updated_at=datetime.fromisoformat(item["updated_at"]["S"])
                ))
                
            return configs
        except Exception as e:
            raise ValueError(f"Failed to list configurations: {str(e)}")
            
    def get_secret(self, secret_name: str) -> SecretValue:
        """Get secret value from AWS Secrets Manager."""
        try:
            response = self.aws_client.get_secret(secret_name)
            secret_value = json.loads(response["SecretString"])
            
            return SecretValue(
                value=secret_value,
                description=response.get("Description"),
                tags=[tag["Key"] for tag in response.get("Tags", [])],
                last_modified=response.get("LastModifiedDate")
            )
        except Exception as e:
            raise ValueError(f"Failed to get secret: {str(e)}")
            
    def create_secret(self, secret_name: str, secret: SecretValue) -> SecretValue:
        """Create new secret in AWS Secrets Manager."""
        try:
            response = self.aws_client.create_secret(
                secret_name=secret_name,
                secret_value=json.dumps(secret.value),
                description=secret.description,
                tags=[{"Key": tag} for tag in secret.tags] if secret.tags else None
            )
            
            return SecretValue(
                value=secret.value,
                description=secret.description,
                tags=secret.tags,
                last_modified=response.get("LastModifiedDate")
            )
        except Exception as e:
            raise ValueError(f"Failed to create secret: {str(e)}")
            
    def update_secret(self, secret_name: str, update: SecretUpdate) -> SecretValue:
        """Update secret in AWS Secrets Manager."""
        try:
            response = self.aws_client.update_secret(
                secret_name=secret_name,
                secret_value=json.dumps(update.value),
                description=update.description
            )
            
            return SecretValue(
                value=update.value,
                description=update.description,
                tags=update.tags,
                last_modified=response.get("LastModifiedDate")
            )
        except Exception as e:
            raise ValueError(f"Failed to update secret: {str(e)}")
            
    def delete_secret(self, secret_name: str) -> None:
        """Delete secret from AWS Secrets Manager."""
        try:
            self.aws_client.delete_secret(secret_name)
        except Exception as e:
            raise ValueError(f"Failed to delete secret: {str(e)}")
            
    def list_secrets(self, max_items: int = 100) -> List[str]:
        """List secrets from AWS Secrets Manager."""
        try:
            response = self.aws_client.list_secrets(max_items=max_items)
            return [secret["Name"] for secret in response.get("SecretList", [])]
        except Exception as e:
            raise ValueError(f"Failed to list secrets: {str(e)}") 