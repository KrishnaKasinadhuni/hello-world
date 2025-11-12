# 03: TLS/SSL

## Objective

Configure TLS/SSL encryption for the MCP Gateway using Nginx as a reverse proxy, including certificate management, HTTPS enforcement, and secure configuration.

## Prerequisites

- Completed: 01-setup/03-docker-base-config.md
- Understanding of TLS/SSL certificates
- Knowledge of Nginx configuration
- Access to certificate authority (Let's Encrypt for production, self-signed for development)

## Implementation Steps

### Step 1: Create Certificate Generation Script

#### scripts/generate-certs.sh

Create script to generate SSL certificates:

```bash
#!/bin/bash

set -e

echo "Generating SSL certificates..."

# Create SSL directory
mkdir -p nginx/ssl

# Generate self-signed certificate for development
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
    -keyout nginx/ssl/key.pem \
    -out nginx/ssl/cert.pem \
    -subj "/C=US/ST=State/L=City/O=Organization/CN=localhost"

echo "Certificates generated successfully!"
echo "Certificate: nginx/ssl/cert.pem"
echo "Private Key: nginx/ssl/key.pem"
```

Make it executable:

```bash
chmod +x scripts/generate-certs.sh
```

### Step 2: Update Nginx Configuration for TLS

#### nginx/nginx.conf

Update Nginx configuration to enable TLS:

```nginx
# HTTPS server configuration
server {
    listen 443 ssl http2;
    server_name _;

    # SSL certificate paths
    ssl_certificate /etc/nginx/ssl/cert.pem;
    ssl_certificate_key /etc/nginx/ssl/key.pem;

    # SSL protocol configuration
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers 'ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384';
    ssl_prefer_server_ciphers on;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 10m;
    ssl_session_tickets off;

    # OCSP stapling
    ssl_stapling on;
    ssl_stapling_verify on;

    # Security headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains; preload" always;
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header Referrer-Policy "no-referrer-when-downgrade" always;

    # API endpoints
    location /api/ {
        proxy_pass http://gateway;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Health check
    location /health {
        proxy_pass http://gateway;
        access_log off;
    }

    # Default location
    location / {
        proxy_pass http://gateway;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

# HTTP server - redirect to HTTPS
server {
    listen 80;
    server_name _;

    # Health check (no redirect)
    location /health {
        proxy_pass http://gateway;
        access_log off;
    }

    # Redirect all other traffic to HTTPS
    location / {
        return 301 https://$host$request_uri;
    }
}
```

### Step 3: Update Docker Compose for TLS

#### docker-compose.yml

Update Docker Compose to mount SSL certificates:

```yaml
services:
  nginx:
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./nginx/ssl:/etc/nginx/ssl:ro
    environment:
      - TLS_ENABLED=true
```

### Step 4: Create Let's Encrypt Configuration (Production)

#### scripts/setup-letsencrypt.sh

Create script for Let's Encrypt setup:

```bash
#!/bin/bash

set -e

DOMAIN=${1:-localhost}
EMAIL=${2:-admin@example.com}

echo "Setting up Let's Encrypt for domain: $DOMAIN"

# Install certbot
apt-get update
apt-get install -y certbot

# Generate certificate
certbot certonly --standalone \
    --email $EMAIL \
    --agree-tos \
    --no-eff-email \
    -d $DOMAIN

# Copy certificates to nginx/ssl
cp /etc/letsencrypt/live/$DOMAIN/fullchain.pem nginx/ssl/cert.pem
cp /etc/letsencrypt/live/$DOMAIN/privkey.pem nginx/ssl/key.pem

echo "Let's Encrypt certificates installed successfully!"
```

### Step 5: Create Certificate Renewal Script

#### scripts/renew-certs.sh

Create script for certificate renewal:

```bash
#!/bin/bash

set -e

echo "Renewing SSL certificates..."

# Renew Let's Encrypt certificates
certbot renew --quiet

# Reload Nginx
docker-compose exec nginx nginx -s reload

echo "Certificates renewed successfully!"
```

### Step 6: Update Environment Configuration

#### .env.example

Add TLS configuration:

```env
# TLS Configuration
TLS_ENABLED=True
TLS_CERT_PATH=/etc/nginx/ssl/cert.pem
TLS_KEY_PATH=/etc/nginx/ssl/key.pem
TLS_DOMAIN=localhost
```

### Step 7: Create TLS Verification Script

#### scripts/verify-tls.sh

Create script to verify TLS configuration:

```bash
#!/bin/bash

set -e

DOMAIN=${1:-localhost}

echo "Verifying TLS configuration for $DOMAIN..."

# Check certificate
openssl s_client -connect $DOMAIN:443 -servername $DOMAIN < /dev/null 2>/dev/null | \
    openssl x509 -noout -dates

# Check TLS version
openssl s_client -connect $DOMAIN:443 -tls1_2 < /dev/null > /dev/null 2>&1
if [ $? -eq 0 ]; then
    echo "TLS 1.2: Supported"
else
    echo "TLS 1.2: Not supported"
fi

openssl s_client -connect $DOMAIN:443 -tls1_3 < /dev/null > /dev/null 2>&1
if [ $? -eq 0 ]; then
    echo "TLS 1.3: Supported"
else
    echo "TLS 1.3: Not supported"
fi

echo "TLS verification complete!"
```

## Testing

### Test TLS Configuration

Test TLS configuration:

```bash
# Generate certificates
./scripts/generate-certs.sh

# Start services
docker-compose up -d

# Test HTTPS
curl -k https://localhost/health

# Verify TLS
./scripts/verify-tls.sh localhost

# Test certificate
openssl s_client -connect localhost:443 -servername localhost
```

## Verification

1. **Certificates Generated**: SSL certificates are generated
2. **HTTPS Enabled**: HTTPS is enabled and working
3. **HTTP Redirect**: HTTP requests redirect to HTTPS
4. **Security Headers**: Security headers are set
5. **TLS Version**: TLS 1.2 and 1.3 are supported

## Troubleshooting

### Issue: Certificate generation fails

**Solution**: Ensure OpenSSL is installed:
```bash
apt-get install openssl
```

### Issue: Nginx can't find certificates

**Solution**: Check certificate paths and permissions:
```bash
ls -la nginx/ssl/
chmod 644 nginx/ssl/cert.pem
chmod 600 nginx/ssl/key.pem
```

### Issue: HTTPS not working

**Solution**: Check Nginx configuration and logs:
```bash
docker-compose logs nginx
docker-compose exec nginx nginx -t
```

## Next Steps

After completing this instruction, proceed to:
- **04-network-isolation.md**: Implement network isolation

