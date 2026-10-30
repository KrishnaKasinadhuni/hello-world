using System;
using System.Collections.Generic;
using UnityEngine;
using QuestTennisTrainer.Core;
using QuestTennisTrainer.Physics;

namespace QuestTennisTrainer.TrainingModes.BallMachine
{
    /// <summary>
    /// Built-in programmable drill sequences constrained to the player's room-scale strike zone.
    /// </summary>
    public enum BallMachineDrillPreset
    {
        CenterGroove,
        ForehandBackhandAlternating,
        HeavyTopspinDeep,
        LowSliceApproach,
        RandomRoomScaleOscillation
    }

    /// <summary>
    /// Configuration for an individual ball feed from the machine.
    /// </summary>
    [Serializable]
    public struct BallFeedParameters
    {
        public float FeedIntervalSeconds; // 1.5 - 6.0s
        public float LaunchSpeedKmh;      // 30 - 130 km/h
        public float ElevationAngleDeg;   // 4 - 28 degrees
        public float TopspinRpm;          // -2000 (backspin) to +3500 (topspin) RPM
        public float SidespinRpm;
        public float TargetLateralOffsetMeters; // Clamped to +/- 1.2m room-scale reach
    }

    /// <summary>
    /// Telemetry record for return net clearance, court landing depth heatmap, and target zone streaks.
    /// </summary>
    [Serializable]
    public struct ReturnShotTelemetry
    {
        public Vector2 LandingCourtPosition; // X = lateral, Z = depth from net
        public float NetClearanceMeters;
        public bool ClearedNet;
        public bool LandedInBounds;
        public bool HitTargetZone;
        public int CurrentTargetStreak;
    }

    /// <summary>
    /// Programmable Ball Machine training system for Meta Quest 3.
    /// Constrains lateral feeds to the smaller of the player's Quest 3 Guardian boundary or +/- 1.2m (1-2 step radius),
    /// ensuring 100% physical footwork (split-step + unit turn) with zero artificial thumbstick locomotion.
    /// </summary>
    public class BallMachineController : MonoBehaviour
    {
        // --- Operational Bounds (from Handoff Spec) ---
        public const float MinFeedIntervalSeconds = 1.5f;
        public const float MaxFeedIntervalSeconds = 6.0f;
        public const float MinLaunchSpeedKmh = 30f;
        public const float MaxLaunchSpeedKmh = 130f;
        public const float MinSpinRpm = -2000f;
        public const float MaxSpinRpm = 3500f;

        [Header("Configuration")]
        [SerializeField] private TennisPhysicsConfig config = TennisPhysicsConfig.CreateDefault();
        [SerializeField] private BallAerodynamics activeBall;
        [SerializeField] private Transform playerHomeCenter;
        [SerializeField] private Transform machineMuzzleTransform;

        [Header("Feed Settings")]
        [SerializeField] private BallMachineDrillPreset activeDrill = BallMachineDrillPreset.ForehandBackhandAlternating;
        [SerializeField] private float feedIntervalSeconds = 3.0f;
        [SerializeField] private float launchSpeedKmh = 78f;
        [SerializeField] private float elevationAngleDeg = 12f;
        [SerializeField] private float topspinRpm = 1400f;
        [SerializeField] private bool isFeeding = false;

        [Header("Guardian & Strike Zone Constraints")]
        [SerializeField] private float calibratedGuardianWidthMeters = 2.4f;
        [SerializeField] private Rect returnTargetCourtZone = new Rect(-3.5f, 7.0f, 3.0f, 4.2f);

        // --- Runtime State & Heatmap Telemetry ---
        private float feedTimer;
        private int feedSequenceIndex;
        private int currentTargetStreak;
        private int bestTargetStreak;
        private float lastReturnNetClearanceMeters;
        private readonly List<Vector2> landingDepthHeatmap = new List<Vector2>(128);

        // --- Events ---
        public event Action<BallFeedParameters, Vector3> OnBallFed;
        public event Action<ReturnShotTelemetry> OnReturnShotEvaluated;

        // --- Public Properties ---
        public bool IsFeeding
        {
            get => isFeeding;
            set => isFeeding = value;
        }

        public int CurrentTargetStreak => currentTargetStreak;
        public int BestTargetStreak => bestTargetStreak;
        public float LastReturnNetClearanceMeters => lastReturnNetClearanceMeters;
        public IReadOnlyList<Vector2> LandingDepthHeatmap => landingDepthHeatmap;

        /// <summary>
        /// Effective maximum lateral offset (meters) constrained by both room-scale config (+/- 1.2m)
        /// and the Quest 3 Guardian play area width.
        /// </summary>
        public float EffectiveMaxLateralReachMeters =>
            ComputeConstrainedLateralReach(config.MaxLateralReachMeters, calibratedGuardianWidthMeters);

        private void Awake()
        {
            if (config.DragCoefficient <= 0f)
            {
                config = TennisPhysicsConfig.CreateDefault();
            }
        }

        private void FixedUpdate()
        {
            if (!isFeeding || activeBall == null) return;

            feedTimer += Time.fixedDeltaTime;
            float clampedInterval = Mathf.Clamp(feedIntervalSeconds, MinFeedIntervalSeconds, MaxFeedIntervalSeconds);

            if (feedTimer >= clampedInterval)
            {
                feedTimer = 0f;
                FeedNextBall();
            }
        }

        /// <summary>
        /// Updates the available lateral strike zone from the Quest 3 Guardian boundary width.
        /// </summary>
        public void SetGuardianPlayWidth(float guardianWidthMeters)
        {
            calibratedGuardianWidthMeters = Mathf.Max(0.8f, guardianWidthMeters);
        }

        /// <summary>
        /// Pure calculation of the safe lateral feed reach (+/- meters from home center marker).
        /// Leaves a 0.25m safety buffer inside the Guardian boundary edge.
        /// </summary>
        public static float ComputeConstrainedLateralReach(float configMaxReach, float guardianWidthMeters)
        {
            float safeGuardianHalfWidth = Mathf.Max(0.3f, (guardianWidthMeters * 0.5f) - 0.25f);
            return Mathf.Min(configMaxReach, safeGuardianHalfWidth);
        }

        /// <summary>
        /// Executes the next ball feed according to the active drill preset and room-scale constraints.
        /// </summary>
        public BallFeedParameters FeedNextBall()
        {
            float maxReach = EffectiveMaxLateralReachMeters;
            BallFeedParameters feedParams = BuildDrillFeedParameters(
                activeDrill,
                feedSequenceIndex,
                maxReach,
                feedIntervalSeconds,
                launchSpeedKmh,
                elevationAngleDeg,
                topspinRpm);

            feedSequenceIndex++;

            Vector3 muzzlePos = machineMuzzleTransform != null
                ? machineMuzzleTransform.position
                : new Vector3(0f, 1.05f, TennisPhysicsConfig.BaselineToNetMeters * 2f);

            Vector3 homePos = playerHomeCenter != null ? playerHomeCenter.position : Vector3.zero;
            Vector3 strikeZoneTarget = new Vector3(
                homePos.x + feedParams.TargetLateralOffsetMeters,
                0.95f,
                homePos.z + 1.2f);

            Vector3 launchDir = ComputeLaunchDirection(muzzlePos, strikeZoneTarget, feedParams.ElevationAngleDeg);

            if (activeBall != null)
            {
                activeBall.transform.position = muzzlePos;
                activeBall.LaunchWithTennisParameters(
                    feedParams.LaunchSpeedKmh,
                    launchDir,
                    feedParams.TopspinRpm,
                    feedParams.SidespinRpm);
            }

            OnBallFed?.Invoke(feedParams, launchDir);
            return feedParams;
        }

        /// <summary>
        /// Generates clamped feed parameters for a given drill preset and sequence step.
        /// </summary>
        public static BallFeedParameters BuildDrillFeedParameters(
            BallMachineDrillPreset preset,
            int stepIndex,
            float maxLateralReachMeters,
            float baseIntervalSeconds = 3.0f,
            float baseSpeedKmh = 78f,
            float baseElevationDeg = 12f,
            float baseTopspinRpm = 1400f)
        {
            float interval = Mathf.Clamp(baseIntervalSeconds, MinFeedIntervalSeconds, MaxFeedIntervalSeconds);
            float speed = Mathf.Clamp(baseSpeedKmh, MinLaunchSpeedKmh, MaxLaunchSpeedKmh);
            float elevation = Mathf.Clamp(baseElevationDeg, 4f, 28f);
            float spin = Mathf.Clamp(baseTopspinRpm, MinSpinRpm, MaxSpinRpm);
            float lateralOffset = 0f;

            switch (preset)
            {
                case BallMachineDrillPreset.ForehandBackhandAlternating:
                    // Alternates between +maxReach (Forehand) and -maxReach (Backhand)
                    lateralOffset = (stepIndex % 2 == 0) ? maxLateralReachMeters : -maxLateralReachMeters;
                    break;

                case BallMachineDrillPreset.HeavyTopspinDeep:
                    speed = Mathf.Clamp(baseSpeedKmh * 1.1f, MinLaunchSpeedKmh, MaxLaunchSpeedKmh);
                    elevation = 16.5f;
                    spin = Mathf.Clamp(2800f, MinSpinRpm, MaxSpinRpm);
                    lateralOffset = ((stepIndex % 3) - 1) * (maxLateralReachMeters * 0.65f);
                    break;

                case BallMachineDrillPreset.LowSliceApproach:
                    speed = Mathf.Clamp(baseSpeedKmh * 0.92f, MinLaunchSpeedKmh, MaxLaunchSpeedKmh);
                    elevation = 7.5f;
                    spin = Mathf.Clamp(-1600f, MinSpinRpm, MaxSpinRpm);
                    lateralOffset = (stepIndex % 2 == 0) ? (maxLateralReachMeters * 0.8f) : -(maxLateralReachMeters * 0.8f);
                    break;

                case BallMachineDrillPreset.RandomRoomScaleOscillation:
                    // Deterministic pseudo-random spread across [-maxReach, +maxReach]
                    float normalizedHash = Mathf.Sin(stepIndex * 12.9898f) * 43758.5453f;
                    normalizedHash = normalizedHash - Mathf.Floor(normalizedHash); // [0, 1]
                    lateralOffset = Mathf.Lerp(-maxLateralReachMeters, maxLateralReachMeters, normalizedHash);
                    break;

                case BallMachineDrillPreset.CenterGroove:
                default:
                    lateralOffset = 0f;
                    break;
            }

            // Guarantee strict clamping within the player's room-scale strike zone
            lateralOffset = Mathf.Clamp(lateralOffset, -maxLateralReachMeters, maxLateralReachMeters);

            return new BallFeedParameters
            {
                FeedIntervalSeconds = interval,
                LaunchSpeedKmh = speed,
                ElevationAngleDeg = elevation,
                TopspinRpm = spin,
                SidespinRpm = 0f,
                TargetLateralOffsetMeters = lateralOffset
            };
        }

        /// <summary>
        /// Computes the normalized 3D launch direction from the ball machine muzzle toward the player's strike zone
        /// at the desired elevation angle.
        /// </summary>
        public static Vector3 ComputeLaunchDirection(Vector3 muzzlePos, Vector3 targetStrikePos, float elevationAngleDeg)
        {
            Vector3 horizontalDelta = new Vector3(
                targetStrikePos.x - muzzlePos.x,
                0f,
                targetStrikePos.z - muzzlePos.z);

            if (horizontalDelta.sqrMagnitude < 0.001f)
            {
                horizontalDelta = Vector3.back;
            }

            Vector3 flatDir = horizontalDelta.normalized;
            float elevRad = elevationAngleDeg * Mathf.Deg2Rad;
            Vector3 launchDir = (flatDir * Mathf.Cos(elevRad)) + (Vector3.up * Mathf.Sin(elevRad));
            return launchDir.normalized;
        }

        /// <summary>
        /// Evaluates a player's return shot for net clearance, court landing depth heatmap, and target zone streak.
        /// </summary>
        public ReturnShotTelemetry RecordPlayerReturnLanding(Vector3 landingWorldPos, float netPassHeightMeters)
        {
            float netClearance = netPassHeightMeters - TennisPhysicsConfig.NetCenterHeightMeters;
            bool clearedNet = netClearance >= 0f;

            float halfCourtWidth = TennisPhysicsConfig.SinglesCourtWidthMeters * 0.5f;
            float courtLength = TennisPhysicsConfig.BaselineToNetMeters * 2f;

            // Player hits from z = 0 across net at z = 11.887m into opponent court [11.887m, 23.774m]
            float depthFromNet = landingWorldPos.z - TennisPhysicsConfig.BaselineToNetMeters;
            bool landedInBounds = clearedNet &&
                                  Mathf.Abs(landingWorldPos.x) <= halfCourtWidth &&
                                  landingWorldPos.z >= TennisPhysicsConfig.BaselineToNetMeters &&
                                  landingWorldPos.z <= courtLength;

            Vector2 courtCoord = new Vector2(landingWorldPos.x, depthFromNet);
            if (clearedNet)
            {
                landingDepthHeatmap.Add(courtCoord);
            }

            bool hitTarget = landedInBounds && returnTargetCourtZone.Contains(courtCoord);
            if (hitTarget)
            {
                currentTargetStreak++;
                if (currentTargetStreak > bestTargetStreak)
                {
                    bestTargetStreak = currentTargetStreak;
                }
            }
            else
            {
                currentTargetStreak = 0;
            }

            lastReturnNetClearanceMeters = netClearance;

            ReturnShotTelemetry telemetry = new ReturnShotTelemetry
            {
                LandingCourtPosition = courtCoord,
                NetClearanceMeters = netClearance,
                ClearedNet = clearedNet,
                LandedInBounds = landedInBounds,
                HitTargetZone = hitTarget,
                CurrentTargetStreak = currentTargetStreak
            };

            OnReturnShotEvaluated?.Invoke(telemetry);
            return telemetry;
        }

        public void ClearHeatmapAndStats()
        {
            landingDepthHeatmap.Clear();
            currentTargetStreak = 0;
            bestTargetStreak = 0;
            lastReturnNetClearanceMeters = 0f;
            feedSequenceIndex = 0;
        }
    }
}

