using System;

namespace QuestTennisTrainer.Core
{
    /// <summary>
    /// Baseline physical constants and room-scale constraints for Meta Quest 3 at 120 Hz.
    /// Pure C# definition so it can be shared between Unity runtime and fast NUnit CI tests.
    /// </summary>
    [Serializable]
    public struct TennisPhysicsConfig
    {
        // --- Quest 3 Timing & Sub-Stepping ---
        public const float TargetFrameRateHz = 120f;
        public const float FixedDeltaTime = 1f / TargetFrameRateHz; // 0.008333s (8.33 ms)
        public const int CollisionSubSteps = 10;                    // ~0.83 ms resolution for fast serves

        // --- ITF Standard Tennis Ball Constants ---
        public const float BallMassKg = 0.0577f;                    // 57.7 grams
        public const float BallRadiusMeters = 0.0335f;              // 6.7 cm diameter
        public const float AirDensityKgPerM3 = 1.204f;              // Sea-level air density at 20 C
        public const float GravityMps2 = 9.81f;

        // --- Aerodynamic Coefficients ---
        public float DragCoefficient;                               // Standard tennis ball Cd (~0.55)
        public float MagnusLiftScale;                               // Spin lift multiplier (Cl)

        // --- Stringbed & Surface Restitution ---
        public float SweetSpotRestitution;                          // Normal COR at stringbed center (~0.82)
        public float FrameMishitRestitution;                        // Normal COR near racket frame (~0.45)
        public float StringTangentialFriction;                      // Controls topspin/slice RPM bite

        // --- Room-Scale Constraints (1-2 Step Radius) ---
        public float MaxLateralReachMeters;                         // Default +/- 1.2m from center marker
        public float WallSmartFunnelBias;                           // 0.15 = 85% true angle, 15% center assist

        // --- Standard Court & Net Dimensions (Meters) ---
        public const float BaselineToNetMeters = 11.887f;
        public const float ServiceLineToNetMeters = 6.401f;
        public const float NetCenterHeightMeters = 0.914f;
        public const float SinglesCourtWidthMeters = 8.23f;

        public static TennisPhysicsConfig CreateDefault()
        {
            return new TennisPhysicsConfig
            {
                DragCoefficient = 0.55f,
                MagnusLiftScale = 0.22f,
                SweetSpotRestitution = 0.82f,
                FrameMishitRestitution = 0.45f,
                StringTangentialFriction = 0.65f,
                MaxLateralReachMeters = 1.20f,
                WallSmartFunnelBias = 0.15f
            };
        }
    }
}
