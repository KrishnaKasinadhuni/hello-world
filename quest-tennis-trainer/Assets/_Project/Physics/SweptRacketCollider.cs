using System;
using UnityEngine;
using QuestTennisTrainer.Core;

namespace QuestTennisTrainer.Physics
{
    /// <summary>
    /// Telemetry and haptic parameters exported after every racket-ball collision.
    /// </summary>
    [Serializable]
    public struct ImpactData
    {
        public Vector3 ImpactPoint;
        public Vector3 ImpactNormal;
        public float ImpactSpeedKmh;
        public float OutgoingSpeedKmh;
        public float OutgoingSpinRpm;
        public Vector3 SpinAxis;
        public float SweetSpotAccuracy; // 1.0 = dead center, 0.0 = outer frame edge
        public bool IsFrameHit;
        public float RelativeVelocityMagnitude;
    }

    /// <summary>
    /// Sub-frame swept racket collision solver running at 120 Hz with 10 sub-steps (~0.83 ms resolution).
    /// Uses a cubic Hermite spline to interpolate controller position and velocity between physics frames,
    /// preventing ball tunneling during high-speed swings and serves (up to 160+ km/h).
    /// Computes oblique stringbed impact restitution, tangential friction, and topspin/slice/kick spin generation.
    /// </summary>
    public class SweptRacketCollider : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TennisPhysicsConfig config = TennisPhysicsConfig.CreateDefault();

        [Header("Racket Dimensions (Meters)")]
        [Tooltip("Offset from the controller grip to the stringbed sweet spot")]
        [SerializeField] private Vector3 sweetSpotOffset = new Vector3(0f, 0.45f, 0f);
        [Tooltip("Half-width of stringbed oval")]
        [SerializeField] private float headRadiusX = 0.13f; // 26 cm head width
        [Tooltip("Half-height of stringbed oval")]
        [SerializeField] private float headRadiusY = 0.16f; // 32 cm head height
        [Tooltip("Outer margin where shots transition to frame mishits")]
        [SerializeField] private float frameMishitThreshold = 0.85f;

        [Header("Ball References")]
        [SerializeField] private BallAerodynamics targetBall;

        // --- Pose History for Hermite Spline Interpolation ---
        private Vector3 prevGripPosition;
        private Quaternion prevGripRotation;
        private Vector3 prevGripVelocity;
        private Vector3 prevGripAngularVelocity;

        private Vector3 currentGripPosition;
        private Quaternion currentGripRotation;
        private Vector3 currentGripVelocity;
        private Vector3 currentGripAngularVelocity;

        private bool hasValidPrevPose = false;

        // --- Events ---
        public event Action<ImpactData> OnRacketHit;

        // --- Public Properties ---
        public TennisPhysicsConfig Config
        {
            get => config;
            set => config = value;
        }

        public Vector3 SweetSpotWorldPosition => transform.TransformPoint(sweetSpotOffset);
        public Vector3 HeadNormal => transform.forward;
        public Vector3 HeadUp => transform.up;
        public Vector3 HeadRight => transform.right;

        private void Awake()
        {
            if (config.DragCoefficient <= 0f)
            {
                config = TennisPhysicsConfig.CreateDefault();
            }
        }

        private void Start()
        {
            InitializePose(transform.position, transform.rotation);
        }

        public void InitializePose(Vector3 pos, Quaternion rot)
        {
            prevGripPosition = pos;
            prevGripRotation = rot;
            prevGripVelocity = Vector3.zero;
            prevGripAngularVelocity = Vector3.zero;

            currentGripPosition = pos;
            currentGripRotation = rot;
            currentGripVelocity = Vector3.zero;
            currentGripAngularVelocity = Vector3.zero;

            hasValidPrevPose = true;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (dt <= 0.0001f) dt = TennisPhysicsConfig.FixedDeltaTime;

            // Fetch controller velocity and angular velocity (from XR controller or calculated differential)
            UpdateControllerPose(dt);

            if (targetBall != null && targetBall.IsSimulating)
            {
                CheckAndResolveCollision(targetBall, dt);
            }
        }

        /// <summary>
        /// Updates the current controller pose and derives linear and angular velocities.
        /// Can be driven by OVRInput or by Transform differentiation.
        /// </summary>
        private void UpdateControllerPose(float dt)
        {
            if (!hasValidPrevPose)
            {
                InitializePose(transform.position, transform.rotation);
                return;
            }

            prevGripPosition = currentGripPosition;
            prevGripRotation = currentGripRotation;
            prevGripVelocity = currentGripVelocity;
            prevGripAngularVelocity = currentGripAngularVelocity;

            currentGripPosition = transform.position;
            currentGripRotation = transform.rotation;

            // Calculate finite difference velocities as robust default
            currentGripVelocity = (currentGripPosition - prevGripPosition) / dt;

            Quaternion deltaRot = currentGripRotation * Quaternion.Inverse(prevGripRotation);
            deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
            if (angleDeg > 180f) angleDeg -= 360f;
            currentGripAngularVelocity = axis.normalized * (angleDeg * Mathf.Deg2Rad / dt);
        }

        /// <summary>
        /// Explicitly injects controller poses for automated testing and simulation outside VR headset.
        /// </summary>
        public void SetPoseExplicit(
            Vector3 prevPos, Quaternion prevRot, Vector3 prevVel, Vector3 prevAngVel,
            Vector3 currPos, Quaternion currRot, Vector3 currVel, Vector3 currAngVel)
        {
            prevGripPosition = prevPos;
            prevGripRotation = prevRot;
            prevGripVelocity = prevVel;
            prevGripAngularVelocity = prevAngVel;

            currentGripPosition = currPos;
            currentGripRotation = currRot;
            currentGripVelocity = currVel;
            currentGripAngularVelocity = currAngVel;

            hasValidPrevPose = true;
        }

        /// <summary>
        /// Evaluates 10 sub-steps along the cubic Hermite spline to detect ball contact and calculate spin restitution.
        /// </summary>
        public bool CheckAndResolveCollision(BallAerodynamics ball, float dt)
        {
            if (ball == null) return false;

            int subSteps = TennisPhysicsConfig.CollisionSubSteps;
            float ballRadius = TennisPhysicsConfig.BallRadiusMeters;

            Vector3 ballPosStart = ball.transform.position;
            Vector3 ballVel = ball.Velocity;
            Vector3 ballAngVel = ball.AngularVelocity;

            // Swept ball position along frame
            Vector3 ballPosEnd = ballPosStart + ballVel * dt;

            for (int i = 1; i <= subSteps; i++)
            {
                float u = (float)i / subSteps;

                // 1. Evaluate Cubic Hermite Spline for Grip Pose and Velocities
                EvaluateHermiteSpline(u, dt,
                    prevGripPosition, currentGripPosition, prevGripVelocity, currentGripVelocity,
                    prevGripRotation, currentGripRotation, prevGripAngularVelocity, currentGripAngularVelocity,
                    out Vector3 gripPosU, out Vector3 gripVelU, out Quaternion gripRotU, out Vector3 gripAngVelU);

                // 2. Evaluate Swept Ball Position at Sub-Step
                Vector3 ballPosU = Vector3.Lerp(ballPosStart, ballPosEnd, u);

                // 3. Transform ball position into racket head local space
                Vector3 sweetSpotU = gripPosU + (gripRotU * sweetSpotOffset);
                Vector3 headNormalU = gripRotU * Vector3.forward;
                Vector3 headRightU = gripRotU * Vector3.right;
                Vector3 headUpU = gripRotU * Vector3.up;

                Vector3 relBallPos = ballPosU - sweetSpotU;
                float localZ = Vector3.Dot(relBallPos, headNormalU);
                float localX = Vector3.Dot(relBallPos, headRightU);
                float localY = Vector3.Dot(relBallPos, headUpU);

                // 4. Check if ball intersects stringbed plane within ball radius margin
                if (Mathf.Abs(localZ) <= ballRadius)
                {
                    // Normalized elliptical radius on string face
                    float normX = localX / headRadiusX;
                    float normY = localY / headRadiusY;
                    float radialDistance = Mathf.Sqrt(normX * normX + normY * normY);

                    // Allow contact within stringbed and outer frame bevel (up to 1.15)
                    if (radialDistance <= 1.15f)
                    {
                        // 5. Point velocity of the stringbed at the impact location
                        Vector3 impactWorldPoint = sweetSpotU + (headRightU * localX) + (headUpU * localY);
                        Vector3 racketPointArm = impactWorldPoint - gripPosU;
                        Vector3 racketPointVel = gripVelU + Vector3.Cross(gripAngVelU, racketPointArm);

                        // 6. Relative velocity of ball relative to racket point
                        Vector3 relVel = ballVel - racketPointVel;

                        // Contact normal points against the incoming ball
                        Vector3 contactNormal = (localZ >= 0f) ? headNormalU : -headNormalU;
                        float normalApproachSpeed = Vector3.Dot(relVel, contactNormal);

                        // Only resolve if ball and racket are approaching each other
                        if (normalApproachSpeed < 0f)
                        {
                            ResolveStringbedImpact(
                                ball, impactWorldPoint, contactNormal,
                                normalApproachSpeed, relVel, racketPointVel,
                                radialDistance, headUpU, headRightU);

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Computes normal rebound restitution and tangential spin brush for topspin, slice, and kick serves.
        /// </summary>
        private void ResolveStringbedImpact(
            BallAerodynamics ball,
            Vector3 impactPoint,
            Vector3 normal,
            float normalApproachSpeed,
            Vector3 relVel,
            Vector3 racketPointVel,
            float radialDistance,
            Vector3 headUp,
            Vector3 headRight)
        {
            float incomingSpeedKmh = relVel.magnitude * 3.6f;

            // 1. Sweet Spot Restitution Calculation
            bool isFrameHit = radialDistance >= frameMishitThreshold;
            float sweetSpotAccuracy = Mathf.Clamp01(1f - radialDistance);
            float cor = Mathf.Lerp(config.SweetSpotRestitution, config.FrameMishitRestitution, Mathf.Clamp01(radialDistance));

            // Normal rebound velocity: v_n' = -cor * v_n
            Vector3 normalReboundVel = normal * (-cor * normalApproachSpeed);

            // 2. Tangential Brush Velocity (decomposed into longitudinal and lateral string axes)
            Vector3 tangentialVel = relVel - (normal * normalApproachSpeed);
            Vector3 brushVel = -tangentialVel; // Direction racket is brushing across ball surface

            float brushUp = Vector3.Dot(brushVel, headUp);       // Upward brush -> Topspin (+), Downward -> Slice (-)
            float brushRight = Vector3.Dot(brushVel, headRight); // Lateral brush -> Sidespin / Kick

            // Tangential friction coefficient controls spin bite
            float friction = config.StringTangentialFriction;
            if (isFrameHit) friction *= 0.5f; // Frame has less friction bite

            // Outgoing tangential linear velocity
            Vector3 tangentialReboundVel = tangentialVel * (1f - friction * 0.45f);

            // Total outgoing linear velocity of ball
            Vector3 outgoingVelocity = racketPointVel + normalReboundVel + tangentialReboundVel;

            // 3. Outgoing Angular Velocity (Spin Generation)
            // Upward brush generates spin around -headRight (topspin curls top forward)
            // Rightward brush generates spin around +headUp (kick / sidespin)
            float ballRadius = TennisPhysicsConfig.BallRadiusMeters;
            float ballMass = TennisPhysicsConfig.BallMassKg;
            float ballMoI = (2f / 3f) * ballMass * ballRadius * ballRadius;

            Vector3 tangentialImpulse = brushVel * (ballMass * friction * 0.75f);
            Vector3 spinTorqueImpulse = Vector3.Cross(-normal * ballRadius, tangentialImpulse);
            Vector3 deltaAngularVelocity = spinTorqueImpulse / ballMoI;

            // Blend existing ball spin with new brush-generated spin
            Vector3 outgoingAngularVelocity = (ball.AngularVelocity * 0.25f) + deltaAngularVelocity;

            // Clamp max physical spin RPM to realistic ATP/WTA maximums (~4,500 RPM)
            float maxRadSec = 4500f * (2f * Mathf.PI / 60f);
            if (outgoingAngularVelocity.magnitude > maxRadSec)
            {
                outgoingAngularVelocity = outgoingAngularVelocity.normalized * maxRadSec;
            }

            // Apply results to ball
            ball.Velocity = outgoingVelocity;
            ball.AngularVelocity = outgoingAngularVelocity;

            // Telemetry compilation
            ImpactData data = new ImpactData
            {
                ImpactPoint = impactPoint,
                ImpactNormal = normal,
                ImpactSpeedKmh = incomingSpeedKmh,
                OutgoingSpeedKmh = outgoingVelocity.magnitude * 3.6f,
                OutgoingSpinRpm = outgoingAngularVelocity.magnitude * (60f / (2f * Mathf.PI)),
                SpinAxis = outgoingAngularVelocity.normalized,
                SweetSpotAccuracy = sweetSpotAccuracy,
                IsFrameHit = isFrameHit,
                RelativeVelocityMagnitude = relVel.magnitude
            };

            OnRacketHit?.Invoke(data);
        }

        /// <summary>
        /// Evaluates cubic Hermite spline interpolation for smooth controller position and velocity between 120 Hz ticks.
        /// </summary>
        public static void EvaluateHermiteSpline(
            float u,
            float dt,
            Vector3 p0, Vector3 p1, Vector3 v0, Vector3 v1,
            Quaternion q0, Quaternion q1, Vector3 w0, Vector3 w1,
            out Vector3 pOut, out Vector3 vOut, out Quaternion qOut, out Vector3 wOut)
        {
            u = Mathf.Clamp01(u);
            float u2 = u * u;
            float u3 = u2 * u;

            // Cubic Hermite basis functions
            float h00 = 2f * u3 - 3f * u2 + 1f;
            float h10 = u3 - 2f * u2 + u;
            float h01 = -2f * u3 + 3f * u2;
            float h11 = u3 - u2;

            // Position: P(u) = h00*P0 + h10*(V0*dt) + h01*P1 + h11*(V1*dt)
            pOut = h00 * p0 + h10 * (v0 * dt) + h01 * p1 + h11 * (v1 * dt);

            // Velocity: dP/du * (1/dt)
            float dh00 = 6f * u2 - 6f * u;
            float dh10 = 3f * u2 - 4f * u + 1f;
            float dh01 = -6f * u2 + 6f * u;
            float dh11 = 3f * u2 - 2f * u;

            vOut = (dh00 * p0 + dh10 * (v0 * dt) + dh01 * p1 + dh11 * (v1 * dt)) / Mathf.Max(dt, 0.0001f);

            // Slerp for orientation and lerp for angular velocity
            qOut = Quaternion.Slerp(q0, q1, u);
            wOut = Vector3.Lerp(w0, w1, u);
        }
    }
}
