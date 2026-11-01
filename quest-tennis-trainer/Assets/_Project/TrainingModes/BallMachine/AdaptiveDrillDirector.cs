using System;
using UnityEngine;
using QuestTennisTrainer.Core;

namespace QuestTennisTrainer.TrainingModes.BallMachine
{
    /// <summary>
    /// Progressive 5-tier AI difficulty ladder for the Programmable Ball Machine.
    /// </summary>
    public enum AdaptiveDifficultyTier
    {
        Tier1_WarmupGroove = 1,
        Tier2_BaselineRally = 2,
        Tier3_HeavyTopspin = 3,
        Tier4_ProPace = 4,
        Tier5_ChampionshipPressure = 5
    }

    /// <summary>
    /// Snapshot of the Adaptive Drill Director's state and tuned BallFeedParameters after evaluating a player stroke.
    /// </summary>
    [Serializable]
    public struct AdaptiveDrillSnapshot
    {
        public AdaptiveDifficultyTier CurrentTier;
        public int ConsecutiveQualityReturns;
        public int ConsecutiveErrors;
        public BallFeedParameters RecommendedFeed;
        public string DirectorStatusMessage;
    }

    /// <summary>
    /// Session 14: Adaptive AI Drill Difficulty Director for Meta Quest 3.
    /// Dynamically scales BallMachineController feed speed, spin RPM, and interval across 5 progressive skill tiers
    /// based on the player's sweet-spot accuracy and net/court consistency, while strictly clamping lateral feeds
    /// within the player's +/- 1.20m room-scale boundary.
    /// </summary>
    public class AdaptiveDrillDirector : MonoBehaviour
    {
        public const int PromotionStreakThreshold = 3;
        public const int DemotionErrorThreshold = 2;
        public const float QualitySweetSpotThreshold = 0.65f;

        [Header("Adaptive Progression")]
        [SerializeField] private AdaptiveDifficultyTier currentTier = AdaptiveDifficultyTier.Tier2_BaselineRally;
        [SerializeField] private float maxRoomScaleReachMeters = 1.20f;

        private int consecutiveQualityReturns;
        private int consecutiveErrors;
        private int feedCounter;

        public AdaptiveDifficultyTier CurrentTier => currentTier;
        public int ConsecutiveQualityReturns => consecutiveQualityReturns;
        public int ConsecutiveErrors => consecutiveErrors;
        public AdaptiveDrillSnapshot LatestSnapshot { get; private set; }

        /// <summary>
        /// Resets the director to a specified starting tier.
        /// </summary>
        public void ResetToTier(AdaptiveDifficultyTier startingTier = AdaptiveDifficultyTier.Tier2_BaselineRally)
        {
            currentTier = startingTier;
            consecutiveQualityReturns = 0;
            consecutiveErrors = 0;
            feedCounter = 0;
            LatestSnapshot = BuildSnapshot("Adaptive Drill initialized.");
        }

        /// <summary>
        /// Evaluates a player's stroke outcome and promotes or demotes the ball machine difficulty tier accordingly.
        /// </summary>
        public AdaptiveDrillSnapshot EvaluatePlayerStroke(
            bool clearedNet,
            bool landedInBounds,
            float sweetSpotAccuracy,
            bool isFrameHit)
        {
            feedCounter++;
            bool isQualityReturn = clearedNet
                && landedInBounds
                && !isFrameHit
                && sweetSpotAccuracy >= QualitySweetSpotThreshold;

            string statusMsg;

            if (isQualityReturn)
            {
                consecutiveQualityReturns++;
                consecutiveErrors = 0;

                if (consecutiveQualityReturns >= PromotionStreakThreshold &&
                    currentTier < AdaptiveDifficultyTier.Tier5_ChampionshipPressure)
                {
                    currentTier = (AdaptiveDifficultyTier)((int)currentTier + 1);
                    consecutiveQualityReturns = 0;
                    statusMsg = $"Promoted to {currentTier}! Increasing pace and topspin RPM.";
                }
                else
                {
                    statusMsg = $"Quality return ({consecutiveQualityReturns}/{PromotionStreakThreshold} toward next tier).";
                }
            }
            else
            {
                consecutiveErrors++;
                consecutiveQualityReturns = 0;

                if (consecutiveErrors >= DemotionErrorThreshold &&
                    currentTier > AdaptiveDifficultyTier.Tier1_WarmupGroove)
                {
                    currentTier = (AdaptiveDifficultyTier)((int)currentTier - 1);
                    consecutiveErrors = 0;
                    statusMsg = $"Stepping down to {currentTier} to rebuild rhythm and sweet-spot timing.";
                }
                else
                {
                    statusMsg = $"Missed return ({consecutiveErrors}/{DemotionErrorThreshold} before tier step-down).";
                }
            }

            LatestSnapshot = BuildSnapshot(statusMsg);
            return LatestSnapshot;
        }

        /// <summary>
        /// Computes the exact BallFeedParameters for a given difficulty tier, strictly clamped to +/- 1.20m room-scale reach.
        /// </summary>
        public static BallFeedParameters GetFeedParametersForTier(
            AdaptiveDifficultyTier tier,
            int sequenceIndex,
            float maxLateralReachMeters = 1.20f)
        {
            float safeMaxReach = Mathf.Clamp(maxLateralReachMeters, 0.30f, 1.20f);
            float alternatingSign = (sequenceIndex % 2 == 0) ? 1f : -1f;

            switch (tier)
            {
                case AdaptiveDifficultyTier.Tier1_WarmupGroove:
                    return new BallFeedParameters
                    {
                        FeedIntervalSeconds = 3.8f,
                        LaunchSpeedKmh = 62f,
                        ElevationAngleDeg = 14.5f,
                        TopspinRpm = 800f,
                        SidespinRpm = 0f,
                        TargetLateralOffsetMeters = Mathf.Clamp(0.45f * alternatingSign, -safeMaxReach, safeMaxReach)
                    };

                case AdaptiveDifficultyTier.Tier2_BaselineRally:
                    return new BallFeedParameters
                    {
                        FeedIntervalSeconds = 3.1f,
                        LaunchSpeedKmh = 78f,
                        ElevationAngleDeg = 13.0f,
                        TopspinRpm = 1450f,
                        SidespinRpm = 0f,
                        TargetLateralOffsetMeters = Mathf.Clamp(0.75f * alternatingSign, -safeMaxReach, safeMaxReach)
                    };

                case AdaptiveDifficultyTier.Tier3_HeavyTopspin:
                    return new BallFeedParameters
                    {
                        FeedIntervalSeconds = 2.6f,
                        LaunchSpeedKmh = 94f,
                        ElevationAngleDeg = 12.0f,
                        TopspinRpm = 2200f,
                        SidespinRpm = 150f * alternatingSign,
                        TargetLateralOffsetMeters = Mathf.Clamp(0.95f * alternatingSign, -safeMaxReach, safeMaxReach)
                    };

                case AdaptiveDifficultyTier.Tier4_ProPace:
                    return new BallFeedParameters
                    {
                        FeedIntervalSeconds = 2.1f,
                        LaunchSpeedKmh = 110f,
                        ElevationAngleDeg = 10.8f,
                        TopspinRpm = 2750f,
                        SidespinRpm = 250f * alternatingSign,
                        TargetLateralOffsetMeters = Mathf.Clamp(1.10f * alternatingSign, -safeMaxReach, safeMaxReach)
                    };

                case AdaptiveDifficultyTier.Tier5_ChampionshipPressure:
                default:
                    return new BallFeedParameters
                    {
                        FeedIntervalSeconds = 1.7f,
                        LaunchSpeedKmh = 124f,
                        ElevationAngleDeg = 9.8f,
                        TopspinRpm = 3200f,
                        SidespinRpm = 350f * alternatingSign,
                        TargetLateralOffsetMeters = Mathf.Clamp(1.20f * alternatingSign, -safeMaxReach, safeMaxReach)
                    };
            }
        }

        private AdaptiveDrillSnapshot BuildSnapshot(string statusMessage)
        {
            return new AdaptiveDrillSnapshot
            {
                CurrentTier = currentTier,
                ConsecutiveQualityReturns = consecutiveQualityReturns,
                ConsecutiveErrors = consecutiveErrors,
                RecommendedFeed = GetFeedParametersForTier(currentTier, feedCounter, maxRoomScaleReachMeters),
                DirectorStatusMessage = statusMessage
            };
        }
    }
}

