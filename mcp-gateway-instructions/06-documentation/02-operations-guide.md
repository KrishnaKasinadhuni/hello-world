# 02: Operations Guide

## Objective

Create a comprehensive operations guide for the MCP Gateway, including deployment procedures, monitoring, troubleshooting, and maintenance tasks.

## Prerequisites

- Completed: All previous instructions
- Understanding of operations and DevOps
- Knowledge of monitoring and logging

## Implementation Steps

### Step 1: Create Operations Guide

#### docs/operations.md

Create operations guide:

```markdown
# MCP Gateway Operations Guide

## Table of Contents

1. [Deployment](#deployment)
2. [Monitoring](#monitoring)
3. [Troubleshooting](#troubleshooting)
4. [Maintenance](#maintenance)
5. [Backup and Recovery](#backup-and-recovery)
6. [Security](#security)

## Deployment

### Initial Deployment

1. **Prerequisites**
   - Docker and Docker Compose installed
   - Domain name configured
   - SSL certificates prepared

2. **Configuration**
   - Copy `.env.example` to `.env.production`
   - Update environment variables
   - Generate secure secrets
   - Configure SSL certificates

3. **Deployment**
   ```bash
   ./scripts/deploy-production.sh
   ```

4. **Verification**
   ```bash
   ./scripts/health-check.sh
   ```

### Updates and Upgrades

1. **Backup Current Deployment**
   ```bash
   ./scripts/backup.sh
   ```

2. **Update Code**
   ```bash
   git pull
   ```

3. **Run Migrations**
   ```bash
   docker-compose exec gateway alembic upgrade head
   ```

4. **Restart Services**
   ```bash
   docker-compose restart
   ```

5. **Verify Deployment**
   ```bash
   ./scripts/health-check.sh
   ```

## Monitoring

### Health Checks

Monitor health checks regularly:

```bash
# Check gateway health
curl http://localhost:8000/health

# Check detailed health
curl http://localhost:8000/health/detailed

# Check readiness
curl http://localhost:8000/health/ready

# Check liveness
curl http://localhost:8000/health/live
```

### Logs

Monitor logs for errors:

```bash
# Gateway logs
docker-compose logs -f gateway

# Database logs
docker-compose logs -f postgres

# Redis logs
docker-compose logs -f redis

# Nginx logs
docker-compose logs -f nginx

# All logs
docker-compose logs -f
```

### Metrics

Monitor resource usage:

```bash
# Container stats
docker stats

# Disk usage
df -h

# Memory usage
free -h
```

## Troubleshooting

### Common Issues

#### Service Won't Start

1. Check Docker status:
   ```bash
   docker ps
   docker-compose ps
   ```

2. Check logs:
   ```bash
   docker-compose logs gateway
   ```

3. Check configuration:
   ```bash
   docker-compose config
   ```

#### Database Connection Issues

1. Check database status:
   ```bash
   docker-compose exec postgres pg_isready
   ```

2. Check database logs:
   ```bash
   docker-compose logs postgres
   ```

3. Verify connection string:
   ```bash
   echo $DATABASE_URL
   ```

#### Redis Connection Issues

1. Check Redis status:
   ```bash
   docker-compose exec redis redis-cli ping
   ```

2. Check Redis logs:
   ```bash
   docker-compose logs redis
   ```

#### Authentication Issues

1. Check JWT secret key:
   ```bash
   echo $JWT_SECRET_KEY
   ```

2. Check user in database:
   ```bash
   docker-compose exec postgres psql -U postgres -d mcp_gateway -c "SELECT * FROM users;"
   ```

#### Rate Limiting Issues

1. Check Redis:
   ```bash
   docker-compose exec redis redis-cli keys "rate_limit:*"
   ```

2. Check rate limit configuration:
   ```bash
   echo $RATE_LIMIT_PER_MINUTE
   ```

## Maintenance

### Daily Tasks

- Monitor health checks
- Review error logs
- Check resource usage
- Verify backups

### Weekly Tasks

- Review audit logs
- Check security events
- Update dependencies
- Review performance metrics

### Monthly Tasks

- Rotate secrets
- Update SSL certificates
- Review and update security policies
- Performance optimization

### Database Maintenance

```bash
# Vacuum database
docker-compose exec postgres psql -U postgres -d mcp_gateway -c "VACUUM;"

# Analyze database
docker-compose exec postgres psql -U postgres -d mcp_gateway -c "ANALYZE;"

# Check database size
docker-compose exec postgres psql -U postgres -d mcp_gateway -c "SELECT pg_size_pretty(pg_database_size('mcp_gateway'));"
```

### Log Rotation

Configure log rotation:

```bash
# Rotate logs
docker-compose exec gateway logrotate -f /etc/logrotate.conf
```

## Backup and Recovery

### Creating Backups

```bash
# Create backup
./scripts/backup.sh

# Backup to remote location
./scripts/backup.sh | ssh user@backup-server "cat > backup.tar.gz"
```

### Restoring Backups

```bash
# Restore from backup
./scripts/restore.sh <backup_file>
```

### Backup Schedule

Set up automated backups:

```bash
# Add to crontab
0 2 * * * /path/to/scripts/backup.sh
```

## Security

### Security Monitoring

- Monitor audit logs regularly
- Review security events
- Check for suspicious activity
- Verify SSL certificates

### Security Updates

- Update dependencies regularly
- Apply security patches
- Review security policies
- Update SSL certificates

### Incident Response

1. **Identify Incident**
   - Check logs
   - Review security events
   - Verify alerts

2. **Contain Incident**
   - Isolate affected systems
   - Block suspicious IPs
   - Disable affected services

3. **Remediate Incident**
   - Fix vulnerabilities
   - Update security policies
   - Restore from backup if needed

4. **Post-Incident**
   - Review incident
   - Update procedures
   - Document lessons learned

## Performance Optimization

### Database Optimization

- Index frequently queried columns
- Optimize queries
- Monitor query performance
- Regular vacuum and analyze

### Caching

- Use Redis for caching
- Cache frequently accessed data
- Set appropriate TTLs
- Monitor cache hit rates

### Resource Management

- Monitor resource usage
- Adjust resource limits
- Scale services as needed
- Optimize container resources

## Support

### Getting Help

- Check documentation
- Review logs
- Check health status
- Contact support

### Reporting Issues

When reporting issues, include:
- Error messages
- Logs
- Configuration
- Steps to reproduce
```

### Step 2: Create Quick Reference Guide

#### docs/quick-reference.md

Create quick reference guide:

```markdown
# MCP Gateway Quick Reference

## Common Commands

### Starting Services
```bash
docker-compose up -d
```

### Stopping Services
```bash
docker-compose down
```

### Viewing Logs
```bash
docker-compose logs -f gateway
```

### Health Checks
```bash
curl http://localhost:8000/health
```

### Database Access
```bash
docker-compose exec postgres psql -U postgres -d mcp_gateway
```

### Redis Access
```bash
docker-compose exec redis redis-cli
```

### Creating Backups
```bash
./scripts/backup.sh
```

### Restoring Backups
```bash
./scripts/restore.sh <backup_file>
```

## Environment Variables

### Required
- `DATABASE_URL`: Database connection string
- `REDIS_HOST`: Redis host
- `JWT_SECRET_KEY`: JWT secret key

### Optional
- `DEBUG`: Enable debug mode
- `LOG_LEVEL`: Logging level
- `RATE_LIMIT_PER_MINUTE`: Rate limit per minute

## API Endpoints

### Authentication
- `POST /api/auth/login`: Login
- `POST /api/auth/refresh`: Refresh token
- `GET /api/auth/me`: Get current user

### MCP Servers
- `GET /api/servers`: List servers
- `POST /api/servers`: Register server
- `GET /api/servers/{id}`: Get server
- `PUT /api/servers/{id}`: Update server
- `DELETE /api/servers/{id}`: Delete server

## Troubleshooting

### Service Won't Start
1. Check Docker: `docker ps`
2. Check logs: `docker-compose logs`
3. Check config: `docker-compose config`

### Database Issues
1. Check status: `docker-compose exec postgres pg_isready`
2. Check logs: `docker-compose logs postgres`

### Redis Issues
1. Check status: `docker-compose exec redis redis-cli ping`
2. Check logs: `docker-compose logs redis`
```

### Step 3: Create Runbook

#### docs/runbook.md

Create runbook for common operations:

```markdown
# MCP Gateway Runbook

## Incident Response

### Service Down

1. Check service status:
   ```bash
   docker-compose ps
   ```

2. Check health:
   ```bash
   curl http://localhost:8000/health
   ```

3. Check logs:
   ```bash
   docker-compose logs gateway
   ```

4. Restart service:
   ```bash
   docker-compose restart gateway
   ```

### Database Issues

1. Check database:
   ```bash
   docker-compose exec postgres pg_isready
   ```

2. Check connections:
   ```bash
   docker-compose exec postgres psql -U postgres -c "SELECT count(*) FROM pg_stat_activity;"
   ```

3. Restart database:
   ```bash
   docker-compose restart postgres
   ```

### High Memory Usage

1. Check memory:
   ```bash
   docker stats
   ```

2. Check logs:
   ```bash
   docker-compose logs gateway
   ```

3. Restart service:
   ```bash
   docker-compose restart gateway
   ```

### Rate Limiting Issues

1. Check Redis:
   ```bash
   docker-compose exec redis redis-cli keys "rate_limit:*"
   ```

2. Clear rate limits:
   ```bash
   docker-compose exec redis redis-cli FLUSHDB
   ```

### SSL Certificate Issues

1. Check certificates:
   ```bash
   ls -la nginx/ssl/
   ```

2. Renew certificates:
   ```bash
   ./scripts/setup-letsencrypt.sh
   ```

3. Reload Nginx:
   ```bash
   docker-compose exec nginx nginx -s reload
   ```
```

## Testing

### Test Operations Guide

Verify operations guide:

```bash
# Test deployment
./scripts/deploy-production.sh

# Test health checks
./scripts/health-check.sh

# Test backups
./scripts/backup.sh

# Test restore
./scripts/restore.sh <backup_file>
```

## Verification

1. **Operations Guide**: Operations guide is complete
2. **Quick Reference**: Quick reference is available
3. **Runbook**: Runbook is created
4. **Documentation**: All documentation is accessible

## Troubleshooting

### Issue: Documentation not accessible

**Solution**: Check documentation files and paths:
```bash
ls -la docs/
```

### Issue: Operations guide incomplete

**Solution**: Review and complete operations guide with all necessary procedures.

## Next Steps

After completing this instruction, all instruction sets are complete. Review the complete set of instructions and ensure everything is properly documented and tested.

