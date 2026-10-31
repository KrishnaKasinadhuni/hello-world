using System;
using UnityEngine;
using QuestTennisTrainer.Core;
using QuestTennisTrainer.Physics;
using QuestTennisTrainer.TrainingModes.BallMachine;
using QuestTennisTrainer.TrainingModes.WallPractice;
using QuestTennisTrainer.TrainingModes.ServeTrainer;

namespace QuestTennisTrainer.UI
{
    /// <summary>
    /// Immutable snapshot of courtside radar board and non-dominant wrist HUD readouts.
    /// </summary>
    [Serializable]
    public struct HudTelemetrySnapshot
    {
        public TennisTrainingMode ActiveMode;
        public float ShotSpeedKmh;
        public float SpinRpm;
        public string SpinCategoryLabel;
        public float NetClearanceCentimeters;
        public float SweetSpotPercent;
        public bool IsFrameMishit;
        public int CurrentStreak;
        public int TotalScore;
        public string CourtCallBanner;        // e.g. "IN · T-CONE HIT!", "NET FAULT", "SWEET SPOT!"
        public string FormattedRadarReadout;  // e.g. "185 km/h | 2900 RPM (Kick)"
        public string FormattedWristSummary;  // e.g. "Streak: 4 | Net: +62 cm | Sweet: 95%"
    }

    /// <summary>
    /// Spatial Training HUD & Courtside Serve Radar controller for Meta Quest 3.
    /// Aggregates real-time events from SweptRacketCollider, TennisWallController,
    /// BallMachineController, and ServeTossController into a world-space scoreboard
    /// and a glanceable non-dominant wrist display.
    /// </summary>
    public class TrainingHudController : MonoBehaviour
    {
        [Header("Subsystem References")]
        [SerializeField] private TennisSimulationBootstrap bootstrap;
        [SerializeField] private SweptRacketCollider racketCollider;
        [SerializeField] private TennisWallController tennisWall;
        [SerializeField] private BallMachineController ballMachine;
        [SerializeField] private ServeTossController serveTrainer;

        [Header("Spatial Anchors")]
        [SerializeField] private Transform courtsideRadarBoardTransform;
        [SerializeField] private Transform nonDominantWristHudTransform;

        // --- Runtime Snapshot State ---
        private HudTelemetrySnapshot latestSnapshot;

        // --- Events ---
        public event Action<HudTelemetrySnapshot> OnHudUpdated;

        // --- Public Properties ---
        public HudTelemetrySnapshot LatestSnapshot => latestSnapshot;

        private void OnEnable()
        {
            BindEvents();
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        public void BindSubsystems(
            TennisSimulationBootstrap simBootstrap,
            SweptRacketCollider racket,
            TennisWallController wall,
            BallMachineController machine,
            ServeTossController serve)
        {
            UnbindEvents();
            bootstrap = simBootstrap;
            racketCollider = racket;
            tennisWall = wall;
            ballMachine = machine;
            serveTrainer = serve;
            BindEvents();
        }

        private void BindEvents()
        {
            if (racketCollider != null) racketCollider.OnRacketHit += HandleRacketImpact;
            if (tennisWall != null) tennisWall.OnWallHit += HandleWallImpact;
            if (ballMachine != null) ballMachine.OnReturnShotEvaluated += HandleBallMachineReturn;
            if (serveTrainer != null) serveTrainer.OnServeEvaluated += HandleServeEvaluated;
        }

        private void UnbindEvents()
        {
            if (racketCollider != null) racketCollider.OnRacketHit -= HandleRacketImpact;
            if (tennisWall != null) tennisWall.OnWallHit -= HandleWallImpact;
            if (ballMachine != null) ballMachine.OnReturnShotEvaluated -= HandleBallMachineReturn;
            if (serveTrainer != null) serveTrainer.OnServeEvaluated -= HandleServeEvaluated;
        }

        public void HandleRacketImpact(ImpactData impact)
        {
            TennisTrainingMode mode = bootstrap != null ? bootstrap.ActiveMode : TennisTrainingMode.BallMachine;
            float sweetPct = Mathf.RoundToInt(impact.SweetSpotAccuracy * 100f);
            string spinLabel = ClassifyShotSpinLabel(impact.OutgoingSpinRpm, impact.SpinAxis);
            string callBanner = impact.IsFrameHit ? "FRAME MISHIT (75 Hz)" : "SWEET SPOT (200 Hz)";

            latestSnapshot = BuildSnapshot(
                mode,
                impact.OutgoingSpeedKmh,
                impact.OutgoingSpinRpm,
                spinLabel,
                latestSnapshot.NetClearanceCentimeters,
                sweetPct,
                impact.IsFrameHit,
                latestSnapshot.CurrentStreak,
                latestSnapshot.TotalScore,
                callBanner);

            OnHudUpdated?.Invoke(latestSnapshot);
        }

        public void HandleWallImpact(WallHitResult wallHit)
        {
            float netCm = wallHit.NetClearanceMeters * 100f;
            string callBanner;
            if (wallHit.IsOut)
            {
                callBanner = "OUT · MISSED WALL";
            }
            else if (wallHit.IsNetFault)
            {
                callBanner = "NET FAULT (< 91.4 cm)";
            }
            else if (!string.IsNullOrEmpty(wallHit.HitTargetZoneId))
            {
                callBanner = $"TARGET HIT: {wallHit.HitTargetZoneId}";
            }
            else
            {
                callBanner = "GOOD RALLY REBOUND";
            }

            latestSnapshot = BuildSnapshot(
                TennisTrainingMode.TennisWall,
                wallHit.ReboundVelocity.magnitude * 3.6f,
                latestSnapshot.SpinRpm,
                latestSnapshot.SpinCategoryLabel ?? "Topspin",
                netCm,
                latestSnapshot.SweetSpotPercent,
                latestSnapshot.IsFrameMishit,
                wallHit.CurrentRallyStreak,
                wallHit.TotalScore,
                callBanner);

            OnHudUpdated?.Invoke(latestSnapshot);
        }

        public void HandleBallMachineReturn(ReturnShotTelemetry returnShot)
        {
            float netCm = returnShot.NetClearanceMeters * 100f;
            string callBanner;
            if (!returnShot.ClearedNet)
            {
                callBanner = "NET FAULT";
            }
            else if (!returnShot.LandedInBounds)
            {
                callBanner = "OUT LONG / WIDE";
            }
            else if (returnShot.HitTargetZone)
            {
                callBanner = "IN · TARGET ZONE STREAK!";
            }
            else
            {
                callBanner = "IN BOUNDS";
            }

            latestSnapshot = BuildSnapshot(
                TennisTrainingMode.BallMachine,
                latestSnapshot.ShotSpeedKmh,
                latestSnapshot.SpinRpm,
                latestSnapshot.SpinCategoryLabel ?? "Topspin",
                netCm,
                latestSnapshot.SweetSpotPercent,
                latestSnapshot.IsFrameMishit,
                returnShot.CurrentTargetStreak,
                returnShot.CurrentTargetStreak * 100,
                callBanner);

            OnHudUpdated?.Invoke(latestSnapshot);
        }

        public void HandleServeEvaluated(ServeTelemetryResult serve)
        {
            string callBanner;
            if (serve.IsNetFault)
            {
                callBanner = "SERVE FAULT · NET";
            }
            else if (!serve.IsInServiceBox)
            {
                callBanner = "SERVE FAULT · LONG/WIDE";
            }
            else if (serve.HitTargetCone)
            {
                callBanner = $"ACE! HIT {serve.TargetCone} CONE";
            }
            else
            {
                callBanner = $"GOOD SERVE ({serve.CourtSide})";
            }

            int nextStreak = serve.IsInServiceBox ? (latestSnapshot.CurrentStreak + 1) : 0;
            int pointsAdded = serve.HitTargetCone ? 200 : (serve.IsInServiceBox ? 100 : 0);

            latestSnapshot = BuildSnapshot(
                TennisTrainingMode.ServePractice,
                serve.ServeSpeedKmh,
                serve.SpinRpm,
                serve.ServeType.ToString(),
                latestSnapshot.NetClearanceCentimeters,
                latestSnapshot.SweetSpotPercent,
                latestSnapshot.IsFrameMishit,
                nextStreak,
                latestSnapshot.TotalScore + pointsAdded,
                callBanner);

            OnHudUpdated?.Invoke(latestSnapshot);
        }

        /// <summary>
        /// Pure formatter producing courtside radar and wrist HUD strings.
        /// </summary>
        public static HudTelemetrySnapshot BuildSnapshot(
            TennisTrainingMode mode,
            float speedKmh,
            float spinRpm,
            string spinLabel,
            float netClearanceCm,
            float sweetSpotPct,
            bool isFrameMishit,
            int streak,
            int score,
            string banner)
        {
            int roundedSpeed = Mathf.RoundToInt(speedKmh);
            int roundedSpin = Mathf.RoundToInt(spinRpm);
            int roundedNetCm = Mathf.RoundToInt(netClearanceCm);
            int roundedSweet = Mathf.Clamp(Mathf.RoundToInt(sweetSpotPct), 0, 100);
            string netSign = roundedNetCm >= 0 ? "+" : "";

            string radar = $"{roundedSpeed} km/h | {roundedSpin} RPM ({spinLabel})";
            string wrist = $"Streak: {streak} | Net: {netSign}{roundedNetCm} cm | Sweet: {roundedSweet}%";

            return new HudTelemetrySnapshot
            {
                ActiveMode = mode,
                ShotSpeedKmh = speedKmh,
                SpinRpm = spinRpm,
                SpinCategoryLabel = spinLabel,
                NetClearanceCentimeters = netClearanceCm,
                SweetSpotPercent = roundedSweet,
                IsFrameMishit = isFrameMishit,
                CurrentStreak = streak,
                TotalScore = score,
                CourtCallBanner = banner,
                FormattedRadarReadout = radar,
                FormattedWristSummary = wrist
            };
        }

        public static string ClassifyShotSpinLabel(float spinRpm, Vector3 spinAxis)
        {
            if (spinRpm < 600f) return "Flat";
            if (spinAxis.x >= 0f) return "Topspin";
            return "Slice";
        }
    }
}
