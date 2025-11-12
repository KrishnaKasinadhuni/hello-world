# 04: Production Deployment

## Objective

Create a comprehensive production deployment guide for the MCP Gateway, including deployment checklist, security hardening, monitoring, and maintenance procedures.

## Prerequisites

- Completed: All previous instructions
- Production server with Docker and Docker Compose
- Domain name and DNS configuration
- SSL certificates (Let's Encrypt recommended)

## Implementation Steps

### Step 1: Create Production Deployment Checklist

#### docs/production-checklist.md

Create production deployment checklist:

```markdown
# Production Deployment Checklist

## Pre-Deployment

- [ ] Review and update all environment variables
- [ ] Generate secure secrets (JWT, database, Redis)
- [ ] Configure SSL certificates (Let's Encrypt)
- [ ] Set up DNS records
- [ ] Configure firewall rules
- [ ] Set up backup strategy
- [ ] Configure monitoring and alerting
- [ ] Review security configurations
- [ ] Test in staging environment

## Deployment

- [ ] Deploy infrastructure (Docker, Docker Compose)
- [ ] Configure environment variables
- [ ] Start services
- [ ] Run database migrations
- [ ] Seed initial data
- [ ] Verify health checks
- [ ] Test API endpoints
- [ ] Configure monitoring
- [ ] Set up backups

## Post-Deployment

- [ ] Verify all services are running
- [ ] Test authentication and authorization
- [ ] Test MCP server registration
- [ ] Test request routing
- [ ] Monitor logs for errors
- [ ] Verify SSL certificates
- [ ] Test rate limiting
- [ ] Verify audit logging
- [ ] Document deployment

## Security

- [ ] Change all default passwords
- [ ] Enable TLS/SSL
- [ ] Configure firewall
- [ ] Enable rate limiting
- [ ] Configure CORS properly
- [ ] Enable audit logging
- [ ] Review security headers
- [ ] Set up intrusion detection
- [ ] Configure backup encryption

## Monitoring

- [ ] Set up health checks
- [ ] Configure log aggregation
- [ ] Set up metrics collection
- [ ] Configure alerting
- [ ] Set up dashboards
- [ ] Monitor resource usage
- [ ] Monitor security events
- [ ] Set up incident response
```

### Step 2: Create Production Deployment Script

#### scripts/deploy-production.sh

Create production deployment script:

```bash
#!/bin/bash

set -e

echo "Deploying MCP Gateway to production..."

# Check if .env.production exists
if [ ! -f .env.production ]; then
    echo "Error: .env.production not found. Please create it from .env.production.example"
    exit 1
fi

# Load production environment
export $(cat .env.production | xargs)

# Generate SSL certificates if using Let's Encrypt
if [ "$TLS_ENABLED" == "True" ] && [ ! -f nginx/ssl/cert.pem ]; then
    echo "Setting up Let's Encrypt certificates..."
    ./scripts/setup-letsencrypt.sh "$TLS_DOMAIN"
fi

# Create networks
docker network create mcp_gateway_network 2>/dev/null || true
docker network create gateway_to_servers 2>/dev/null || true
docker network create mcp_servers_network 2>/dev/null || true

# Start services with production configuration
docker-compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env.production up -d

# Wait for services to be healthy
echo "Waiting for services to start..."
sleep 30

# Run database migrations
echo "Running database migrations..."
docker-compose --env-file .env.production exec gateway alembic upgrade head

# Seed initial data
echo "Seeding initial data..."
docker-compose --env-file .env.production exec gateway python -m src.scripts.seed_permissions

# Verify health checks
echo "Verifying health checks..."
./scripts/health-check.sh

echo "Production deployment complete!"
echo "Gateway: https://$TLS_DOMAIN"
echo "API Docs: https://$TLS_DOMAIN/api/docs"
```

Make it executable:

```bash
chmod +x scripts/deploy-production.sh
```

### Step 3: Create Backup Script

#### scripts/backup.sh

Create backup script:

```bash
#!/bin/bash

set -e

BACKUP_DIR=${1:-./backups}
DATE=$(date +%Y%m%d_%H%M%S)

echo "Creating backup..."

# Create backup directory
mkdir -p "$BACKUP_DIR"

# Backup database
echo "Backing up database..."
docker-compose exec -T postgres pg_dump -U postgres mcp_gateway | gzip > "$BACKUP_DIR/db_$DATE.sql.gz"

# Backup Redis data
echo "Backing up Redis data..."
docker-compose exec -T redis redis-cli --rdb - | gzip > "$BACKUP_DIR/redis_$DATE.rdb.gz"

# Backup configuration
echo "Backing up configuration..."
tar -czf "$BACKUP_DIR/config_$DATE.tar.gz" .env.production nginx/ssl/

# Backup logs
echo "Backing up logs..."
docker-compose exec -T gateway tar -czf - /var/log/mcp-gateway > "$BACKUP_DIR/logs_$DATE.tar.gz"

echo "Backup complete: $BACKUP_DIR"
```

Make it executable:

```bash
chmod +x scripts/backup.sh
```

### Step 4: Create Restore Script

#### scripts/restore.sh

Create restore script:

```bash
#!/bin/bash

set -e

BACKUP_FILE=${1:-}
BACKUP_DIR=${2:-./backups}

if [ -z "$BACKUP_FILE" ]; then
    echo "Usage: $0 <backup_file> [backup_dir]"
    exit 1
fi

echo "Restoring from backup: $BACKUP_FILE"

# Restore database
echo "Restoring database..."
gunzip -c "$BACKUP_DIR/db_$BACKUP_FILE.sql.gz" | docker-compose exec -T postgres psql -U postgres mcp_gateway

# Restore Redis data
echo "Restoring Redis data..."
gunzip -c "$BACKUP_DIR/redis_$BACKUP_FILE.rdb.gz" | docker-compose exec -T redis redis-cli --rdb -

echo "Restore complete!"
```

Make it executable:

```bash
chmod +x scripts/restore.sh
```

### Step 5: Create Monitoring Configuration

#### monitoring/prometheus.yml

Create Prometheus configuration:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'mcp-gateway'
    static_configs:
      - targets: ['gateway:8000']
    metrics_path: '/metrics'
```

### Step 6: Create Maintenance Guide

#### docs/maintenance.md

Create maintenance guide:

```markdown
# Maintenance Guide

## Daily Tasks

- Monitor health checks
- Review error logs
- Check resource usage
- Verify backups

## Weekly Tasks

- Review audit logs
- Check security events
- Update dependencies
- Review performance metrics

## Monthly Tasks

- Rotate secrets
- Update SSL certificates
- Review and update security policies
- Performance optimization

## Backup and Recovery

### Creating Backups

```bash
./scripts/backup.sh
```

### Restoring Backups

```bash
./scripts/restore.sh <backup_file>
```

## Updates and Upgrades

### Updating Gateway

1. Backup current deployment
2. Pull latest code
3. Run database migrations
4. Restart services
5. Verify health checks

### Updating Dependencies

1. Review dependency updates
2. Test in staging
3. Update dependencies
4. Run tests
5. Deploy to production
```

## Testing

### Test Production Deployment

Test production deployment:

```bash
# Deploy to production
./scripts/deploy-production.sh

# Verify deployment
./scripts/health-check.sh

# Test backups
./scripts/backup.sh

# Test restore
./scripts/restore.sh <backup_file>
```

## Verification

1. **Deployment**: Production deployment works
2. **Health Checks**: All health checks pass
3. **Backups**: Backups are created successfully
4. **Monitoring**: Monitoring is configured
5. **Security**: Security is hardened

## Troubleshooting

### Issue: Deployment fails

**Solution**: Check logs and verify configuration:
```bash
docker-compose logs
docker-compose config
```

### Issue: Health checks fail

**Solution**: Check service status and dependencies:
```bash
docker-compose ps
./scripts/health-check.sh
```

### Issue: Backups fail

**Solution**: Check disk space and permissions:
```bash
df -h
ls -la backups/
```

## Next Steps

After completing this instruction, proceed to:
- **05-testing/01-unit-tests.md**: Set up unit tests

