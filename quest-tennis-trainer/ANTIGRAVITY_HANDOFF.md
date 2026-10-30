# Antigravity Handoff & Context Export: Quest 3 Tennis Trainer

## 1. Project Overview & Core Requirements
- **Target Hardware**: Meta Quest 3 (Standalone VR / Mixed Reality, Snapdragon XR2 Gen 2)
- **Engine & Stack**: Unity 6 LTS (C#), OpenXR, Meta XR SDK, Meta Haptics SDK, Universal Render Pipeline (URP) with Vulkan & Dynamic Fixed Foveated Rendering (FFR)
- **Physics Rate**: Locked **120 Hz** (`Time.fixedDeltaTime = 0.008333f`) with **10 sub-steps** (~0.83 ms collision resolution) for high-speed swings and serves.
- **Locomotion Constraint**: **Basic room-scale movement only** ($\pm 1.2\text{m}$ / 1–2 step radius around a home center marker). Zero artificial thumbstick locomotion to prevent motion sickness and allow real-world split-steps, unit turns, and weight transfer inside a home play space.
- **Cloud & CI/CD Preference**: 100% local-first gameplay; **GitHub Actions** for CI/CD and **Google Cloud Platform (GCP)** (Google Cloud Storage / Artifact Registry via Workload Identity Federation, plus optional Cloud Firestore) for build storage and telemetry backup.
- **Pacing Rule**: Execute in small, modular sessions (2–3 files per session) to conserve AI Pro credits.

## 2. Three Single-Player Training Systems
1. **Programmable Ball Machine**:
   - Configurable feed interval (1.5–6s), launch speed (30–130 km/h), elevation angle, and spin presets (-2,000 to +3,500 RPM topspin/backspin).
   - Reads Quest 3 Guardian width and constrains feeds to the player's $\pm 1.2\text{m}$ lateral strike zone.
   - Real-time net clearance height, landing depth heatmaps, and target zone streak counters.
2. **Tennis Wall (Backboard)**:
   - Vertical practice wall with painted 0.914m net line, target boxes, and vertical spin reflection (topspin kicks upward off the wall).
   - Adjustable wall distance (6m for rapid reflex volleys, 12m for baseline groundstrokes).
   - **Smart Return Funnel**: Blends 85% true reflection angle with 15% lateral centering bias (`WallSmartFunnelBias = 0.15f`) so slightly off-angle shots still rebound inside the player's 2m room-scale space.
3. **Serve Practice**:
   - Two-handed tracking: Non-dominant controller handles **Ball Toss** (release timing, velocity smoothing filter, peak-height indicator) while dominant controller tracks **Trophy Pose, Racket Drop, and Pronation/Brush** for Flat, Slice, and Kick serves.
   - Includes **Serve Overhead Tracking Compensation** (100–200 ms IMU velocity integration when the racket drops behind the head/back out of headset camera line-of-sight).
   - Stationary Deuce and Ad baseline markers, service box target cones (Wide, Body, T), fault/in detection, serve speed radar (km/h), and spin RPM/axis readout.

## 3. Realistic Physics & Haptics Architecture
- **Sub-Frame Swept Racket Collision (`SweptRacketCollider.cs`)**: Interpolates 10 sub-steps between previous and current controller poses along a cubic Hermite spline using `OVRInput.GetLocalControllerVelocity()` and `GetLocalControllerAngularVelocity()`.
- **Oblique Stringbed Impact & Spin Model (`StringbedImpactModel.cs`)**: Decomposes relative velocity into Normal rebound ($e_n \approx 0.82$ at sweet spot, $0.45$ near frame) and Tangential string brush to calculate outgoing topspin, slice, and kick-serve RPM.
- **Aerodynamics & Bounce (`BallAerodynamics.cs`)**: Calculates gravity ($9.81\text{ m/s}^2$), air drag ($\mathbf{F}_d = -\frac{1}{2} \rho A C_d |\mathbf{v}| \mathbf{v}$), and the **Magnus Effect** ($\mathbf{F}_m = \frac{1}{2} \rho A C_L |\mathbf{v}| (\hat{\boldsymbol{\omega}} \times \mathbf{v})$), plus surface restitution for Hard Court, Clay, Grass, and Concrete Wall.
- **Parametric Haptics (`RacketHapticsController.cs`)**:
  - Sweet-Spot Hit: Crisp high-frequency pulse (`200 Hz`, `15 ms`) scaled by impact velocity.
  - Frame Mishit: Low-frequency vibration (`75 Hz`, `35 ms`) with dampened audio and reduced COR.
  - Serve Toss Release: Subtle micro-tick (`100 Hz`, `5 ms`) as the ball leaves virtual fingertips.

## 4. Current Repository State
- **Local Repo Path**: `/Users/krishnakasinadhuni/projects/hello-world`
- **Subproject Path**: `/Users/krishnakasinadhuni/projects/hello-world/quest-tennis-trainer`
- **Active Git Branch**: `feat/quest-tennis-trainer`
- **Completed & Committed Files (All 5 Sessions Complete)**:
  - `quest-tennis-trainer/README.md` — Architecture overview and 5-session roadmap.
  - `quest-tennis-trainer/.gitignore` — Unity 6, C#, and Meta Quest `.apk`/`.aab` ignore rules.
  - `quest-tennis-trainer/Assets/_Project/Core/TennisPhysicsConfig.cs` — Pure C# struct with 120 Hz timing, ITF ball constants, drag/Magnus coefficients, stringbed restitution, and room-scale reach parameters.
  - `quest-tennis-trainer/Assets/_Project/Physics/BallAerodynamics.cs` — Pure C# / Unity aerodynamic solver for gravity, air drag, Magnus spin curve, and court/wall bounce restitution.
  - `quest-tennis-trainer/Assets/_Project/Physics/SweptRacketCollider.cs` — 120 Hz 10-substep Hermite spline collision detector with stringbed sweet-spot vs frame restitution, tangential friction, and topspin/slice/kick spin calculation.
  - `quest-tennis-trainer/Assets/_Project/TrainingModes/WallPractice/TennisWallController.cs` — Backboard controller with 0.914m net-line fault detection, target zones, vertical topspin reflection, and Smart Return Funnel (85% true angle + 15% center assist).
  - `quest-tennis-trainer/Assets/_Project/XR_Input/RacketHapticsController.cs` — Parametric OpenXR/Quest 3 haptics controller (200 Hz sweet-spot pulse, 75 Hz frame mishit vibration, and 100 Hz serve toss micro-tick).
  - `quest-tennis-trainer/Assets/_Project/TrainingModes/BallMachine/BallMachineController.cs` — Programmable ball machine constrained to Quest 3 Guardian & $\pm 1.2\text{m}$ room-scale reach, with drill presets, net clearance, and landing depth heatmap.
  - `quest-tennis-trainer/Assets/_Project/TrainingModes/ServeTrainer/ServeTossController.cs` — Two-handed serve trainer with velocity-smoothed toss release, apex predictor, 200 ms behind-the-back IMU occlusion compensation, and Deuce/Ad Wide/Body/T service box radar telemetry.
  - `quest-tennis-trainer/Assets/_Project/Tests/TennisPhysicsTests.cs` — NUnit test suite covering 120 Hz invariants, Magnus dip/lift, wall rebound kick, Hermite spline accuracy, Smart Return Funnel, parametric haptics, ball machine clamping, and serve occlusion/service-box telemetry.
  - `quest-tennis-trainer/Assets/_Project/Tests/UnityEngineStubs.cs` — Conditional `#if !UNITY_5_3_OR_NEWER` math/XR shim and `NUnitLite` runner entrypoint for headless `.NET 8` & Docker testing.
  - `quest-tennis-trainer/QuestTennisTrainer.Tests.csproj` — Deterministic `.NET 8` project manifest compiling all C# modules and NUnit tests.
  - `quest-tennis-trainer/Dockerfile` & `quest-tennis-trainer/.dockerignore` — Multi-stage build into Microsoft's hardened distroless `mcr.microsoft.com/dotnet/runtime:8.0-noble-chiseled` image (no shell, no package manager, non-root `$APP_UID`).
  - `quest-tennis-trainer/Assets/_Project/Core/TennisSimulationBootstrap.cs` — 120 Hz physics lock and training mode orchestrator (`BallMachine`, `TennisWall`, `ServePractice`).
  - `quest-tennis-trainer/Assets/_Project/Core/TennisCourtSceneBuilder.cs` — Zero-click procedural ITF court layout generator and full training rig builder.
  - `.github/workflows/quest-tennis-ci.yml` — Path-filtered GitHub Actions CI/CD workflow running `dotnet test` + GCP Workload Identity Federation for Quest 3 APK storage.

## 5. Remaining Build Roadmap (Next Sessions)
- **[x] Session 1 — Repo Foundation & Physics Constants** (Done)
- **[x] Session 2 — Core Ball Aerodynamics & Swept Racket Physics** (Done)
- **[x] Session 3 — Tennis Wall & Haptics** (Done)
- **[x] Session 4 — Programmable Ball Machine & Serve Practice** (Done)
- **[x] Session 5 — NUnit Physics Tests & GitHub Actions + GCP CI/CD** (Done)
- **[x] Session 6 — Hardened Distroless Docker Sandbox & `.NET 8` Test Runner** (Done)
- **[x] Session 7 — Zero-Click 3D Scene Bootstrapper (`TennisSimulationBootstrap.cs` & `TennisCourtSceneBuilder.cs`)** (Done)
- **[ ] Session 8 — Unity 6 Package Manifest (`Packages/manifest.json`) & Sandbox CLI Script (`scripts/sandbox.sh`)**

## 6. Prompt to Paste Directly into Antigravity

> We are building `quest-tennis-trainer` inside `/Users/krishnakasinadhuni/projects/hello-world/quest-tennis-trainer` on the `feat/quest-tennis-trainer` branch.
> First, read `quest-tennis-trainer/README.md`, `quest-tennis-trainer/ANTIGRAVITY_HANDOFF.md`, and `quest-tennis-trainer/Assets/_Project/Core/TennisPhysicsConfig.cs`.
> To conserve AI Pro credits, only work on **one session at a time (2–3 files max)** and wait for my confirmation before moving to the next session. Let's start **Session 5** now by implementing the NUnit physics tests under `quest-tennis-trainer/Assets/_Project/Tests/` and `.github/workflows/quest-tennis-ci.yml`.
