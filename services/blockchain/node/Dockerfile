# ==========================================
# Stage 1: Cargo Chef Recipe Planner
# ==========================================
FROM lukemathwalker/cargo-chef:latest-rust-1.93-slim-bookworm AS planner
WORKDIR /app
COPY Cargo.toml Cargo.lock ./
COPY src ./src
RUN cargo chef prepare --recipe-path recipe.json

# ==========================================
# Stage 2: Cache & Build Dependencies
# ==========================================
FROM lukemathwalker/cargo-chef:latest-rust-1.93-slim-bookworm AS builder
WORKDIR /app

# Install build dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    pkg-config \
    libssl-dev \
    && rm -rf /var/lib/apt/lists/*

COPY --from=planner /app/recipe.json recipe.json
# Build and cache dependencies layer
RUN cargo chef cook --release --recipe-path recipe.json

# Build actual application
COPY Cargo.toml Cargo.lock ./
COPY src ./src
RUN cargo build --release --bin blockchain-node

# Strip debug symbols to minimize binary size
RUN strip /app/target/release/blockchain-node

# ==========================================
# Stage 3: Minimal & Secure Production Runtime
# ==========================================
FROM debian:bookworm-slim AS runtime

ARG VERSION=0.1.0
LABEL org.opencontainers.image.title="blockchain-node" \
      org.opencontainers.image.description="Rust P2P Proof-of-Work Blockchain Node Engine" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.authors="KrishnaKasinadhuni" \
      org.opencontainers.image.licenses="MIT"

# Security: Install CA certificates for outbound P2P HTTPS/HTTP requests & curl for healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    tzdata \
    && rm -rf /var/lib/apt/lists/*

# Security: Run as an unprivileged non-root user (Principle of Least Privilege)
RUN groupadd --gid 10001 appgroup && \
    useradd --uid 10001 --gid appgroup --shell /bin/false --no-create-home appuser

WORKDIR /app

# Copy binary from builder stage
COPY --from=builder --chown=appuser:appgroup /app/target/release/blockchain-node /app/blockchain-node

USER appuser:appgroup

# Environment defaults
ENV PORT=3002 \
    RUST_LOG=info \
    CHAIN_DIFFICULTY=2 \
    MINING_REWARD=50

EXPOSE 3002

# Liveness/Readiness Container Healthcheck
HEALTHCHECK --interval=20s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:${PORT}/api/status || exit 1

STOPSIGNAL SIGTERM

ENTRYPOINT ["/app/blockchain-node"]
