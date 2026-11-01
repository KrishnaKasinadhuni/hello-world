using System;
using UnityEngine;
using QuestTennisTrainer.Physics;
using QuestTennisTrainer.TrainingModes.BallMachine;
using QuestTennisTrainer.TrainingModes.WallPractice;
using QuestTennisTrainer.TrainingModes.ServeTrainer;
using QuestTennisTrainer.UI;
using QuestTennisTrainer.XR_Input;

namespace QuestTennisTrainer.Core
{
    /// <summary>
    /// The three core single-player training systems in Quest 3 Tennis Trainer.
    /// </summary>
    public enum TennisTrainingMode
    {
        BallMachine,
        TennisWall,
        ServePractice
    }

    /// <summary>
    /// Central 120 Hz bootstrapper and mode orchestrator for Meta Quest 3.
    /// - Locks Time.fixedDeltaTime to 0.008333s (120 Hz) with 10 sub-steps (~0.83 ms collision resolution).
    /// - Enforces room-scale footwork only (+/- 1.2m around the stationary home center marker, zero thumbstick locomotion).
    /// - Orchestrates switching between Programmable Ball Machine, Tennis Wall (Backboard), and Serve Practice.
    /// </summary>
    public class TennisSimulationBootstrap : MonoBehaviour
    {
        [Header("Core Configuration")]
        [SerializeField] private TennisPhysicsConfig physicsConfig = TennisPhysicsConfig.CreateDefault();
        [SerializeField] private TennisTrainingMode activeMode = TennisTrainingMode.BallMachine;
        [SerializeField] private CourtSurfaceType courtSurface = CourtSurfaceType.HardCourt;

        [Header("System References (Phase 1)")]
        [SerializeField] private BallAerodynamics activeBall;
        [SerializeField] private SweptRacketCollider racketCollider;
        [SerializeField] private RacketHapticsController hapticsController;
        [SerializeField] private BallMachineController ballMachine;
        [SerializeField] private TennisWallController tennisWall;
        [SerializeField] private ServeTossController serveTrainer;
        [SerializeField] private Transform playerHomeCenterMarker;

        [Header("Phase 2 & Phase 3 Subsystems")]
        [SerializeField] private TrainingHudController hudController;
        [SerializeField] private TrajectoryArcRenderer trajectoryArcRenderer;
        [SerializeField] private RoomScaleGuardianVisualizer guardianVisualizer;
        [SerializeField] private SwingCoachAnalyzer swingCoach;
        [SerializeField] private RacketAudioSynthesizer audioSynthesizer;
        [SerializeField] private AdaptiveDrillDirector adaptiveDrillDirector;

        // --- Events ---
        public event Action<TennisTrainingMode> OnTrainingModeChanged;

        // --- Public Properties ---
        public TennisPhysicsConfig PhysicsConfig => physicsConfig;
        public TennisTrainingMode ActiveMode => activeMode;
        public CourtSurfaceType CourtSurface => courtSurface;
        public BallAerodynamics ActiveBall => activeBall;
        public SweptRacketCollider RacketCollider => racketCollider;
        public RacketHapticsController HapticsController => hapticsController;
        public BallMachineController BallMachine => ballMachine;
        public TennisWallController TennisWall => tennisWall;
        public ServeTossController ServeTrainer => serveTrainer;
        public TrainingHudController HudController => hudController;
        public TrajectoryArcRenderer TrajectoryArcRenderer => trajectoryArcRenderer;
        public RoomScaleGuardianVisualizer GuardianVisualizer => guardianVisualizer;
        public SwingCoachAnalyzer SwingCoach => swingCoach;
        public RacketAudioSynthesizer AudioSynthesizer => audioSynthesizer;
        public AdaptiveDrillDirector AdaptiveDirector => adaptiveDrillDirector;

        private void Awake()
        {
            ApplyQuest3TimingAndPhysicsLock();
            SwitchTrainingMode(activeMode);
        }

        /// <summary>
        /// Locks physics simulation to 120 Hz (8.333 ms fixed step) and propagates baseline config to all subsystems.
        /// </summary>
        public void ApplyQuest3TimingAndPhysicsLock()
        {
            if (physicsConfig.DragCoefficient <= 0f)
            {
                physicsConfig = TennisPhysicsConfig.CreateDefault();
            }

            Time.fixedDeltaTime = TennisPhysicsConfig.FixedDeltaTime; // 0.0083333s (120 Hz)

#if UNITY_5_3_OR_NEWER
            Application.targetFrameRate = (int)TennisPhysicsConfig.TargetFrameRateHz;
#endif

            if (activeBall != null)
            {
                activeBall.Config = physicsConfig;
                activeBall.CurrentCourtSurface = courtSurface;
            }

            if (racketCollider != null)
            {
                racketCollider.Config = physicsConfig;
            }
        }

        /// <summary>
        /// Binds all Phase 1, Phase 2, and Phase 3 subsystem references (used by TennisCourtSceneBuilder).
        /// </summary>
        public void BindSubsystems(
            BallAerodynamics ball,
            SweptRacketCollider racket,
            RacketHapticsController haptics,
            BallMachineController machine,
            TennisWallController wall,
            ServeTossController serve,
            Transform homeMarker,
            TrainingHudController hud = null,
            TrajectoryArcRenderer arcRenderer = null,
            RoomScaleGuardianVisualizer guardian = null,
            SwingCoachAnalyzer coach = null,
            RacketAudioSynthesizer audio = null,
            AdaptiveDrillDirector adaptiveDirector = null)
        {
            activeBall = ball;
            racketCollider = racket;
            hapticsController = haptics;
            ballMachine = machine;
            tennisWall = wall;
            serveTrainer = serve;
            playerHomeCenterMarker = homeMarker;
            hudController = hud;
            trajectoryArcRenderer = arcRenderer;
            guardianVisualizer = guardian;
            swingCoach = coach;
            audioSynthesizer = audio;
            adaptiveDrillDirector = adaptiveDirector;

            if (adaptiveDrillDirector != null)
            {
                adaptiveDrillDirector.ResetToTier(AdaptiveDifficultyTier.Tier2_BaselineRally);
            }

            ApplyQuest3TimingAndPhysicsLock();
            SwitchTrainingMode(activeMode);
        }

        /// <summary>
        /// Processes a single racket-ball impact across all integrated Phase 1, Phase 2, and Phase 3 subsystems:
        /// Haptics -> Procedural Audio -> Spatial HUD -> Biomechanical Swing Coach -> Adaptive Drill Director.
        /// </summary>
        public AdaptiveDrillSnapshot ProcessIntegratedRacketStroke(
            ImpactData impact,
            Vector3 racketVelocityWorld,
            bool clearedNet,
            bool landedInBounds)
        {
            if (hapticsController != null)
            {
                hapticsController.HandleRacketHit(impact);
            }

            if (audioSynthesizer != null)
            {
                audioSynthesizer.PlayRacketImpactAudio(impact);
            }

            if (hudController != null)
            {
                hudController.HandleRacketImpact(impact);
            }

            if (swingCoach != null)
            {
                swingCoach.RecordAndAnalyzeStroke(impact, racketVelocityWorld);
            }

            if (adaptiveDrillDirector != null)
            {
                return adaptiveDrillDirector.EvaluatePlayerStroke(
                    clearedNet,
                    landedInBounds,
                    impact.SweetSpotAccuracy,
                    impact.IsFrameHit);
            }

            return default;
        }

        /// <summary>
        /// Switches cleanly between Programmable Ball Machine, Tennis Wall, and Serve Practice,
        /// repositioning the player's stationary room-scale home marker as appropriate.
        /// </summary>
        public void SwitchTrainingMode(TennisTrainingMode newMode)
        {
            activeMode = newMode;

            if (ballMachine != null)
            {
                ballMachine.IsFeeding = (newMode == TennisTrainingMode.BallMachine);
            }

            if (playerHomeCenterMarker != null)
            {
                if (newMode == TennisTrainingMode.ServePractice && serveTrainer != null)
                {
                    playerHomeCenterMarker.position =
                        ServeTossController.GetStationaryBaselineMarkerPosition(serveTrainer.ActiveCourtSide);
                }
                else
                {
                    // Center mark at baseline (0, 0, 0) for Ball Machine and Tennis Wall rallies
                    playerHomeCenterMarker.position = Vector3.zero;
                }
            }

            if (newMode == TennisTrainingMode.TennisWall && tennisWall != null)
            {
                tennisWall.ResetRallyStreak();
                tennisWall.ApplyWallDistance(tennisWall.WallDistanceMeters);
            }

            OnTrainingModeChanged?.Invoke(activeMode);
        }

        /// <summary>
        /// Changes the active court surface (HardCourt, Clay, Grass) and updates bounce restitution.
        /// </summary>
        public void SetCourtSurface(CourtSurfaceType surface)
        {
            courtSurface = surface;
            if (activeBall != null)
            {
                activeBall.CurrentCourtSurface = surface;
            }
        }
    }
}

