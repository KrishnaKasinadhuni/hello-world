using System;
using System.Collections.Generic;
using UnityEngine;
using QuestTennisTrainer.Core;

namespace QuestTennisTrainer.XR_Input
{
    /// <summary>
    /// Meta Quest 3 environment blending modes for room-scale safety.
    /// </summary>
    public enum QuestEnvironmentBlendMode
    {
        FullVirtualStadium,
        MixedRealityBaselineWindow, // Real room visible around player's feet; virtual court ahead of baseline
        FullPassthroughBackboard    // Full MR passthrough for living-room wall rallies
    }

    /// <summary>
    /// Real-time spatial safety state relative to the player's +/- 1.2m room-scale strike zone.
    /// </summary>
    public enum RoomScaleSafetyState
    {
        SafeCenter,
        CautionNearEdge,
        BoundaryBreached
    }

    /// <summary>
    /// Telemetry snapshot for the room-scale footwork safety ring and dynamic MR passthrough fade.
    /// </summary>
    [Serializable]
    public struct RoomScaleSafetyStatus
    {
        public RoomScaleSafetyState SafetyState;
        public float LateralDistanceFromHomeMeters;
        public float DepthDistanceFromHomeMeters;
        public float RemainingMarginMeters;
        public float DynamicPassthroughOpacity; // [0.0 = pure VR, 1.0 = full real-world camera passthrough]
        public bool ShouldPauseBallMachine;
    }

    /// <summary>
    /// Renders the +/- 1.2m room-scale footwork safety ring on the floor and dynamically blends
    /// Meta Quest 3 color passthrough if the player's head or extended racket tip approaches
    /// the edge of their calibrated living-room play space.
    /// </summary>
    public class RoomScaleGuardianVisualizer : MonoBehaviour
    {
        public const float DefaultCautionMarginMeters = 0.25f;
        public const float DefaultMaxDepthExcursionMeters = 0.90f; // 1 step forward/back from baseline marker

        [Header("Configuration")]
        [SerializeField] private TennisPhysicsConfig config = TennisPhysicsConfig.CreateDefault();
        [SerializeField] private QuestEnvironmentBlendMode blendMode = QuestEnvironmentBlendMode.MixedRealityBaselineWindow;
        [SerializeField] private float cautionBufferMeters = DefaultCautionMarginMeters;

        [Header("Tracked Transforms")]
        [SerializeField] private Transform homeCenterMarker;
        [SerializeField] private Transform headsetTransform;
        [SerializeField] private Transform racketTipTransform;

        // --- Runtime State ---
        private RoomScaleSafetyStatus currentStatus;

        // --- Events ---
        public event Action<RoomScaleSafetyStatus> OnSafetyStatusChanged;

        // --- Public Properties ---
        public QuestEnvironmentBlendMode BlendMode
        {
            get => blendMode;
            set => blendMode = value;
        }

        public RoomScaleSafetyStatus CurrentStatus => currentStatus;

        private void Awake()
        {
            if (config.MaxLateralReachMeters <= 0f)
            {
                config = TennisPhysicsConfig.CreateDefault();
            }
        }

        private void FixedUpdate()
        {
            Vector3 homePos = homeCenterMarker != null ? homeCenterMarker.position : Vector3.zero;
            Vector3 hmdPos = headsetTransform != null ? headsetTransform.position : homePos;
            Vector3 tipPos = racketTipTransform != null ? racketTipTransform.position : hmdPos;

            RoomScaleSafetyStatus nextStatus = EvaluateSafetyStatus(
                homePos,
                hmdPos,
                tipPos,
                config.MaxLateralReachMeters,
                DefaultMaxDepthExcursionMeters,
                cautionBufferMeters,
                blendMode);

            bool stateChanged = nextStatus.SafetyState != currentStatus.SafetyState;
            currentStatus = nextStatus;

            if (stateChanged)
            {
                OnSafetyStatusChanged?.Invoke(currentStatus);
            }
        }

        /// <summary>
        /// Pure calculation of the player's head and extended racket-tip margin inside the room-scale box,
        /// returning the safety state and dynamic Quest 3 passthrough opacity.
        /// </summary>
        public static RoomScaleSafetyStatus EvaluateSafetyStatus(
            Vector3 homeCenter,
            Vector3 headPosition,
            Vector3 racketTipPosition,
            float maxLateralReachMeters = 1.20f,
            float maxDepthReachMeters = DefaultMaxDepthExcursionMeters,
            float cautionBufferMeters = DefaultCautionMarginMeters,
            QuestEnvironmentBlendMode mode = QuestEnvironmentBlendMode.MixedRealityBaselineWindow)
        {
            float headLateral = Mathf.Abs(headPosition.x - homeCenter.x);
            float tipLateral = Mathf.Abs(racketTipPosition.x - homeCenter.x);
            float worstLateral = Mathf.Max(headLateral, tipLateral);

            float headDepth = Mathf.Abs(headPosition.z - homeCenter.z);
            float tipDepth = Mathf.Abs(racketTipPosition.z - homeCenter.z);
            float worstDepth = Mathf.Max(headDepth, tipDepth);

            float lateralMargin = maxLateralReachMeters - worstLateral;
            float depthMargin = maxDepthReachMeters - worstDepth;
            float remainingMargin = Mathf.Min(lateralMargin, depthMargin);

            RoomScaleSafetyState state;
            if (remainingMargin < 0f)
            {
                state = RoomScaleSafetyState.BoundaryBreached;
            }
            else if (remainingMargin <= cautionBufferMeters)
            {
                state = RoomScaleSafetyState.CautionNearEdge;
            }
            else
            {
                state = RoomScaleSafetyState.SafeCenter;
            }

            // Base passthrough alpha depends on chosen environment blend mode
            float basePassthrough = mode == QuestEnvironmentBlendMode.FullPassthroughBackboard
                ? 1.0f
                : (mode == QuestEnvironmentBlendMode.MixedRealityBaselineWindow ? 0.35f : 0.0f);

            // Smoothly ramp passthrough opacity up to 1.0 as remaining margin shrinks from cautionBuffer to 0m
            float proximityFade = 0f;
            if (cautionBufferMeters > 0.001f && remainingMargin < cautionBufferMeters)
            {
                proximityFade = Mathf.Clamp01(1f - (remainingMargin / cautionBufferMeters));
            }

            float finalOpacity = Mathf.Clamp01(Mathf.Max(basePassthrough, proximityFade));

            return new RoomScaleSafetyStatus
            {
                SafetyState = state,
                LateralDistanceFromHomeMeters = worstLateral,
                DepthDistanceFromHomeMeters = worstDepth,
                RemainingMarginMeters = remainingMargin,
                DynamicPassthroughOpacity = finalOpacity,
                ShouldPauseBallMachine = (state == RoomScaleSafetyState.BoundaryBreached)
            };
        }

        /// <summary>
        /// Generates a rounded-rectangle floor perimeter ring around the home center marker
        /// for rendering the +/- 1.2m footwork boundary on the virtual/MR floor.
        /// </summary>
        public static List<Vector3> GenerateSafetyRingPerimeter(
            Vector3 homeCenter,
            float halfWidthMeters = 1.20f,
            float halfDepthMeters = DefaultMaxDepthExcursionMeters,
            float floorYOffset = 0.008f)
        {
            float y = homeCenter.y + floorYOffset;
            return new List<Vector3>(5)
            {
                new Vector3(homeCenter.x - halfWidthMeters, y, homeCenter.z - halfDepthMeters),
                new Vector3(homeCenter.x + halfWidthMeters, y, homeCenter.z - halfDepthMeters),
                new Vector3(homeCenter.x + halfWidthMeters, y, homeCenter.z + halfDepthMeters),
                new Vector3(homeCenter.x - halfWidthMeters, y, homeCenter.z + halfDepthMeters),
                new Vector3(homeCenter.x - halfWidthMeters, y, homeCenter.z - halfDepthMeters)
            };
        }
    }
}

