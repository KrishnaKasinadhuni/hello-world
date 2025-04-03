# Configuration Manager Service

A service for managing configurations and secrets using AWS DynamoDB and Secrets Manager. This service provides a secure and centralized way to store and retrieve configuration values and secrets across your applications.

## Features

- **Configuration Management**
  - Store and retrieve configuration values
  - Environment-based configuration
  - Version tracking with timestamps
  - Tag-based organization
  - JSON value support

- **Secret Management**
  - Secure storage of sensitive values
  - Automatic encryption
  - Access control through AWS IAM
  - Version history
  - Tag-based organization

- **API Features**
  - RESTful endpoints
  - OpenAPI documentation
  - CORS support
  - Health check endpoint
  - Error handling
  - Input validation

## Prerequisites

- Python 3.11 or higher
- AWS account with access to:
  - DynamoDB
  - Secrets Manager
  - IAM
- Docker and Docker Compose (for containerized deployment)

## AWS IAM Setup

### 1. Create IAM Role

First, create an IAM role that the service will assume:

```bash
# Create the role
aws iam create-role \
  --role-name config-manager-role \
  --assume-role-policy-document '{
    "Version": "2012-10-17",
    "Statement": [
      {
        "Effect": "Allow",
        "Principal": {
          "Service": "ecs-tasks.amazonaws.com"
        },
        "Action": "sts:AssumeRole"
      }
    ]
  }'
```

### 2. Create IAM Policy

Create a policy with the necessary permissions:

```bash
# Create the policy
aws iam create-policy \
  --policy-name config-manager-policy \
  --policy-document '{
    "Version": "2012-10-17",
    "Statement": [
      {
        "Effect": "Allow",
        "Action": [
          "dynamodb:GetItem",
          "dynamodb:PutItem",
          "dynamodb:UpdateItem",
          "dynamodb:DeleteItem",
          "dynamodb:Query"
        ],
        "Resource": "arn:aws:dynamodb:*:*:table/config_values"
      },
      {
        "Effect": "Allow",
        "Action": [
          "secretsmanager:GetSecretValue",
          "secretsmanager:CreateSecret",
          "secretsmanager:UpdateSecret",
          "secretsmanager:DeleteSecret",
          "secretsmanager:ListSecrets"
        ],
        "Resource": "*"
      }
    ]
  }'
```

### 3. Attach Policy to Role

Attach the policy to the role:

```bash
aws iam attach-role-policy \
  --role-name config-manager-role \
  --policy-arn arn:aws:iam::123456789012:policy/config-manager-policy
```

### 4. Create ECS Task Role

If running in ECS, create a task role:

```bash
aws iam create-role \
  --role-name ecs-task-role \
  --assume-role-policy-document '{
    "Version": "2012-10-17",
    "Statement": [
      {
        "Effect": "Allow",
        "Principal": {
          "Service": "ecs-tasks.amazonaws.com"
        },
        "Action": "sts:AssumeRole"
      }
    ]
  }'
```

### 5. Attach Policy to ECS Task Role

Attach the policy to the ECS task role:

```bash
aws iam attach-role-policy \
  --role-name ecs-task-role \
  --policy-arn arn:aws:iam::123456789012:policy/config-manager-policy
```

### 6. Configure ECS Task Definition

Update your ECS task definition to use the task role:

```json
{
  "taskRoleArn": "arn:aws:iam::123456789012:role/ecs-task-role",
  "executionRoleArn": "arn:aws:iam::123456789012:role/ecs-task-execution-role"
}
```

## Setup

### Local Development

1. Create a virtual environment:
   ```bash
   python -m venv venv
   source venv/bin/activate  # On Windows: venv\Scripts\activate
   ```

2. Install dependencies:
   ```bash
   pip install -r requirements.txt
   ```

3. Set up environment variables:
   ```bash
   cp .env.example .env
   # Edit .env with your AWS role ARN and settings
   ```

4. Run the service:
   ```bash
   uvicorn src.main:app --reload
   ```

### Docker Deployment

1. Build the Docker image:
   ```bash
   docker-compose build
   ```

2. Start the service:
   ```bash
   docker-compose up -d
   ```

## API Endpoints

### Health Check
- `GET /health` - Check service health

### Configurations
- `GET /configs/{key}` - Get configuration value
- `POST /configs` - Create new configuration
- `PUT /configs/{key}` - Update configuration
- `DELETE /configs/{key}` - Delete configuration
- `GET /configs` - List all configurations

### Secrets
- `GET /secrets/{secret_name}` - Get secret value
- `POST /secrets/{secret_name}` - Create new secret
- `PUT /secrets/{secret_name}` - Update secret
- `DELETE /secrets/{secret_name}` - Delete secret
- `GET /secrets` - List all secrets

## Usage Examples

### Create a Configuration
```bash
curl -X POST http://localhost:8000/configs \
  -H "Content-Type: application/json" \
  -d '{
    "key": "database_url",
    "value": "postgresql://user:pass@localhost:5432/db",
    "description": "Database connection URL",
    "environment": "development",
    "tags": ["database", "connection"]
  }'
```

### Get a Configuration
```bash
curl http://localhost:8000/configs/database_url?environment=development
```

### Create a Secret
```bash
curl -X POST http://localhost:8000/secrets/api_key \
  -H "Content-Type: application/json" \
  -d '{
    "value": "your-secret-key",
    "description": "API key for external service",
    "tags": ["api", "authentication"]
  }'
```

### Get a Secret
```bash
curl http://localhost:8000/secrets/api_key
```

## Configuration

The service can be configured through environment variables:

- `AWS_REGION` - AWS region (default: us-east-1)
- `AWS_ROLE_ARN` - IAM role ARN to assume
- `AWS_ROLE_SESSION_NAME` - Session name for assumed role (default: config_manager_service)
- `DYNAMODB_TABLE` - DynamoDB table name (default: config_values)
- `CREDENTIAL_CACHE_DURATION` - Duration to cache credentials in seconds (default: 3600)

## Security Considerations

1. **IAM Role Permissions**
   - Follow the principle of least privilege
   - Use resource-level permissions where possible
   - Regularly audit and rotate permissions

2. **Credential Management**
   - Credentials are automatically cached and refreshed
   - Cache duration is configurable
   - 5-minute buffer before credential expiry

3. **Network Security**
   - Use VPC endpoints for AWS services
   - Enable encryption in transit
   - Use security groups to restrict access

4. **Secret Management**
   - Secrets are encrypted at rest
   - Access is controlled through IAM
   - Version history is maintained

## Development

### Code Style
- Follow PEP 8 guidelines
- Use type hints
- Document functions and classes
- Write unit tests

### Testing
```bash
# Run tests
pytest

# Run with coverage
pytest --cov=src tests/
```

### Linting
```bash
# Run linters
flake8
black --check .
mypy src/
```

## Docker Commands

```bash
# Build image
docker-compose build

# Start service
docker-compose up -d

# Stop service
docker-compose down

# View logs
docker-compose logs -f

# Rebuild and restart
docker-compose up -d --build
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details. 