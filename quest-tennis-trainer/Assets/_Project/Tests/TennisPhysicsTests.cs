using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR;
using QuestTennisTrainer.Core;
using QuestTennisTrainer.Physics;
using QuestTennisTrainer.TrainingModes.WallPractice;
using QuestTennisTrainer.TrainingModes.BallMachine;
using QuestTennisTrainer.TrainingModes.ServeTrainer;
using QuestTennisTrainer.XR_Input;
using QuestTennisTrainer.UI;

namespace QuestTennisTrainer.Tests
{
    /// <summary>
    /// Fast EditMode NUnit test suite verifying 120 Hz physics constants, Magnus aerodynamics,
    /// 10-substep Hermite spline interpolation, Smart Return Funnel clamping, parametric haptics,
    /// Ball Machine room-scale constraints, and Serve Trainer occlusion/fault math.
    /// </summary>
    [TestFixture]
    public class TennisPhysicsTests
    {
        private const float Epsilon = 0.001f;

        [Test]
        public void TennisPhysicsConfig_DefaultsMatchQuest3AndItfSpecification()
        {
            TennisPhysicsConfig cfg = TennisPhysicsConfig.CreateDefault();

            Assert.AreEqual(120f, TennisPhysicsConfig.TargetFrameRateHz, Epsilon);
            Assert.AreEqual(1f / 120f, TennisPhysicsConfig.FixedDeltaTime, 0.00001f);
            Assert.AreEqual(10, TennisPhysicsConfig.CollisionSubSteps);
            Assert.AreEqual(0.0577f, TennisPhysicsConfig.BallMassKg, 0.0001f);
            Assert.AreEqual(0.0335f, TennisPhysicsConfig.BallRadiusMeters, 0.0001f);
            Assert.AreEqual(0.82f, cfg.SweetSpotRestitution, Epsilon);
            Assert.AreEqual(0.45f, cfg.FrameMishitRestitution, Epsilon);
            Assert.AreEqual(1.20f, cfg.MaxLateralReachMeters, Epsilon);
            Assert.AreEqual(0.15f, cfg.WallSmartFunnelBias, Epsilon);
        }

        [Test]
        public void BallAerodynamics_TopspinProducesDownwardMagnusDip_AndBackspinProducesUpwardLift()
        {
            // Ball flying forward (+Z) at 25 m/s (90 km/h)
            Vector3 forwardVel = new Vector3(0f, 0f, 25f);

            // Topspin in Unity (+X right, +Y up, +Z forward): omega = (+w, 0, 0) so omega x r_top = (0, 0, +v_top)
            float rpm2500RadSec = 2500f * (2f * Mathf.PI / 60f);
            Vector3 topspinOmega = new Vector3(rpm2500RadSec, 0f, 0f);
            Vector3 magnusTopspin = BallAerodynamics.ComputeMagnusForce(forwardVel, topspinOmega, 0.22f);

            Assert.Less(magnusTopspin.y, -0.05f, "Topspin must generate a downward (-Y) Magnus force that dips the ball into the court.");

            // Slice/Backspin: omega = (-w, 0, 0)
            Vector3 backspinOmega = new Vector3(-rpm2500RadSec, 0f, 0f);
            Vector3 magnusBackspin = BallAerodynamics.ComputeMagnusForce(forwardVel, backspinOmega, 0.22f);

            Assert.Greater(magnusBackspin.y, 0.05f, "Backspin/slice must generate an upward (+Y) Magnus lift force.");
        }

        [Test]
        public void BallAerodynamics_ConcreteWallBounce_TopspinKicksUpward()
        {
            TennisPhysicsConfig cfg = TennisPhysicsConfig.CreateDefault();
            Vector3 incomingVel = new Vector3(0f, 1.0f, 20f);
            float topspinRadSec = 2400f * (2f * Mathf.PI / 60f);
            Vector3 angVel = new Vector3(topspinRadSec, 0f, 0f);

            // Wall normal points back toward player (-Z)
            BallAerodynamics.ResolveSurfaceBounce(
                ref incomingVel,
                ref angVel,
                Vector3.back,
                CourtSurfaceType.ConcreteWall,
                cfg);

            Assert.Less(incomingVel.z, -14f, "Ball must rebound back toward player (-Z).");
            Assert.Greater(incomingVel.y, 1.0f, "Topspin against vertical backboard must kick upward (+Y).");
        }

        [Test]
        public void SweptRacketCollider_HermiteSpline_InterpolatesEndpointsAndVelocitySmoothly()
        {
            float dt = TennisPhysicsConfig.FixedDeltaTime;
            Vector3 p0 = new Vector3(0f, 1.0f, 0f);
            Vector3 p1 = new Vector3(0f, 1.2f, 0.25f);
            Vector3 v0 = new Vector3(0f, 15f, 20f);
            Vector3 v1 = new Vector3(0f, 25f, 35f);
            Quaternion q0 = Quaternion.identity;
            Quaternion q1 = Quaternion.Euler(20f, 0f, 0f);

            SweptRacketCollider.EvaluateHermiteSpline(
                0f, dt, p0, p1, v0, v1, q0, q1, Vector3.zero, Vector3.right,
                out Vector3 pStart, out Vector3 vStart, out _, out _);

            SweptRacketCollider.EvaluateHermiteSpline(
                1f, dt, p0, p1, v0, v1, q0, q1, Vector3.zero, Vector3.right,
                out Vector3 pEnd, out Vector3 vEnd, out _, out _);

            Assert.AreEqual(p0.z, pStart.z, Epsilon);
            Assert.AreEqual(p1.z, pEnd.z, Epsilon);
            Assert.AreEqual(v0.z, vStart.z, 0.05f);
            Assert.AreEqual(v1.z, vEnd.z, 0.05f);
        }

        [Test]
        public void TennisWallController_SmartReturnFunnel_ClampsReboundWithinRoomScaleReach()
        {
            Vector3 wallImpact = new Vector3(1.8f, 1.4f, 12f);
            // Wide angled rebound that would normally land far outside +/- 1.2m reach
            Vector3 wideReboundVel = new Vector3(4.5f, 3.0f, -15f);
            Vector3 homeCenter = Vector3.zero;
            float maxReach = 1.20f;

            Vector3 funneledVel = TennisWallController.ComputeSmartFunnelVelocity(
                wallImpact,
                wideReboundVel,
                homeCenter,
                0.15f,
                maxReach);

            // Preserve total rebound speed
            Assert.AreEqual(wideReboundVel.magnitude, funneledVel.magnitude, 0.01f);

            // Predict X coordinate when ball crosses baseline z = 0
            float timeToBaseline = Mathf.Abs((homeCenter.z - wallImpact.z) / funneledVel.z);
            float arrivalX = wallImpact.x + funneledVel.x * timeToBaseline;

            Assert.LessOrEqual(Mathf.Abs(arrivalX), maxReach + 0.15f,
                "Smart Return Funnel must steer off-angle rebounds back within the player's room-scale strike zone.");
        }

        [Test]
        public void RacketHapticsController_SweetSpotVsFrameMishit_UsesCanonicalFrequenciesAndDurations()
        {
            ImpactData sweetSpotImpact = new ImpactData
            {
                IsFrameHit = false,
                SweetSpotAccuracy = 0.95f,
                RelativeVelocityMagnitude = 24f
            };

            HapticPulseDescriptor sweetPulse = RacketHapticsController.BuildImpactHapticPulse(
                sweetSpotImpact, XRNode.RightHand);

            Assert.AreEqual(HapticEventType.SweetSpotHit, sweetPulse.EventType);
            Assert.AreEqual(200f, sweetPulse.FrequencyHz, Epsilon);
            Assert.AreEqual(0.015f, sweetPulse.DurationSeconds, Epsilon);

            ImpactData frameImpact = new ImpactData
            {
                IsFrameHit = true,
                SweetSpotAccuracy = 0.10f,
                RelativeVelocityMagnitude = 24f
            };

            HapticPulseDescriptor framePulse = RacketHapticsController.BuildImpactHapticPulse(
                frameImpact, XRNode.RightHand);

            Assert.AreEqual(HapticEventType.FrameMishit, framePulse.EventType);
            Assert.AreEqual(75f, framePulse.FrequencyHz, Epsilon);
            Assert.AreEqual(0.035f, framePulse.DurationSeconds, Epsilon);
        }

        [Test]
        public void BallMachineController_DrillFeeds_NeverExceedRoomScaleOrGuardianReach()
        {
            float maxReach = BallMachineController.ComputeConstrainedLateralReach(1.20f, 2.20f);
            Assert.LessOrEqual(maxReach, 1.20f);

            for (int step = 0; step < 12; step++)
            {
                BallFeedParameters feed = BallMachineController.BuildDrillFeedParameters(
                    BallMachineDrillPreset.RandomRoomScaleOscillation,
                    step,
                    maxReach);

                Assert.LessOrEqual(Mathf.Abs(feed.TargetLateralOffsetMeters), maxReach + Epsilon);
            }
        }

        [Test]
        public void ServeTossController_OcclusionCompensationAndServiceBoxEvaluation_AreAccurate()
        {
            // 1. Behind-the-head detection during trophy racket drop
            Vector3 hmdPos = new Vector3(0f, 1.75f, 0f);
            Vector3 hmdFwd = Vector3.forward;
            Vector3 racketBehindBack = new Vector3(0.15f, 1.60f, -0.28f);

            Assert.IsTrue(ServeTossController.IsRacketInOverheadOcclusionZone(hmdPos, hmdFwd, racketBehindBack));

            // 2. Deuce service box evaluation (lands inside left service box [-4.115m, 0m] x [11.887m, 18.288m])
            ImpactData serveHit = new ImpactData
            {
                ImpactPoint = new Vector3(1.0f, 2.65f, 0.2f),
                OutgoingSpeedKmh = 185f,
                OutgoingSpinRpm = 2900f,
                SpinAxis = new Vector3(-0.85f, 0.52f, 0f).normalized
            };

            Vector2 tCone = ServeTossController.GetTargetConeCourtPosition(ServeCourtSide.Deuce, ServeTargetCone.T);
            Vector3 validLanding = new Vector3(tCone.x, 0f, tCone.y);

            ServeTelemetryResult telemetry = ServeTossController.EvaluateServeLanding(
                validLanding,
                serveHit,
                ServeCourtSide.Deuce,
                ServeTargetCone.T,
                2.85f,
                true);

            Assert.IsFalse(telemetry.IsNetFault);
            Assert.IsTrue(telemetry.IsInServiceBox);
            Assert.IsTrue(telemetry.HitTargetCone);
            Assert.AreEqual(ServeSpinClassification.Kick, telemetry.ServeType);
        }

        [Test]
        public void TennisCourtSceneBuilder_BuildsWiredTrainingRig_AndExecutesWallRally()
        {
            TennisSimulationBootstrap rig = TennisCourtSceneBuilder.BuildWiredTrainingRig(
                WallDistancePreset.BaselineGroundstrokes12m);

            Assert.IsNotNull(rig.ActiveBall);
            Assert.IsNotNull(rig.TennisWall);
            Assert.AreEqual(TennisPhysicsConfig.FixedDeltaTime, Time.fixedDeltaTime, 0.00001f);

            // Switch to Tennis Wall mode and simulate a shot crossing the 12m backboard inside the Center_Control target zone
            rig.SwitchTrainingMode(TennisTrainingMode.TennisWall);
            Assert.AreEqual(TennisTrainingMode.TennisWall, rig.ActiveMode);

            BallAerodynamics ball = rig.ActiveBall;
            ball.LaunchWithTennisParameters(95f, new Vector3(0f, 0.08f, 1f), 2000f, 0f);

            Vector3 preImpactPos = new Vector3(0.1f, 1.50f, 11.80f);
            Vector3 postImpactPos = new Vector3(0.1f, 1.52f, 12.05f);

            bool hitWall = rig.TennisWall.CheckAndResolveWallCollision(preImpactPos, postImpactPos, ball);

            Assert.IsTrue(hitWall, "Ball crossing the 12m backboard plane must register a valid wall hit.");
            Assert.AreEqual(1, rig.TennisWall.CurrentRallyStreak);
            Assert.AreEqual(1, rig.TennisWall.TargetZoneHits);
            Assert.AreEqual(100, rig.TennisWall.TotalScore);
            Assert.Less(ball.Velocity.z, 0f, "Ball must rebound back toward the player (-Z).");
        }

        [Test]
        public void TrainingHudAndTrajectoryArc_FormatTelemetryAndPredictMagnusArcAccurately()
        {
            TrainingHudController hud = new TrainingHudController();

            ImpactData sweetHit = new ImpactData
            {
                OutgoingSpeedKmh = 142.4f,
                OutgoingSpinRpm = 2650f,
                SpinAxis = Vector3.right,
                SweetSpotAccuracy = 0.94f,
                IsFrameHit = false
            };

            hud.HandleRacketImpact(sweetHit);
            Assert.AreEqual("142 km/h | 2650 RPM (Topspin)", hud.LatestSnapshot.FormattedRadarReadout);
            Assert.AreEqual(94f, hud.LatestSnapshot.SweetSpotPercent, Epsilon);

            // Predict 60 frames (0.5s) of forward flight with topspin vs slice and verify topspin dips lower
            TennisPhysicsConfig cfg = TennisPhysicsConfig.CreateDefault();
            Vector3 startPos = new Vector3(0f, 1.2f, 0f);
            Vector3 startVel = new Vector3(0f, 3.5f, 24f);
            float omegaRad = 2800f * (2f * Mathf.PI / 60f);

            var topspinArc = TrajectoryArcRenderer.PredictFlightArc(startPos, startVel, new Vector3(omegaRad, 0f, 0f), cfg, 45);
            var sliceArc = TrajectoryArcRenderer.PredictFlightArc(startPos, startVel, new Vector3(-omegaRad, 0f, 0f), cfg, 45);

            Vector3 topEnd = topspinArc[topspinArc.Count - 1];
            Vector3 sliceEnd = sliceArc[sliceArc.Count - 1];

            Assert.Less(topEnd.y, sliceEnd.y, "Predicted topspin arc must dip lower than predicted slice arc.");
        }
    }
}

