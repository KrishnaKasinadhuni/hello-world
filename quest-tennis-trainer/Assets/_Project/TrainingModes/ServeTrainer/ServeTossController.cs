using System;
using System.Collections.Generic;
using UnityEngine;
using QuestTennisTrainer.Core;
using QuestTennisTrainer.Physics;
using QuestTennisTrainer.XR_Input;

namespace QuestTennisTrainer.TrainingModes.ServeTrainer
{
    /// <summary>
    /// Baseline court side for stationary room-scale serve practice.
    /// </summary>
    public enum ServeCourtSide
    {
        Deuce, // Right of center mark, serving diagonally into opponent's left (Deuce) service box
        Ad     // Left of center mark, serving diagonally into opponent's right (Ad) service box
    }

    /// <summary>
    /// Target zones inside the opponent's diagonal service box.
    /// </summary>
    public enum ServeTargetCone
    {
        Wide,
        Body,
        T
    }

    /// <summary>
    /// Biomechanical classification of the executed serve based on outgoing spin RPM and axis.
    /// </summary>
    public enum ServeSpinClassification
    {
        Flat,
        Slice,
        Kick
    }

    /// <summary>
    /// Complete radar, spin, and service-box fault telemetry for an executed serve.
    /// </summary>
    [Serializable]
    public struct ServeTelemetryResult
    {
        public ServeCourtSide CourtSide;
        public ServeSpinClassification ServeType;
        public float ServeSpeedKmh;
        public float SpinRpm;
        public Vector3 SpinAxis;
        public float TossPeakHeightMeters;
        public float ContactHeightMeters;
        public bool UsedOcclusionCompensation;
        public bool IsNetFault;
        public bool IsFootFault;
        public bool IsInServiceBox;
        public bool HitTargetCone;
        public ServeTargetCone TargetCone;
        public Vector2 LandingCourtCoords;
    }

    /// <summary>
    /// Two-handed Serve Practice controller for Meta Quest 3.
    /// - Non-dominant hand: Ball toss release timing, exponential velocity smoothing filter, and apex indicator.
    /// - Dominant hand: Trophy pose, behind-the-back IMU occlusion compensation (100-200 ms), and pronation/brush tracking.
    /// - Court telemetry: Stationary Deuce/Ad markers, Wide/Body/T target cones, fault detector, and speed/spin radar.
    /// </summary>
    public class ServeTossController : MonoBehaviour
    {
        // --- Serve & Tracking Constants ---
        public const float MaxOcclusionCompensationSeconds = 0.20f; // 200 ms max IMU dead-reckoning window
        public const int VelocitySmoothingWindowSamples = 6;        // 50 ms smoothing window at 120 Hz
        public const float ServiceBoxWidthMeters = TennisPhysicsConfig.SinglesCourtWidthMeters * 0.5f; // 4.115m

        [Header("Core References")]
        [SerializeField] private TennisPhysicsConfig config = TennisPhysicsConfig.CreateDefault();
        [SerializeField] private BallAerodynamics serveBall;
        [SerializeField] private SweptRacketCollider racketCollider;
        [SerializeField] private RacketHapticsController hapticsController;

        [Header("Two-Handed Transforms")]
        [SerializeField] private Transform headsetCameraTransform;
        [SerializeField] private Transform nonDominantTossHandTransform;
        [SerializeField] private Transform dominantRacketHandTransform;

        [Header("Stationary Baseline & Target Configuration")]
        [SerializeField] private ServeCourtSide activeCourtSide = ServeCourtSide.Deuce;
        [SerializeField] private ServeTargetCone activeTargetCone = ServeTargetCone.T;
        [SerializeField] private float targetConeRadiusMeters = 0.65f;

        // --- Toss Velocity Smoothing Buffer ---
        private readonly Queue<Vector3> tossVelocitySamples = new Queue<Vector3>(VelocitySmoothingWindowSamples);
        private Vector3 prevTossHandPos;
        private bool isHoldingBallForToss;
        private float lastTossPeakHeightMeters;
        private Vector3 predictedTossApexPosition;

        // --- Behind-the-Back IMU Occlusion Compensation State ---
        private Vector3 lastTrackedRacketPos;
        private Quaternion lastTrackedRacketRot = Quaternion.identity;
        private Vector3 lastTrackedRacketVel;
        private Vector3 lastTrackedRacketAngVel;
        private float currentOcclusionDurationSeconds;
        private bool wasOccludedDuringSwing;

        // --- Active Serve Flight State ---
        private ImpactData lastServeImpact;
        private bool isServeInFlight;

        // --- Events ---
        public event Action<Vector3, Vector3, float> OnBallTossed; // releasePos, smoothedVel, apexHeight
        public event Action<ServeTelemetryResult> OnServeEvaluated;

        // --- Public Properties ---
        public ServeCourtSide ActiveCourtSide
        {
            get => activeCourtSide;
            set => activeCourtSide = value;
        }

        public ServeTargetCone ActiveTargetCone
        {
            get => activeTargetCone;
            set => activeTargetCone = value;
        }

        public Vector3 PredictedTossApexPosition => predictedTossApexPosition;
        public float LastTossPeakHeightMeters => lastTossPeakHeightMeters;
        public bool IsHoldingBallForToss => isHoldingBallForToss;

        private void Awake()
        {
            if (config.DragCoefficient <= 0f)
            {
                config = TennisPhysicsConfig.CreateDefault();
            }
        }

        private void OnEnable()
        {
            if (racketCollider != null)
            {
                racketCollider.OnRacketHit += HandleServeRacketStrike;
            }
            if (serveBall != null)
            {
                serveBall.OnCourtBounce += HandleServeCourtBounce;
            }
        }

        private void OnDisable()
        {
            if (racketCollider != null)
            {
                racketCollider.OnRacketHit -= HandleServeRacketStrike;
            }
            if (serveBall != null)
            {
                serveBall.OnCourtBounce -= HandleServeCourtBounce;
            }
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (dt <= 0.0001f) dt = TennisPhysicsConfig.FixedDeltaTime;

            UpdateTossHandTracking(dt);
            UpdateRacketOcclusionCompensation(dt);
        }

        /// <summary>
        /// Begins holding the ball in the non-dominant hand at the stationary Deuce or Ad baseline marker.
        /// </summary>
        public void BeginBallTossHold()
        {
            isHoldingBallForToss = true;
            isServeInFlight = false;
            wasOccludedDuringSwing = false;
            tossVelocitySamples.Clear();

            if (nonDominantTossHandTransform != null)
            {
                prevTossHandPos = nonDominantTossHandTransform.position;
                if (serveBall != null)
                {
                    serveBall.IsSimulating = false;
                    serveBall.transform.position = nonDominantTossHandTransform.position;
                }
            }
        }

        /// <summary>
        /// Releases the ball from the non-dominant hand, applies velocity smoothing, calculates apex height,
        /// and triggers the 100 Hz, 5 ms fingertip haptic micro-tick.
        /// </summary>
        public Vector3 ReleaseBallToss(Vector3 releasePosition, Vector3 rawReleaseVelocity)
        {
            isHoldingBallForToss = false;
            RecordTossVelocitySample(rawReleaseVelocity);

            Vector3 smoothedVel = ComputeSmoothedTossVelocity(tossVelocitySamples);

            // Constrain unintentional horizontal drift so the toss stays reachable in room-scale
            smoothedVel.x = Mathf.Clamp(smoothedVel.x, -1.2f, 1.2f);
            smoothedVel.z = Mathf.Clamp(smoothedVel.z, -1.0f, 1.5f);
            smoothedVel.y = Mathf.Max(smoothedVel.y, 3.2f); // Minimum upward toss speed

            predictedTossApexPosition = CalculateTossApexPosition(releasePosition, smoothedVel);
            lastTossPeakHeightMeters = predictedTossApexPosition.y;

            if (serveBall != null)
            {
                serveBall.transform.position = releasePosition;
                // Toss is released with near-zero spin
                serveBall.Launch(smoothedVel, Vector3.zero);
            }

            if (hapticsController != null)
            {
                hapticsController.TriggerServeTossReleaseTick();
            }

            OnBallTossed?.Invoke(releasePosition, smoothedVel, lastTossPeakHeightMeters);
            return smoothedVel;
        }

        private void UpdateTossHandTracking(float dt)
        {
            if (nonDominantTossHandTransform == null) return;

            Vector3 currPos = nonDominantTossHandTransform.position;
            Vector3 instVel = (currPos - prevTossHandPos) / dt;
            prevTossHandPos = currPos;

            if (isHoldingBallForToss)
            {
                RecordTossVelocitySample(instVel);
                if (serveBall != null)
                {
                    serveBall.transform.position = currPos;
                }
            }
        }

        public void RecordTossVelocitySample(Vector3 velocitySample)
        {
            if (tossVelocitySamples.Count >= VelocitySmoothingWindowSamples)
            {
                tossVelocitySamples.Dequeue();
            }
            tossVelocitySamples.Enqueue(velocitySample);
        }

        /// <summary>
        /// Exponentially weighted moving average filter that removes controller release micro-jitter.
        /// </summary>
        public static Vector3 ComputeSmoothedTossVelocity(IEnumerable<Vector3> samples)
        {
            Vector3 weightedSum = Vector3.zero;
            float totalWeight = 0f;
            float weight = 1f;

            foreach (Vector3 sample in samples)
            {
                weightedSum += sample * weight;
                totalWeight += weight;
                weight *= 1.35f; // Favor most recent pre-release frames
            }

            return totalWeight > 0f ? (weightedSum / totalWeight) : Vector3.up * 4.5f;
        }

        /// <summary>
        /// Calculates the 3D peak-height apex coordinate of the ball toss under standard gravity.
        /// </summary>
        public static Vector3 CalculateTossApexPosition(Vector3 releasePos, Vector3 releaseVel)
        {
            float vy = Mathf.Max(0f, releaseVel.y);
            float timeToApex = vy / TennisPhysicsConfig.GravityMps2;
            float apexHeight = releasePos.y + (vy * vy) / (2f * TennisPhysicsConfig.GravityMps2);

            return new Vector3(
                releasePos.x + releaseVel.x * timeToApex,
                apexHeight,
                releasePos.z + releaseVel.z * timeToApex);
        }

        /// <summary>
        /// Detects when the racket drops behind the player's head/back during the trophy pose and
        /// integrates IMU linear/angular velocity for 100-200 ms to prevent inside-out camera tracking loss.
        /// </summary>
        private void UpdateRacketOcclusionCompensation(float dt)
        {
            if (dominantRacketHandTransform == null || headsetCameraTransform == null) return;

            Vector3 hmdPos = headsetCameraTransform.position;
            Vector3 hmdForward = headsetCameraTransform.forward;
            Vector3 racketPos = dominantRacketHandTransform.position;

            bool isBehindHead = IsRacketInOverheadOcclusionZone(hmdPos, hmdForward, racketPos);

            if (!isBehindHead)
            {
                // Controller is in inside-out camera FOV; update ground-truth state
                lastTrackedRacketVel = (racketPos - lastTrackedRacketPos) / dt;
                Quaternion deltaRot = dominantRacketHandTransform.rotation * Quaternion.Inverse(lastTrackedRacketRot);
                deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
                if (angleDeg > 180f) angleDeg -= 360f;
                lastTrackedRacketAngVel = axis.normalized * (angleDeg * Mathf.Deg2Rad / dt);

                lastTrackedRacketPos = racketPos;
                lastTrackedRacketRot = dominantRacketHandTransform.rotation;
                currentOcclusionDurationSeconds = 0f;
                return;
            }

            // Controller is behind the head/back in the trophy drop window (compensate up to 200 ms)
            currentOcclusionDurationSeconds += dt;
            if (currentOcclusionDurationSeconds <= MaxOcclusionCompensationSeconds)
            {
                wasOccludedDuringSwing = true;
                PredictOccludedRacketPose(
                    lastTrackedRacketPos,
                    lastTrackedRacketRot,
                    lastTrackedRacketVel,
                    lastTrackedRacketAngVel,
                    currentOcclusionDurationSeconds,
                    out Vector3 compensatedPos,
                    out Quaternion compensatedRot);

                dominantRacketHandTransform.SetPositionAndRotation(compensatedPos, compensatedRot);
            }
        }

        /// <summary>
        /// Returns true when the racket drops behind the headset's camera plane during the trophy/racket-drop phase.
        /// </summary>
        public static bool IsRacketInOverheadOcclusionZone(Vector3 hmdPosition, Vector3 hmdForward, Vector3 racketPosition)
        {
            Vector3 toRacket = racketPosition - hmdPosition;
            Vector3 flatForward = new Vector3(hmdForward.x, 0f, hmdForward.z).normalized;
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;

            float forwardDot = Vector3.Dot(toRacket, flatForward);
            // Behind the ear plane (forwardDot < -0.10m) and near/above shoulder height
            return forwardDot < -0.10f && toRacket.y > -0.35f;
        }

        /// <summary>
        /// Integrates IMU velocity and angular velocity for up to 200 ms during behind-the-back racket drop occlusion.
        /// </summary>
        public static void PredictOccludedRacketPose(
            Vector3 entryPosition,
            Quaternion entryRotation,
            Vector3 entryLinearVelocity,
            Vector3 entryAngularVelocity,
            float occlusionTimeSeconds,
            out Vector3 predictedPosition,
            out Quaternion predictedRotation)
        {
            float clampedTime = Mathf.Clamp(occlusionTimeSeconds, 0f, MaxOcclusionCompensationSeconds);

            // Apply gentle damping over the 200 ms window so dead-reckoning never overshoots wildly
            float damping = Mathf.Lerp(1f, 0.85f, clampedTime / MaxOcclusionCompensationSeconds);
            predictedPosition = entryPosition + (entryLinearVelocity * damping * clampedTime);

            float angleDeg = entryAngularVelocity.magnitude * Mathf.Rad2Deg * clampedTime * damping;
            Vector3 axis = entryAngularVelocity.sqrMagnitude > 0.0001f ? entryAngularVelocity.normalized : Vector3.right;
            predictedRotation = Quaternion.AngleAxis(angleDeg, axis) * entryRotation;
        }

        private void HandleServeRacketStrike(ImpactData impact)
        {
            lastServeImpact = impact;
            isServeInFlight = true;
        }

        private void HandleServeCourtBounce(Vector3 bouncePos, Vector3 normal, CourtSurfaceType surface)
        {
            if (!isServeInFlight) return;
            isServeInFlight = false;

            ServeTelemetryResult result = EvaluateServeLanding(
                bouncePos,
                lastServeImpact,
                activeCourtSide,
                activeTargetCone,
                lastTossPeakHeightMeters,
                wasOccludedDuringSwing,
                targetConeRadiusMeters);

            OnServeEvaluated?.Invoke(result);
        }

        /// <summary>
        /// Evaluates serve landing coordinates against regulation ITF Deuce/Ad service boxes and Wide/Body/T target cones.
        /// </summary>
        public static ServeTelemetryResult EvaluateServeLanding(
            Vector3 landingWorldPos,
            ImpactData impact,
            ServeCourtSide courtSide,
            ServeTargetCone targetCone,
            float tossPeakHeightMeters,
            bool usedOcclusionCompensation,
            float coneRadiusMeters = 0.65f)
        {
            float netZ = TennisPhysicsConfig.BaselineToNetMeters;                   // 11.887m
            float serviceLineZ = netZ + TennisPhysicsConfig.ServiceLineToNetMeters; // 18.288m

            bool isNetFault = landingWorldPos.z < netZ;

            // Deuce serve originates on right (+X) and must land in opponent's left (-X) box: [-4.115m, 0m]
            // Ad serve originates on left (-X) and must land in opponent's right (+X) box: [0m, +4.115m]
            float minX = (courtSide == ServeCourtSide.Deuce) ? -ServiceBoxWidthMeters : 0f;
            float maxX = (courtSide == ServeCourtSide.Deuce) ? 0f : ServiceBoxWidthMeters;

            bool isInServiceBox = !isNetFault &&
                                  landingWorldPos.z >= netZ &&
                                  landingWorldPos.z <= serviceLineZ &&
                                  landingWorldPos.x >= minX &&
                                  landingWorldPos.x <= maxX;

            Vector2 landing2D = new Vector2(landingWorldPos.x, landingWorldPos.z);
            Vector2 conePos2D = GetTargetConeCourtPosition(courtSide, targetCone);
            bool hitCone = isInServiceBox && Vector2.Distance(landing2D, conePos2D) <= coneRadiusMeters;

            ServeSpinClassification serveType = ClassifyServeSpin(impact.OutgoingSpinRpm, impact.SpinAxis);

            return new ServeTelemetryResult
            {
                CourtSide = courtSide,
                ServeType = serveType,
                ServeSpeedKmh = impact.OutgoingSpeedKmh,
                SpinRpm = impact.OutgoingSpinRpm,
                SpinAxis = impact.SpinAxis,
                TossPeakHeightMeters = tossPeakHeightMeters,
                ContactHeightMeters = impact.ImpactPoint.y,
                UsedOcclusionCompensation = usedOcclusionCompensation,
                IsNetFault = isNetFault,
                IsFootFault = false, // Room-scale baseline marker prevents stepping over baseline
                IsInServiceBox = isInServiceBox,
                HitTargetCone = hitCone,
                TargetCone = targetCone,
                LandingCourtCoords = landing2D
            };
        }

        /// <summary>
        /// Classifies a serve as Flat, Slice, or Kick based on total spin RPM and spin axis tilt.
        /// </summary>
        public static ServeSpinClassification ClassifyServeSpin(float spinRpm, Vector3 spinAxis)
        {
            if (spinRpm < 1200f)
            {
                return ServeSpinClassification.Flat;
            }

            Vector3 axis = spinAxis.normalized;
            // High horizontal X component indicates strong topspin+sidespin brush (Kick serve)
            if (Mathf.Abs(axis.x) >= Mathf.Abs(axis.y))
            {
                return ServeSpinClassification.Kick;
            }

            return ServeSpinClassification.Slice;
        }

        /// <summary>
        /// Returns the regulation court coordinate (X, Z) for Wide, Body, or T target cones.
        /// </summary>
        public static Vector2 GetTargetConeCourtPosition(ServeCourtSide side, ServeTargetCone cone)
        {
            float netZ = TennisPhysicsConfig.BaselineToNetMeters;
            float deepServiceZ = netZ + TennisPhysicsConfig.ServiceLineToNetMeters - 0.65f; // 0.65m inside service line
            float sign = (side == ServeCourtSide.Deuce) ? -1f : 1f;

            switch (cone)
            {
                case ServeTargetCone.Wide:
                    return new Vector2(sign * (ServiceBoxWidthMeters - 0.55f), deepServiceZ - 0.8f);
                case ServeTargetCone.Body:
                    return new Vector2(sign * (ServiceBoxWidthMeters * 0.5f), deepServiceZ);
                case ServeTargetCone.T:
                default:
                    return new Vector2(sign * 0.45f, deepServiceZ);
            }
        }

        /// <summary>
        /// Returns the stationary baseline home marker world position for Deuce (+1.0m X) or Ad (-1.0m X).
        /// </summary>
        public static Vector3 GetStationaryBaselineMarkerPosition(ServeCourtSide side)
        {
            float xOffset = (side == ServeCourtSide.Deuce) ? 1.0f : -1.0f;
            return new Vector3(xOffset, 0f, 0f);
        }
    }
}

