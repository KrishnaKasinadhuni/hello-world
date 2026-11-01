# Changelog

All notable changes to the `hello-world` repository will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.4.0] - 2026-10-31

### Added
- **[quest-tennis-trainer]**: Completed Session 16 (`Full-Stack Rig Integration`) wiring all Phase 1, Phase 2, and Phase 3 subsystems (`TrainingHudController`, `TrajectoryArcRenderer`, `RoomScaleGuardianVisualizer`, `SwingCoachAnalyzer`, `RacketAudioSynthesizer`, `AdaptiveDrillDirector`) directly into `TennisSimulationBootstrap.cs` (`ProcessIntegratedRacketStroke`) and `TennisCourtSceneBuilder.cs` (`BuildWiredTrainingRig`), verified by 17/17 NUnit tests.
- **[quest-tennis-trainer]**: Completed Phase 3 (Sessions 13–15) for the Meta Quest 3 VR/MR tennis trainer (`quest-tennis-trainer/`):
  - **Session 13**: Procedural Acoustic Impact Synthesizer (`RacketAudioSynthesizer.cs`) generating 44.1 kHz PCM waveforms for sweet-spot stringbed "pop" (`420 Hz` + `1120 Hz` harmonic + felt snap), graphite frame "clank" (`940 Hz` + `1610 Hz`), and court/wall bounce thuds (`120–195 Hz`), paired 1:1 with `RacketHapticsController.cs`.
  - **Session 14**: Adaptive AI Drill Difficulty Director (`AdaptiveDrillDirector.cs`) scaling `BallMachineController` feed speed (`62–124 km/h`), spin (`800–3200 RPM`), and interval (`3.8–1.7s`) across 5 progressive skill tiers based on player sweet-spot accuracy and consistency, clamped within $\pm 1.20\text{m}$ room-scale reach.
  - **Session 15**: Local-first TOON stroke telemetry persistence & SHA-256 integrity verifier (`SessionTelemetryPersistence.cs`) with `.toon` file signing, tamper detection, and GCP Cloud Storage / Firestore JSON envelope generation.
- **[quest-tennis-trainer]**: Completed Phase 2 (Sessions 9–12) for the Meta Quest 3 VR/MR tennis trainer (`quest-tennis-trainer/`):
  - **Session 9**: Spatial Training HUD & Serve Speed Radar (`TrainingHudController.cs`), non-dominant wrist HUD, 3D trajectory ribbon, toss apex target ring, and color-coded court bounce markers (`TrajectoryArcRenderer.cs`).
  - **Session 10**: Quest 3 Mixed Reality (Passthrough) & $\pm 1.2\text{m}$ Room-Scale Safety Ring (`RoomScaleGuardianVisualizer.cs`) with `SafeCenter` / `CautionNearEdge` / `BoundaryBreached` states, dynamic MR passthrough fade, and automatic ball machine pause on boundary breach.
  - **Session 11**: Biomechanical Swing Coach (`SwingCoachAnalyzer.cs`) evaluating low-to-high swing path angle ($\text{deg}$), racket face tilt ($\text{deg}$), actionable coaching cues, and compact TOON tabular stroke telemetry export (`ExportToonTelemetry()`).
  - **Session 12**: Automated Unity 6 Quest 3 APK build script (`QuestAndroidBuildScript.cs`) targeting `ARM64`, `IL2CPP`, `Vulkan`, and `120 Hz` (`build/Android/QuestTennisTrainer.apk`).
- **[quest-tennis-trainer]**: Added Unity 6 C# single-player Meta Quest 3 VR/MR tennis training simulation (`quest-tennis-trainer/`) featuring 120 Hz sub-frame physics (`TennisPhysicsConfig.cs`), Magnus aerodynamics & surface restitution (`BallAerodynamics.cs`), 10-substep cubic Hermite spline racket collision & stringbed spin solver (`SweptRacketCollider.cs`), Tennis Wall with Smart Return Funnel (`TennisWallController.cs`), parametric OpenXR haptics (`RacketHapticsController.cs`), Programmable Ball Machine (`BallMachineController.cs`), two-handed Serve Practice with 200 ms IMU occlusion compensation (`ServeTossController.cs`), NUnit test suite (`TennisPhysicsTests.cs`), hardened distroless `.NET 8` Docker sandbox (`Dockerfile`), and GHCR + GCP Workload Identity Federation CI/CD workflow (`.github/workflows/quest-tennis-ci.yml`).
- **[security]**: Added repository-wide public security governance (`LICENSE` MIT, `SECURITY.md`, `.github/CODEOWNERS`, `.github/dependabot.yml`), scrubbed AWS account IDs from tracked files, and added least-privilege `permissions` and `concurrency` guards across GitHub Actions workflows.
- **[aws-ecr]**: Added declarative AWS CDK `EcrStack` in `cdk-patterns/lib/ecr-stack.ts` with unit tests (`test/ecr.test.ts`) provisioning 4 core microservice repositories (`hello-world/image-classification`, `hello-world/log-analyzer`, `hello-world/config-manager`, `hello-world/reverse-image-search-backend`) with `scanOnPush`, AES256 encryption, and 7-day untagged / 10-image retention lifecycle policies.
- **[aws-ecr]**: Added `services/aws/scripts/ecr-pipeline.sh` automation CLI for AWS ECR authentication, repository setup, multi-platform Docker container builds (`linux/amd64`), and automated tagging/pushing (`:latest`, `:<git-sha>`).
- **[aws-ecr]**: Successfully built and published container images for all 4 core microservices to Amazon ECR registry `<AWS_ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com`.
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
