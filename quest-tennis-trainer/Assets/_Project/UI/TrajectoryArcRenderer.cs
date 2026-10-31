using System;
using System.Collections.Generic;
using UnityEngine;
using QuestTennisTrainer.Core;
using QuestTennisTrainer.Physics;

namespace QuestTennisTrainer.UI
{
    /// <summary>
    /// Visual classification for court landing spot markers.
    /// </summary>
    public enum BounceMarkerStyle
    {
        InBounds,
        TargetZoneBonus,
        FaultOrOut
    }

    /// <summary>
    /// Record of a court bounce marker rendered in 3D space.
    /// </summary>
    [Serializable]
    public struct CourtBounceMarker
    {
        public Vector3 WorldPosition;
        public BounceMarkerStyle Style;
        public float SpeedAtImpactKmh;
    }

    /// <summary>
    /// Renders the 3D ball trajectory ribbon, forward predictive flight arc (with Magnus spin curve),
    /// Serve Toss Apex target ring, and color-coded court bounce markers.
    /// </summary>
    public class TrajectoryArcRenderer : MonoBehaviour
    {
        public const int DefaultMaxTrailSamples = 90;  // 0.75 seconds of 120 Hz flight history
        public const int DefaultPredictionSteps = 60;  // 0.50 seconds of forward Magnus prediction

        [Header("References")]
        [SerializeField] private BallAerodynamics trackedBall;
        [SerializeField] private Transform tossApexRingTransform;

        [Header("Trail Settings")]
        [SerializeField] private int maxTrailSamples = DefaultMaxTrailSamples;
        [SerializeField] private int maxBounceMarkers = 24;

        private readonly List<Vector3> liveTrailPoints = new List<Vector3>(DefaultMaxTrailSamples);
        private readonly List<CourtBounceMarker> bounceMarkers = new List<CourtBounceMarker>(24);
        private Vector3 currentTossApexCenter;
        private bool isTossApexVisible;

        // --- Public Properties ---
        public IReadOnlyList<Vector3> LiveTrailPoints => liveTrailPoints;
        public IReadOnlyList<CourtBounceMarker> BounceMarkers => bounceMarkers;
        public Vector3 CurrentTossApexCenter => currentTossApexCenter;
        public bool IsTossApexVisible => isTossApexVisible;

        private void FixedUpdate()
        {
            if (trackedBall == null || !trackedBall.IsSimulating) return;
            RecordTrailSample(trackedBall.transform.position);
        }

        /// <summary>
        /// Appends a 120 Hz position breadcrumb to the bounded flight trail ribbon.
        /// </summary>
        public void RecordTrailSample(Vector3 worldPos)
        {
            if (liveTrailPoints.Count >= Mathf.Max(8, maxTrailSamples))
            {
                liveTrailPoints.RemoveAt(0);
            }
            liveTrailPoints.Add(worldPos);
        }

        public void ClearTrail()
        {
            liveTrailPoints.Clear();
        }

        /// <summary>
        /// Displays the 3D floating target ring at the predicted peak of the player's serve toss.
        /// </summary>
        public void ShowTossApexIndicator(Vector3 apexWorldPosition)
        {
            currentTossApexCenter = apexWorldPosition;
            isTossApexVisible = true;
            if (tossApexRingTransform != null)
            {
                tossApexRingTransform.SetPositionAndRotation(apexWorldPosition, Quaternion.identity);
            }
        }

        public void HideTossApexIndicator()
        {
            isTossApexVisible = false;
        }

        /// <summary>
        /// Places a persistent court bounce marker dot for visual depth/placement feedback.
        /// </summary>
        public CourtBounceMarker AddBounceMarker(Vector3 worldPos, BounceMarkerStyle style, float speedKmh)
        {
            if (bounceMarkers.Count >= Mathf.Max(1, maxBounceMarkers))
            {
                bounceMarkers.RemoveAt(0);
            }

            CourtBounceMarker marker = new CourtBounceMarker
            {
                WorldPosition = new Vector3(worldPos.x, 0.005f, worldPos.z),
                Style = style,
                SpeedAtImpactKmh = speedKmh
            };

            bounceMarkers.Add(marker);
            return marker;
        }

        /// <summary>
        /// Computes a forward-predicted 3D trajectory arc using the exact 120 Hz BallAerodynamics
        /// gravity + drag + Magnus solver without mutating the live ball state.
        /// </summary>
        public static List<Vector3> PredictFlightArc(
            Vector3 startPosition,
            Vector3 startVelocity,
            Vector3 startAngularVelocity,
            TennisPhysicsConfig config,
            int steps = DefaultPredictionSteps,
            float dt = TennisPhysicsConfig.FixedDeltaTime)
        {
            int clampedSteps = Mathf.Clamp(steps, 2, 360);
            List<Vector3> points = new List<Vector3>(clampedSteps + 1) { startPosition };

            Vector3 pos = startPosition;
            Vector3 vel = startVelocity;
            Vector3 angVel = startAngularVelocity;

            for (int i = 0; i < clampedSteps; i++)
            {
                BallAerodynamics.StepSimulation(ref pos, ref vel, ref angVel, config, dt);

                if (pos.y <= TennisPhysicsConfig.BallRadiusMeters && vel.y < 0f)
                {
                    pos.y = TennisPhysicsConfig.BallRadiusMeters;
                    points.Add(pos);
                    break;
                }

                points.Add(pos);
            }

            return points;
        }
    }
}

