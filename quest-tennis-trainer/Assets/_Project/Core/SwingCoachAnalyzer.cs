using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using QuestTennisTrainer.Physics;

namespace QuestTennisTrainer.Core
{
    /// <summary>
    /// Biomechanical breakdown of a single tennis stroke at the instant of stringbed contact.
    /// </summary>
    [Serializable]
    public struct StrokeBiomechanicsReport
    {
        public int ShotIndex;
        public float SwingPathAngleDeg;     // Positive = low-to-high topspin brush, Negative = downward chop
        public float RacketFaceTiltDeg;     // Positive = open face, Negative = closed hood, ~0 = square
        public float OutgoingSpeedKmh;
        public float OutgoingSpinRpm;
        public float SweetSpotPercent;
        public bool IsFrameMishit;
        public string CoachingCue;
    }

    /// <summary>
    /// Aggregate session summary metrics across all recorded strokes.
    /// </summary>
    [Serializable]
    public struct SessionCoachingSummary
    {
        public int TotalShots;
        public float AverageSpeedKmh;
        public float AverageSpinRpm;
        public float AverageSwingPathAngleDeg;
        public float SweetSpotHitRatePercent;
        public int FrameMishitCount;
    }

    /// <summary>
    /// Biomechanical Swing Coach & Token-Efficient Telemetry Exporter (TOON/CSV tabular layout).
    /// Evaluates low-to-high swing path angle, stringbed face angle at contact, and sweet-spot consistency,
    /// generating actionable coaching cues after each shot.
    /// </summary>
    public class SwingCoachAnalyzer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SweptRacketCollider racketCollider;

        private readonly List<StrokeBiomechanicsReport> strokeHistory = new List<StrokeBiomechanicsReport>(256);

        // --- Events ---
        public event Action<StrokeBiomechanicsReport> OnStrokeAnalyzed;

        // --- Public Properties ---
        public IReadOnlyList<StrokeBiomechanicsReport> StrokeHistory => strokeHistory;

        public StrokeBiomechanicsReport RecordAndAnalyzeStroke(ImpactData impact, Vector3 racketVelocityAtContact)
        {
            int nextIndex = strokeHistory.Count + 1;
            StrokeBiomechanicsReport report = AnalyzeStrokeBiomechanics(nextIndex, impact, racketVelocityAtContact);
            strokeHistory.Add(report);
            OnStrokeAnalyzed?.Invoke(report);
            return report;
        }

        /// <summary>
        /// Pure biomechanical solver computing low-to-high swing path angle (degrees),
        /// stringbed face tilt (degrees), and a targeted tennis coaching cue.
        /// </summary>
        public static StrokeBiomechanicsReport AnalyzeStrokeBiomechanics(
            int shotIndex,
            ImpactData impact,
            Vector3 racketVelocity)
        {
            // 1. Low-to-High Swing Path Angle: atan2(vy, horizSpeed) in degrees
            float horizSpeed = Mathf.Sqrt(racketVelocity.x * racketVelocity.x + racketVelocity.z * racketVelocity.z);
            float swingPathRad = horizSpeed > 0.01f
                ? (float)Math.Atan2(racketVelocity.y, horizSpeed)
                : 0f;
            float swingPathDeg = swingPathRad * Mathf.Rad2Deg;

            // 2. Stringbed Face Tilt Angle relative to vertical plane: asin(normal.y) in degrees
            Vector3 norm = impact.ImpactNormal.normalized;
            float clampedNy = Mathf.Clamp(norm.y, -1f, 1f);
            float faceTiltDeg = (float)Math.Asin(clampedNy) * Mathf.Rad2Deg;

            float sweetPct = Mathf.Clamp(Mathf.RoundToInt(impact.SweetSpotAccuracy * 100f), 0, 100);
            string cue = BuildCoachingCue(swingPathDeg, faceTiltDeg, impact.IsFrameHit, impact.OutgoingSpinRpm);

            return new StrokeBiomechanicsReport
            {
                ShotIndex = shotIndex,
                SwingPathAngleDeg = swingPathDeg,
                RacketFaceTiltDeg = faceTiltDeg,
                OutgoingSpeedKmh = impact.OutgoingSpeedKmh,
                OutgoingSpinRpm = impact.OutgoingSpinRpm,
                SweetSpotPercent = sweetPct,
                IsFrameMishit = impact.IsFrameHit,
                CoachingCue = cue
            };
        }

        public static string BuildCoachingCue(
            float swingPathDeg,
            float faceTiltDeg,
            bool isFrameHit,
            float spinRpm)
        {
            if (isFrameHit)
            {
                return "Watch the ball onto the strings — frame mishit detected; keep your head still through contact.";
            }

            if (faceTiltDeg > 11f && swingPathDeg > 8f)
            {
                return $"Racket face is open (+{Mathf.RoundToInt(faceTiltDeg)} deg) — close the stringbed slightly so topspin dips inside the baseline.";
            }

            if (faceTiltDeg < -10f)
            {
                return $"Racket face is overly closed ({Mathf.RoundToInt(faceTiltDeg)} deg) — risk of netting; square the strings through the strike zone.";
            }

            if (swingPathDeg >= 12f && swingPathDeg <= 30f && spinRpm >= 1500f)
            {
                return $"Textbook low-to-high topspin brush (+{Mathf.RoundToInt(swingPathDeg)} deg, {Mathf.RoundToInt(spinRpm)} RPM) with a stable stringbed!";
            }

            if (swingPathDeg < -4f)
            {
                return $"High-to-low drive/slice path ({Mathf.RoundToInt(swingPathDeg)} deg) — great for penetrating approach shots and serves.";
            }

            return $"Solid flat contact ({Mathf.RoundToInt(swingPathDeg)} deg path) — drop the racket head lower before contact to add heavier topspin margin.";
        }

        /// <summary>
        /// Computes aggregate coaching statistics across the active practice session.
        /// </summary>
        public SessionCoachingSummary ComputeSessionSummary()
        {
            int count = strokeHistory.Count;
            if (count == 0) return default;

            float sumSpeed = 0f;
            float sumSpin = 0f;
            float sumSwingDeg = 0f;
            int cleanHits = 0;
            int mishits = 0;

            for (int i = 0; i < count; i++)
            {
                StrokeBiomechanicsReport s = strokeHistory[i];
                sumSpeed += s.OutgoingSpeedKmh;
                sumSpin += s.OutgoingSpinRpm;
                sumSwingDeg += s.SwingPathAngleDeg;
                if (s.IsFrameMishit) mishits++;
                else cleanHits++;
            }

            return new SessionCoachingSummary
            {
                TotalShots = count,
                AverageSpeedKmh = sumSpeed / count,
                AverageSpinRpm = sumSpin / count,
                AverageSwingPathAngleDeg = sumSwingDeg / count,
                SweetSpotHitRatePercent = (cleanHits * 100f) / count,
                FrameMishitCount = mishits
            };
        }

        /// <summary>
        /// Exports the recorded stroke history in a compact TOON-style headered tabular format
        /// (~55% fewer tokens than verbose JSON arrays) for LLM AI Coach analysis or cloud telemetry backup.
        /// </summary>
        public string ExportSessionToCompactTable()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("strokes[shot_idx,speed_kmh,spin_rpm,swing_deg,face_deg,sweet_pct,frame_hit]:");

            for (int i = 0; i < strokeHistory.Count; i++)
            {
                StrokeBiomechanicsReport s = strokeHistory[i];
                sb.Append("  ")
                  .Append(s.ShotIndex).Append(',')
                  .Append(Mathf.RoundToInt(s.OutgoingSpeedKmh)).Append(',')
                  .Append(Mathf.RoundToInt(s.OutgoingSpinRpm)).Append(',')
                  .Append(s.SwingPathAngleDeg.ToString("F1")).Append(',')
                  .Append(s.RacketFaceTiltDeg.ToString("F1")).Append(',')
                  .Append(Mathf.RoundToInt(s.SweetSpotPercent)).Append(',')
                  .Append(s.IsFrameMishit ? 1 : 0)
                  .AppendLine();
            }

            return sb.ToString();
        }

        public void ClearHistory()
        {
            strokeHistory.Clear();
        }
    }
}

