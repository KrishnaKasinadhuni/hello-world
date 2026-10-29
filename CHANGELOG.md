# Changelog

All notable changes to the `hello-world` repository will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **[quest-tennis-trainer]**: Added Unity 6 C# single-player Meta Quest 3 VR/MR tennis training simulation (`quest-tennis-trainer/`) featuring 120 Hz sub-frame physics (`TennisPhysicsConfig.cs`), Magnus aerodynamics & surface restitution (`BallAerodynamics.cs`), 10-substep cubic Hermite spline racket collision & stringbed spin solver (`SweptRacketCollider.cs`), Tennis Wall with Smart Return Funnel (`TennisWallController.cs`), parametric OpenXR haptics (`RacketHapticsController.cs`), Programmable Ball Machine (`BallMachineController.cs`), two-handed Serve Practice with 200 ms IMU occlusion compensation (`ServeTossController.cs`), NUnit test suite (`TennisPhysicsTests.cs`), and GCP Workload Identity Federation CI/CD workflow (`.github/workflows/quest-tennis-ci.yml`).
- **[aws-ecr]**: Added declarative AWS CDK `EcrStack` in `cdk-patterns/lib/ecr-stack.ts` with unit tests (`test/ecr.test.ts`) provisioning 4 core microservice repositories (`hello-world/image-classification`, `hello-world/log-analyzer`, `hello-world/config-manager`, `hello-world/reverse-image-search-backend`) with `scanOnPush`, AES256 encryption, and 7-day untagged / 10-image retention lifecycle policies.
- **[aws-ecr]**: Added `services/aws/scripts/ecr-pipeline.sh` automation CLI for AWS ECR authentication, repository setup, multi-platform Docker container builds (`linux/amd64`), and automated tagging/pushing (`:latest`, `:<git-sha>`).
- **[aws-ecr]**: Successfully built and published container images for all 4 core microservices to Amazon ECR registry `908027415245.dkr.ecr.us-east-1.amazonaws.com`.
- **[blockchain]**: Implemented **Semantic Versioning (SemVer)** for Docker container images (`blockchain-node:${VERSION}`, `blockchain-client:${VERSION}`) with OCI version labels and build args.
- **[blockchain]**: Added modern production **Dockerfiles & `docker-compose.yml`** for both Rust node (multi-stage `cargo-chef` layer caching, non-root `appuser`, healthchecks) and TypeScript 7 client (unprivileged Nginx, gzip compression, security headers, immutable caching).
- **[blockchain]**: Added **Multi-Node Peer-to-Peer (P2P) Network & Nakamoto Consensus Layer** (`peer.rs`, `consensus.rs`, `gossip.rs`): peer discovery, P2P block & transaction gossip propagation, and longest valid chain conflict resolution.
- **[blockchain]**: Added **P2P Network Mesh Control UI** (`PeersTab.tsx`) in TypeScript 7 client for registering remote node URLs, monitoring connected peers, and triggering network consensus synchronization.
- **[blockchain]**: Extended TypeScript 7 client UI with interactive **Address Inspector Modal** (balance, sent/received volume, activity history) and **Transaction Inspector Modal** (confirmation count, cryptographic validity, sender/recipient links).
- **[blockchain]**: Added **Analytics Dashboard & Miner Leaderboard** (`AnalyticsTab.tsx`) with miner rankings, chain share %, rewards earned, throughput metrics (avg block time, txs/block, block density visualizer), and mempool aging/health metrics.
- **[blockchain]**: Rust 1.93+ Tokio/Axum Proof-of-Work node engine (`services/blockchain/node/`) featuring SHA-256 block hashing, account state ledger, mempool, and REST/RPC API.
- **[blockchain]**: TypeScript 7.0+ React 19 Web Explorer UI & Client SDK (`services/blockchain/client/`) with glassmorphism UI, network metrics, transaction broadcasting, and block miner.
- **[docs]**: Established root `CHANGELOG.md` and codified changelog update requirements in `AGENTS.md` and `CLAUDE.md`.

### Changed
- **[docs]**: Updated `README.md`, `AGENTS.md`, and `CLAUDE.md` repository map, subproject classification table, and build/test commands.

## [0.3.0] - 2026-08-22

### Added
- **[gcp-gateway]**: Hosted Remote MCP Gateway on GCP Cloud Run with Google OAuth 2.0 OIDC auth (#11).
- **[gcp-gateway]**: Integrated GCP Secret Manager for dynamic email whitelisting and 403 Forbidden authorization guard (#15).
- **[gcp-gateway]**: Added Tech Intelligence Radar tool with TTL memory retention engine (#13).
- **[gcp-gateway]**: Added unit and integration test suite for FastMCP tools and gateway endpoints (#12).

### Security
- **[gcp-gateway]**: Provisioned persistent GCS memory bucket with uniform bucket-level access control and public access prevention (#16, #17).
- **[gcp-gateway]**: Configured 30-day GCS object lifecycle expiration policy and application retention defaults (#18).
- **[security]**: Remediated Dependabot security alerts across all Node.js subproject manifests (#10).

## [0.2.0] - 2026-08-15

### Added
- **[cdk]**: Express Mode rapid deployment workflow (`npm run deploy:dev`) for `cdk-patterns` (#9).

### Changed
- **[docs]**: Updated default Git branch references from `master` to `main`.

## [0.1.0] - 2026-08-08

### Added
- Initial sandbox repository initialization with multi-stack subprojects:
  - `frontend/`: React + MUI image classification UI.
  - `reverse-image-search-aws/`: FastAPI + AWS Bedrock Titan embedding engine.
  - `services/nodejs/imageClassification/`: Node.js Hapi + Cohere AI REST microservice.
  - `services/python/`: Python ML log analyzer (`logAnalyzer/`), config manager (`configManager/`), and MOBI ebook converter (`ebookConverter/`).
