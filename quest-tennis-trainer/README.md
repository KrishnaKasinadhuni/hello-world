# Quest 3 Tennis Trainer (`quest-tennis-trainer`)

Single-player VR/MR tennis training simulation for **Meta Quest 3** built with **Unity 6 (C#)**, **OpenXR**, and **120 Hz sub-frame physics**. Designed specifically for **basic room-scale movement** (1–2 step radius around a home center marker) with realistic aerodynamics, swept-spline racket collision, and parametric haptics.

## Core Training Systems

1. **Programmable Ball Machine**: Configurable feed interval, launch speed, spin (topspin/backspin RPM), and oscillation constrained to the player's calibrated room-scale reach ($\pm 1.2\text{m}$).
2. **Tennis Wall (Backboard)**: Vertical practice wall with net-line fault detection, target zones, vertical spin reflection, and a **Smart Return Funnel** (85% true angle reflection + 15% centering bias) to keep long rallies inside a 2m living-room space.
3. **Serve Practice**: Two-handed tracking with left-hand toss physics, behind-the-back IMU occlusion compensation during the racket drop, and service-box radar/spin telemetry for Flat, Slice, and Kick serves.

## Folder Structure

```text
quest-tennis-trainer/
├── .gitignore
├── README.md
└── Assets/
    └── _Project/
        ├── Core/               # 120 Hz bootstrap, room-scale boundary calibration, physics constants
        ├── XR_Input/           # Racket grip offset, serve toss release, Meta Haptics SDK controller
        ├── Physics/            # Magnus aerodynamics, 10-substep swept collider, stringbed spin model
        ├── TrainingModes/
        │   ├── BallMachine/    # Trajectory solver & drill ScriptableObjects
        │   ├── WallPractice/   # Backboard rebound funnel & rally streak tracker
        │   └── ServeTrainer/   # Toss apex guide, fault detector & radar telemetry
        └── Tests/              # Fast NUnit C# math & physics tests for CI/CD
```

## Credit-Friendly 5-Session Build Plan

To keep token and AI Pro credit usage low, development is split into 5 self-contained sessions. Each session touches only 2–3 files:

- **[x] Session 1 — Repo Foundation & Physics Constants**: Create `feat/quest-tennis-trainer` branch, `.gitignore`, project roadmap, and `TennisPhysicsConfig.cs` baseline constants.
- **[x] Session 2 — Core Ball Aerodynamics & Swept Racket Physics**: Implement `BallAerodynamics.cs` (gravity + drag + Magnus spin curve) and `SweptRacketCollider.cs` (120 Hz 10-substep collision + stringbed spin generation).
- **[x] Session 3 — Tennis Wall & Haptics**: Implement `TennisWallController.cs` (with Smart Return Funnel for 1–2 step movement) and `RacketHapticsController.cs` (sweet-spot vs. frame vibration).
- **[x] Session 4 — Programmable Ball Machine & Serve Practice**: Implement `BallMachineController.cs` (ballistic trajectory solver) and `ServeTossController.cs` (toss release + serve box fault/radar tracker).
- **[x] Session 5 — GitHub Actions CI/CD & GCP Artifact Pipeline**: Add `TennisPhysicsTests.cs` NUnit suite and `.github/workflows/quest-tennis-ci.yml` with Workload Identity Federation for Google Cloud Storage / Artifact Registry.
- **[x] Session 6 — Hardened Distroless Docker Sandbox & `.NET 8` NUnit Runner**: Add `Dockerfile` (`mcr.microsoft.com/dotnet/runtime:8.0-noble-chiseled`), `.dockerignore`, `QuestTennisTrainer.Tests.csproj`, and `UnityEngineStubs.cs` (`#if !UNITY_5_3_OR_NEWER`).
- **[x] Session 7 — Zero-Click 3D Scene Bootstrapper**: Add `TennisSimulationBootstrap.cs` (120 Hz lock & mode switcher) and `TennisCourtSceneBuilder.cs` (procedural regulation court & rig wiring).
- **[ ] Session 8 — Unity 6 Package Manifest & Sandbox CLI Script**: Add `Packages/manifest.json` and `scripts/sandbox.sh`.

## Running the Hardened Physics Sandbox Locally

```bash
docker build -t quest-tennis-sandbox:latest quest-tennis-trainer
docker run --rm --read-only --cap-drop=ALL --security-opt=no-new-privileges quest-tennis-sandbox:latest
```
