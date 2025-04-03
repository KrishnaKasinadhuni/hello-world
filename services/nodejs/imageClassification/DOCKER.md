# Docker Deployment Guide

This guide explains how to build and deploy the Image Classification Service using Docker.

## Prerequisites

- Docker installed on your system
- Docker Compose (optional, for easier deployment)
- Cohere API key

## Building the Docker Image

### 1. Build the Image

```bash
# From the service directory
docker build -t image-classification-service .
```

### 2. Run the Container

```bash
docker run -d \
  --name image-classification \
  -p 3000:3000 \
  -e COHERE_API_KEY=your_api_key_here \
  -e PORT=3000 \
  -v $(pwd)/uploads:/app/uploads \
  image-classification-service
```

## Using Docker Compose

### 1. Environment Setup

Create a `.env` file in the service directory:
```
COHERE_API_KEY=your_api_key_here
PORT=3000
```

### 2. Start the Service

```bash
docker-compose up -d
```

### 3. Stop the Service

```bash
docker-compose down
```

## Docker Configuration Details

### Dockerfile Structure

```dockerfile
# Build stage
FROM node:18.17.0-alpine AS builder

WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm ci

# Copy source code
COPY . .

# Production stage
FROM node:18.17.0-alpine

WORKDIR /app

# Copy package files and install production dependencies
COPY package*.json ./
RUN npm ci --only=production

# Copy built files from builder stage
COPY --from=builder /app/src ./src

# Create uploads directory
RUN mkdir -p uploads

# Set environment variables
ENV NODE_ENV=production

# Expose port
EXPOSE 3000

# Start the application
CMD ["npm", "start"]
```

### Docker Compose Configuration

```yaml
version: '3.8'

services:
  app:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "3000:3000"
    environment:
      - NODE_ENV=production
      - PORT=3000
    env_file:
      - .env
    volumes:
      - ./uploads:/app/uploads
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "wget", "--spider", "http://localhost:3000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
```

## Volume Management

### Persistent Storage

The service uses a volume mount for the uploads directory:
- Host path: `./uploads`
- Container path: `/app/uploads`

This ensures that uploaded images persist between container restarts.

### Backup and Restore

To backup uploaded images:
```bash
# Create a backup
tar -czf uploads_backup.tar.gz uploads/

# Restore from backup
tar -xzf uploads_backup.tar.gz
```

## Health Monitoring

The container includes a health check that:
- Runs every 30 seconds
- Checks the `/health` endpoint
- Has a 10-second timeout
- Retries 3 times before marking unhealthy

## Security Considerations

### Environment Variables

- Never commit `.env` files to version control
- Use Docker secrets for sensitive data in production
- Rotate API keys regularly

### File Permissions

- The uploads directory is created with appropriate permissions
- Files are stored with secure permissions
- Regular security audits recommended

## Performance Optimization

### Resource Limits

Add resource limits in docker-compose.yml:
```yaml
services:
  app:
    deploy:
      resources:
        limits:
          cpus: '1'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 512M
```

### Caching

- Build cache is utilized for faster builds
- Node modules are cached between builds
- Production dependencies only are installed

## Troubleshooting

### Common Issues

1. **Container won't start**
   - Check logs: `docker logs image-classification`
   - Verify environment variables
   - Check port conflicts

2. **Uploads not persisting**
   - Verify volume mount
   - Check directory permissions
   - Ensure host directory exists

3. **API errors**
   - Verify Cohere API key
   - Check network connectivity
   - Review API rate limits

### Logging

View container logs:
```bash
# Follow logs
docker logs -f image-classification

# Last 100 lines
docker logs --tail 100 image-classification
```

## Production Deployment

### Multi-stage Build

The Dockerfile uses multi-stage builds to:
- Minimize final image size
- Separate build dependencies
- Optimize for production

### Production Considerations

1. Use a production-grade Node.js server
2. Implement proper logging
3. Set up monitoring
4. Configure backup strategy
5. Use HTTPS in production

## Scaling

### Horizontal Scaling

To scale the service:
```bash
docker-compose up -d --scale app=3
```

### Load Balancing

Consider using:
- Nginx as reverse proxy
- Docker Swarm
- Kubernetes

## Maintenance

### Updates

1. Pull latest changes
2. Rebuild image
3. Restart containers
4. Verify health

### Cleanup

```bash
# Remove unused images
docker image prune

# Remove stopped containers
docker container prune

# Remove unused volumes
docker volume prune
``` 