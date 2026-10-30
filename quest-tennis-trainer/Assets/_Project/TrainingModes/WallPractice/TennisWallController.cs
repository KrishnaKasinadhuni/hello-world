using System;
using System.Collections.Generic;
using UnityEngine;
using QuestTennisTrainer.Core;
using QuestTennisTrainer.Physics;

namespace QuestTennisTrainer.TrainingModes.WallPractice
{
    /// <summary>
    /// Preset distances for the Tennis Wall (Backboard) training mode.
    /// </summary>
    public enum WallDistancePreset
    {
        ReflexVolleys6m = 6,
        MidCourtTransition9m = 9,
        BaselineGroundstrokes12m = 12
    }

    /// <summary>
    /// Rectangular target box painted on the practice wall above the 0.914m net line.
    /// </summary>
    [Serializable]
    public struct WallTargetZone
    {
        public string ZoneId;
        public Vector2 LocalCenter; // X = horizontal offset from wall center, Y = height from floor
        public Vector2 Size;        // Width and Height in meters
        public int PointValue;

        public bool Contains(float localX, float heightY)
        {
            float halfW = Size.x * 0.5f;
            float halfH = Size.y * 0.5f;
            return Mathf.Abs(localX - LocalCenter.x) <= halfW &&
                   Mathf.Abs(heightY - LocalCenter.y) <= halfH;
        }
    }

    /// <summary>
    /// Telemetry emitted on every backboard impact.
    /// </summary>
    [Serializable]
    public struct WallHitResult
    {
        public Vector3 ImpactPoint;
        public float ImpactHeightMeters;
        public float NetClearanceMeters;
        public bool IsNetFault;
        public bool IsOut;
        public string HitTargetZoneId;
        public int CurrentRallyStreak;
        public int BestRallyStreak;
        public int TotalScore;
        public Vector3 ReboundVelocity;
    }

    /// <summary>
    /// Controls the single-player Tennis Wall (Backboard) training system.
    /// Features net-line fault detection (0.914m), target zones, vertical spin reflection
    /// (topspin kicks upward off the wall), and a Smart Return Funnel (85% true reflection + 15% home-center bias)
    /// to keep rallies inside the player's +/- 1.2m room-scale reach.
    /// </summary>
    public class TennisWallController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TennisPhysicsConfig config = TennisPhysicsConfig.CreateDefault();
        [SerializeField] private BallAerodynamics activeBall;
        [SerializeField] private Transform playerHomeCenter;

        [Header("Wall Geometry & Distance")]
        [SerializeField] private float wallDistanceMeters = 12f;
        [SerializeField] private float wallWidthMeters = 8.23f;  // Singles court width
        [SerializeField] private float wallHeightMeters = 4.5f;
        [SerializeField] private float netLineHeightMeters = TennisPhysicsConfig.NetCenterHeightMeters; // 0.914m

        [Header("Target Zones")]
        [SerializeField] private List<WallTargetZone> targetZones = new List<WallTargetZone>();

        // --- Runtime Rally Telemetry ---
        private int currentRallyStreak;
        private int bestRallyStreak;
        private int targetZoneHits;
        private int totalScore;
        private Vector3 prevBallPosition;
        private bool hasPrevBallPos;

        // --- Events ---
        public event Action<WallHitResult> OnWallHit;
        public event Action<WallHitResult> OnNetFault;
        public event Action<int> OnStreakUpdated;

        // --- Public Properties ---
        public int CurrentRallyStreak => currentRallyStreak;
        public int BestRallyStreak => bestRallyStreak;
        public int TargetZoneHits => targetZoneHits;
        public int TotalScore => totalScore;
        public float WallDistanceMeters => wallDistanceMeters;
        public float NetLineHeightMeters => netLineHeightMeters;
        public IReadOnlyList<WallTargetZone> TargetZones => targetZones;

        private void Awake()
        {
            if (config.DragCoefficient <= 0f)
            {
                config = TennisPhysicsConfig.CreateDefault();
            }

            if (targetZones.Count == 0)
            {
                InitializeDefaultTargetZones();
            }
        }

        private void Start()
        {
            ApplyWallDistance(wallDistanceMeters);
            if (activeBall != null)
            {
                prevBallPosition = activeBall.transform.position;
                hasPrevBallPos = true;
            }
        }

        private void FixedUpdate()
        {
            if (activeBall == null || !activeBall.IsSimulating) return;

            Vector3 currPos = activeBall.transform.position;
            if (!hasPrevBallPos)
            {
                prevBallPosition = currPos;
                hasPrevBallPos = true;
                return;
            }

            CheckAndResolveWallCollision(prevBallPosition, currPos, activeBall);
            prevBallPosition = activeBall.transform.position;
        }

        /// <summary>
        /// Sets the practice wall distance from the player's home center marker (e.g. 6m volleys, 12m groundstrokes).
        /// </summary>
        public void SetWallDistancePreset(WallDistancePreset preset)
        {
            ApplyWallDistance((float)preset);
        }

        public void ApplyWallDistance(float distanceMeters)
        {
            wallDistanceMeters = Mathf.Clamp(distanceMeters, 4f, 16f);
            Vector3 homePos = playerHomeCenter != null ? playerHomeCenter.position : Vector3.zero;
            transform.position = new Vector3(homePos.x, 0f, homePos.z + wallDistanceMeters);
            transform.rotation = Quaternion.identity; // Wall faces -Z (back toward player at homePos)
        }

        /// <summary>
        /// Populates default practice target boxes above the 0.914m net line.
        /// </summary>
        public void InitializeDefaultTargetZones()
        {
            targetZones.Clear();
            // Center Deep Control Box
            targetZones.Add(new WallTargetZone
            {
                ZoneId = "Center_Control",
                LocalCenter = new Vector2(0f, 1.55f),
                Size = new Vector2(1.4f, 0.9f),
                PointValue = 100
            });
            // Left (Ad-Side) Corner Box
            targetZones.Add(new WallTargetZone
            {
                ZoneId = "Left_Corner",
                LocalCenter = new Vector2(-2.2f, 1.45f),
                Size = new Vector2(1.2f, 0.85f),
                PointValue = 150
            });
            // Right (Deuce-Side) Corner Box
            targetZones.Add(new WallTargetZone
            {
                ZoneId = "Right_Corner",
                LocalCenter = new Vector2(2.2f, 1.45f),
                Size = new Vector2(1.2f, 0.85f),
                PointValue = 150
            });
        }

        /// <summary>
        /// Detects swept ball crossing against the vertical wall plane, resolves spin reflection,
        /// applies the Smart Return Funnel, and updates rally telemetry.
        /// </summary>
        public bool CheckAndResolveWallCollision(Vector3 prevPos, Vector3 currPos, BallAerodynamics ball)
        {
            if (ball == null) return false;

            float wallPlaneZ = transform.position.z - TennisPhysicsConfig.BallRadiusMeters;
            Vector3 vel = ball.Velocity;

            // Ball must be travelling toward the wall (+Z) and cross the wall contact plane
            if (vel.z <= 0f || prevPos.z > wallPlaneZ || currPos.z < wallPlaneZ)
            {
                return false;
            }

            // Linearly interpolate exact crossing fraction t in [0, 1]
            float dz = currPos.z - prevPos.z;
            float t = Mathf.Abs(dz) > 0.0001f ? Mathf.Clamp01((wallPlaneZ - prevPos.z) / dz) : 1f;
            Vector3 impactPoint = Vector3.Lerp(prevPos, currPos, t);
            impactPoint.z = wallPlaneZ;

            float localX = impactPoint.x - transform.position.x;
            float impactY = impactPoint.y;

            // Check if shot missed the wall entirely
            bool isOut = Mathf.Abs(localX) > (wallWidthMeters * 0.5f) || impactY > wallHeightMeters || impactY < 0f;
            if (isOut)
            {
                ResetRallyStreak();
                WallHitResult outResult = new WallHitResult
                {
                    ImpactPoint = impactPoint,
                    ImpactHeightMeters = impactY,
                    NetClearanceMeters = impactY - netLineHeightMeters,
                    IsNetFault = false,
                    IsOut = true,
                    HitTargetZoneId = string.Empty,
                    CurrentRallyStreak = 0,
                    BestRallyStreak = bestRallyStreak,
                    TotalScore = totalScore,
                    ReboundVelocity = vel
                };
                OnWallHit?.Invoke(outResult);
                return false;
            }

            // 1. Resolve physical ConcreteWall rebound (includes vertical topspin kick)
            Vector3 wallNormal = Vector3.back; // (0, 0, -1) pointing back toward player
            Vector3 reboundVel = ball.Velocity;
            Vector3 reboundAngVel = ball.AngularVelocity;
            BallAerodynamics.ResolveSurfaceBounce(
                ref reboundVel,
                ref reboundAngVel,
                wallNormal,
                CourtSurfaceType.ConcreteWall,
                config);

            // 2. Check Net-Line Fault (painted at 0.914m)
            float netClearance = impactY - netLineHeightMeters;
            bool isNetFault = netClearance < 0f;

            if (isNetFault)
            {
                // Dampen rebound velocity on net-line fault and reset rally streak
                reboundVel *= 0.45f;
                ball.transform.position = impactPoint;
                ball.Velocity = reboundVel;
                ball.AngularVelocity = reboundAngVel;

                ResetRallyStreak();
                WallHitResult faultResult = new WallHitResult
                {
                    ImpactPoint = impactPoint,
                    ImpactHeightMeters = impactY,
                    NetClearanceMeters = netClearance,
                    IsNetFault = true,
                    IsOut = false,
                    HitTargetZoneId = string.Empty,
                    CurrentRallyStreak = 0,
                    BestRallyStreak = bestRallyStreak,
                    TotalScore = totalScore,
                    ReboundVelocity = reboundVel
                };

                OnNetFault?.Invoke(faultResult);
                OnWallHit?.Invoke(faultResult);
                return true;
            }

            // 3. Apply Smart Return Funnel (85% true reflection angle + 15% lateral home-center bias)
            Vector3 homeCenter = playerHomeCenter != null ? playerHomeCenter.position : Vector3.zero;
            reboundVel = ComputeSmartFunnelVelocity(
                impactPoint,
                reboundVel,
                homeCenter,
                config.WallSmartFunnelBias,
                config.MaxLateralReachMeters);

            ball.transform.position = impactPoint;
            ball.Velocity = reboundVel;
            ball.AngularVelocity = reboundAngVel;

            // 4. Evaluate Target Zones & Rally Streak
            currentRallyStreak++;
            if (currentRallyStreak > bestRallyStreak)
            {
                bestRallyStreak = currentRallyStreak;
            }

            string hitZoneId = string.Empty;
            int zonePoints = 25; // Base point for valid shot over the net line

            for (int i = 0; i < targetZones.Count; i++)
            {
                if (targetZones[i].Contains(localX, impactY))
                {
                    hitZoneId = targetZones[i].ZoneId;
                    zonePoints = targetZones[i].PointValue;
                    targetZoneHits++;
                    break;
                }
            }

            totalScore += zonePoints;
            OnStreakUpdated?.Invoke(currentRallyStreak);

            WallHitResult validResult = new WallHitResult
            {
                ImpactPoint = impactPoint,
                ImpactHeightMeters = impactY,
                NetClearanceMeters = netClearance,
                IsNetFault = false,
                IsOut = false,
                HitTargetZoneId = hitZoneId,
                CurrentRallyStreak = currentRallyStreak,
                BestRallyStreak = bestRallyStreak,
                TotalScore = totalScore,
                ReboundVelocity = reboundVel
            };

            OnWallHit?.Invoke(validResult);
            return true;
        }

        /// <summary>
        /// Blends true physical reflection angle with a centering funnel bias (default 15%)
        /// and clamps predicted baseline arrival within the player's room-scale lateral reach (+/- 1.2m).
        /// </summary>
        public static Vector3 ComputeSmartFunnelVelocity(
            Vector3 impactPoint,
            Vector3 trueReboundVelocity,
            Vector3 homeCenter,
            float funnelBias,
            float maxLateralReachMeters)
        {
            float speed = trueReboundVelocity.magnitude;
            if (speed < 0.01f || Mathf.Abs(trueReboundVelocity.z) < 0.01f)
            {
                return trueReboundVelocity;
            }

            funnelBias = Mathf.Clamp01(funnelBias);

            // Estimate flight time back to player's Z plane (homeCenter.z)
            float deltaZ = homeCenter.z - impactPoint.z; // negative since wall is at +Z and player at 0
            float timeToBaseline = Mathf.Abs(deltaZ / trueReboundVelocity.z);

            // True unassisted X landing coordinate at player's baseline
            float trueArrivalX = impactPoint.x + trueReboundVelocity.x * timeToBaseline;

            // Blend 85% true arrival X with 15% homeCenter.x
            float blendedArrivalX = Mathf.Lerp(trueArrivalX, homeCenter.x, funnelBias);

            // Soft-clamp within the player's calibrated room-scale reach (+/- 1.2m)
            float minReachX = homeCenter.x - maxLateralReachMeters;
            float maxReachX = homeCenter.x + maxLateralReachMeters;
            float clampedArrivalX = Mathf.Clamp(blendedArrivalX, minReachX, maxReachX);

            // Compute adjusted lateral velocity vx while preserving total rebound speed and vertical kick vy
            float adjustedVx = (clampedArrivalX - impactPoint.x) / Mathf.Max(timeToBaseline, 0.1f);
            Vector3 adjustedVel = new Vector3(adjustedVx, trueReboundVelocity.y, trueReboundVelocity.z);

            return adjustedVel.normalized * speed;
        }

        public void ResetRallyStreak()
        {
            currentRallyStreak = 0;
            OnStreakUpdated?.Invoke(currentRallyStreak);
        }

        public void ResetSessionStats()
        {
            currentRallyStreak = 0;
            bestRallyStreak = 0;
            targetZoneHits = 0;
            totalScore = 0;
            OnStreakUpdated?.Invoke(0);
        }
    }
}

