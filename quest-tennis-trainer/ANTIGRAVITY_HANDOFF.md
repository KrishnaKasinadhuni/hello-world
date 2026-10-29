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
- **Local Repo Path**: `/Users/krishnakasinadhuni/Documents/local-projects/hello-world`
- **Subproject Path**: `/Users/krishnakasinadhuni/Documents/local-projects/hello-world/quest-tennis-trainer`
- **Active Git Branch**: `feat/quest-tennis-trainer`
- **Completed & Committed Files (Sessions 1 & 2)**:
  - `quest-tennis-trainer/README.md` — Architecture overview and 5-session roadmap.
  - `quest-tennis-trainer/.gitignore` — Unity 6, C#, and Meta Quest `.apk`/`.aab` ignore rules.
  - `quest-tennis-trainer/Assets/_Project/Core/TennisPhysicsConfig.cs` — Pure C# struct with 120 Hz timing, ITF ball constants, drag/Magnus coefficients, stringbed restitution, and room-scale reach parameters.
  - `quest-tennis-trainer/Assets/_Project/Physics/BallAerodynamics.cs` — Pure C# / Unity aerodynamic solver for gravity, air drag, Magnus spin curve, and court/wall bounce restitution.
  - `quest-tennis-trainer/Assets/_Project/Physics/SweptRacketCollider.cs` — 120 Hz 10-substep Hermite spline collision detector with stringbed sweet-spot vs frame restitution, tangential friction, and topspin/slice/kick spin calculation.

## 5. Remaining Build Roadmap (Next Sessions)
- **[x] Session 1 — Repo Foundation & Physics Constants** (Done)
- **[x] Session 2 — Core Ball Aerodynamics & Swept Racket Physics** (Done)
- **[ ] Session 3 — Tennis Wall & Haptics**:
  - Create `quest-tennis-trainer/Assets/_Project/TrainingModes/WallPractice/TennisWallController.cs` (net-line fault detection, target zones, and Smart Return Funnel).
  - Create `quest-tennis-trainer/Assets/_Project/XR_Input/RacketHapticsController.cs` (sweet-spot vs. frame vibration and serve toss tick).
- **[ ] Session 4 — Programmable Ball Machine & Serve Practice**:
  - Create `quest-tennis-trainer/Assets/_Project/TrainingModes/BallMachine/BallMachineController.cs` (ballistic trajectory solver constrained to $\pm 1.2\text{m}$ room-scale reach + drill presets).
  - Create `quest-tennis-trainer/Assets/_Project/TrainingModes/ServeTrainer/ServeTossController.cs` (two-handed toss release, behind-the-back IMU pose smoothing, service box radar/fault telemetry).
- **[ ] Session 5 — NUnit Physics Tests & GitHub Actions + GCP CI/CD**:
  - Add fast `.NET` NUnit tests under `quest-tennis-trainer/Assets/_Project/Tests/`.
  - Add `.github/workflows/quest-tennis-ci.yml` with path filters (`quest-tennis-trainer/**`) and GCP Workload Identity Federation for APK artifact storage.

## 6. Prompt to Paste Directly into Antigravity

> We are building `quest-tennis-trainer` inside `/Users/krishnakasinadhuni/Documents/local-projects/hello-world/quest-tennis-trainer` on the `feat/quest-tennis-trainer` branch.
> First, read `quest-tennis-trainer/README.md`, `quest-tennis-trainer/ANTIGRAVITY_HANDOFF.md`, and `quest-tennis-trainer/Assets/_Project/Core/TennisPhysicsConfig.cs`.
> To conserve AI Pro credits, only work on **one session at a time (2–3 files max)** and wait for my confirmation before moving to the next session. Let's start **Session 3** now by implementing `TennisWallController.cs` and `RacketHapticsController.cs`.
