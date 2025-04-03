import boto3
from typing import Optional, Dict
from datetime import datetime, timedelta
from botocore.config import Config
from botocore.exceptions import ClientError

class AWSClient:
    def __init__(
        self,
        region: str = "us-east-1",
        endpoint_url: Optional[str] = None,
        role_arn: Optional[str] = None,
        role_session_name: str = "config_manager_service",
        credential_cache_duration: int = 3600  # 1 hour in seconds
    ):
        """Initialize AWS clients with IAM role configuration."""
        self.config = Config(
            region_name=region,
            retries={'max_attempts': 3}
        )
        
        self.endpoint_url = endpoint_url
        self.role_arn = role_arn
        self.role_session_name = role_session_name
        self.credential_cache_duration = credential_cache_duration
        
        # Credential caching
        self._cached_credentials: Optional[Dict] = None
        self._credentials_expiry: Optional[datetime] = None
        
        # Initialize clients
        self.secrets_client = self._get_secrets_client()
        self.dynamodb_client = self._get_dynamodb_client()
        
    def _get_credentials(self) -> Optional[Dict]:
        """Get temporary credentials using IAM role with caching."""
        if not self.role_arn:
            return None
            
        # Check if we have valid cached credentials
        if self._cached_credentials and self._credentials_expiry:
            if datetime.utcnow() < self._credentials_expiry:
                return self._cached_credentials
                
        # Get new credentials
        sts_client = boto3.client('sts')
        try:
            response = sts_client.assume_role(
                RoleArn=self.role_arn,
                RoleSessionName=self.role_session_name
            )
            
            # Cache the credentials
            self._cached_credentials = {
                'aws_access_key_id': response['Credentials']['AccessKeyId'],
                'aws_secret_access_key': response['Credentials']['SecretAccessKey'],
                'aws_session_token': response['Credentials']['SessionToken']
            }
            
            # Set expiry time (using a buffer of 5 minutes before actual expiry)
            expiry = response['Credentials']['Expiration']
            self._credentials_expiry = expiry - timedelta(minutes=5)
            
            return self._cached_credentials
        except ClientError as e:
            raise ValueError(f"Failed to assume IAM role: {str(e)}")
        
    def _get_secrets_client(self):
        """Get AWS Secrets Manager client."""
        credentials = self._get_credentials()
        return boto3.client(
            'secretsmanager',
            config=self.config,
            endpoint_url=self.endpoint_url,
            **credentials if credentials else {}
        )
        
    def _get_dynamodb_client(self):
        """Get AWS DynamoDB client."""
        credentials = self._get_credentials()
        return boto3.client(
            'dynamodb',
            config=self.config,
            endpoint_url=self.endpoint_url,
            **credentials if credentials else {}
        )
        
    def get_secret(self, secret_name: str) -> dict:
        """Get secret value from AWS Secrets Manager."""
        try:
            response = self.secrets_client.get_secret_value(
                SecretId=secret_name
            )
            return response
        except ClientError as e:
            if e.response['Error']['Code'] == 'ResourceNotFoundException':
                raise ValueError(f"Secret {secret_name} not found")
            raise
            
    def create_secret(self, secret_name: str, secret_value: str, description: Optional[str] = None, tags: Optional[list] = None) -> dict:
        """Create a new secret in AWS Secrets Manager."""
        try:
            params = {
                'Name': secret_name,
                'SecretString': secret_value
            }
            
            if description:
                params['Description'] = description
                
            if tags:
                params['Tags'] = tags
                
            response = self.secrets_client.create_secret(**params)
            return response
        except ClientError as e:
            if e.response['Error']['Code'] == 'ResourceExistsException':
                raise ValueError(f"Secret {secret_name} already exists")
            raise
            
    def update_secret(self, secret_name: str, secret_value: str, description: Optional[str] = None) -> dict:
        """Update an existing secret in AWS Secrets Manager."""
        try:
            params = {
                'SecretId': secret_name,
                'SecretString': secret_value
            }
            
            if description:
                params['Description'] = description
                
            response = self.secrets_client.update_secret(**params)
            return response
        except ClientError as e:
            if e.response['Error']['Code'] == 'ResourceNotFoundException':
                raise ValueError(f"Secret {secret_name} not found")
            raise
            
    def delete_secret(self, secret_name: str) -> dict:
        """Delete a secret from AWS Secrets Manager."""
        try:
            response = self.secrets_client.delete_secret(
                SecretId=secret_name
            )
            return response
        except ClientError as e:
            if e.response['Error']['Code'] == 'ResourceNotFoundException':
                raise ValueError(f"Secret {secret_name} not found")
            raise
            
    def list_secrets(self, max_items: int = 100, next_token: Optional[str] = None) -> dict:
        """List secrets from AWS Secrets Manager."""
        try:
            params = {'MaxItems': max_items}
            if next_token:
                params['NextToken'] = next_token
                
            response = self.secrets_client.list_secrets(**params)
            return response
        except ClientError as e:
            raise
            
    def get_item(self, table_name: str, key: dict) -> dict:
        """Get item from DynamoDB table."""
        try:
            response = self.dynamodb_client.get_item(
                TableName=table_name,
                Key=key
            )
            return response
        except ClientError as e:
            if e.response['Error']['Code'] == 'ResourceNotFoundException':
                raise ValueError(f"Table {table_name} not found")
            raise
            
    def put_item(self, table_name: str, item: dict) -> dict:
        """Put item in DynamoDB table."""
        try:
            response = self.dynamodb_client.put_item(
                TableName=table_name,
                Item=item
            )
            return response
        except ClientError as e:
            if e.response['Error']['Code'] == 'ResourceNotFoundException':
                raise ValueError(f"Table {table_name} not found")
            raise
            
    def update_item(self, table_name: str, key: dict, update_expression: str, expression_values: dict) -> dict:
        """Update item in DynamoDB table."""
        try:
            response = self.dynamodb_client.update_item(
                TableName=table_name,
                Key=key,
                UpdateExpression=update_expression,
                ExpressionAttributeValues=expression_values
            )
            return response
        except ClientError as e:
            if e.response['Error']['Code'] == 'ResourceNotFoundException':
                raise ValueError(f"Table {table_name} not found")
            raise
            
    def delete_item(self, table_name: str, key: dict) -> dict:
        """Delete item from DynamoDB table."""
        try:
            response = self.dynamodb_client.delete_item(
                TableName=table_name,
                Key=key
            )
            return response
        except ClientError as e:
            if e.response['Error']['Code'] == 'ResourceNotFoundException':
                raise ValueError(f"Table {table_name} not found")
            raise
            
    def query(self, table_name: str, key_condition_expression: str, expression_values: dict, limit: int = 100) -> dict:
        """Query items from DynamoDB table."""
        try:
            response = self.dynamodb_client.query(
                TableName=table_name,
                KeyConditionExpression=key_condition_expression,
                ExpressionAttributeValues=expression_values,
                Limit=limit
            )
            return response
        except ClientError as e:
            if e.response['Error']['Code'] == 'ResourceNotFoundException':
                raise ValueError(f"Table {table_name} not found")
            raise 