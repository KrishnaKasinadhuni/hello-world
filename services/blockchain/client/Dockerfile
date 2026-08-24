# ==========================================
# Stage 1: Build Production Assets
# ==========================================
FROM node:24-alpine AS builder
WORKDIR /app

# Optimize layer caching: copy dependency manifests first
COPY package*.json ./
RUN npm ci --ignore-scripts

# Copy source code and build
COPY tsconfig*.json vite.config.ts index.html ./
COPY src ./src

RUN npm run build

# ==========================================
# Stage 2: Unprivileged Minimal NGINX Server
# ==========================================
FROM nginxinc/nginx-unprivileged:alpine-slim AS runtime

ARG VERSION=0.1.0
LABEL org.opencontainers.image.title="blockchain-client" \
      org.opencontainers.image.description="TypeScript 7 & React 19 Blockchain Web Explorer" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.authors="KrishnaKasinadhuni" \
      org.opencontainers.image.licenses="MIT"

# Copy built SPA artifacts to Nginx docroot
COPY --from=builder --chown=nginx:nginx /app/dist /usr/share/nginx/html

# Copy custom Nginx configuration
COPY --chown=nginx:nginx nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 8080

# Container Healthcheck
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/healthz || exit 1

STOPSIGNAL SIGQUIT

CMD ["nginx", "-g", "daemon off;"]
