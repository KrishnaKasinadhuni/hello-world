using System;
using UnityEngine;
using QuestTennisTrainer.Core;

namespace QuestTennisTrainer.Physics
{
    /// <summary>
    /// Court surface types with distinct normal restitution and tangential friction coefficients.
    /// </summary>
    public enum CourtSurfaceType
    {
        HardCourt,
        Clay,
        Grass,
        ConcreteWall
    }

    /// <summary>
    /// Pure C# and Unity-compatible aerodynamic simulation for an ITF regulation tennis ball.
    /// Solves gravity, aerodynamic drag, Magnus spin forces, spin decay, and court/wall rebounds.
    /// Operates at 120 Hz (FixedDeltaTime = 0.008333s) with sub-stepping support.
    /// </summary>
    public class BallAerodynamics : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TennisPhysicsConfig config = TennisPhysicsConfig.CreateDefault();
        [SerializeField] private CourtSurfaceType currentCourtSurface = CourtSurfaceType.HardCourt;

        [Header("Runtime State")]
        [SerializeField] private Vector3 velocity = Vector3.zero;
        [SerializeField] private Vector3 angularVelocity = Vector3.zero; // radians/second (right-hand rule)
        [SerializeField] private bool isSimulating = true;

        [Header("Ground & Wall Limits")]
        [SerializeField] private float courtFloorY = 0f;
        [SerializeField] private bool enableFloorBounce = true;

        // --- ITF Ball Physical Constants ---
        private const float BallMass = TennisPhysicsConfig.BallMassKg;             // 0.0577 kg
        private const float BallRadius = TennisPhysicsConfig.BallRadiusMeters;     // 0.0335 m
        private const float AirDensity = TennisPhysicsConfig.AirDensityKgPerM3;     // 1.204 kg/m^3
        private const float Gravity = TennisPhysicsConfig.GravityMps2;             // 9.81 m/s^2
        private static readonly float BallCrossSectionArea = Mathf.PI * BallRadius * BallRadius; // ~0.003525 m^2
        private static readonly float BallMomentOfInertia = (2f / 3f) * BallMass * BallRadius * BallRadius; // Hollow sphere

        // --- Spin Decay Constant ---
        private const float SpinAirDamping = 0.05f; // Exponential decay rate per second

        // --- Events ---
        public event Action<Vector3, Vector3, CourtSurfaceType> OnCourtBounce; // position, normal, surface
        public event Action<Vector3, Vector3> OnWallBounce;                   // position, normal

        // --- Public Properties & Telemetry ---
        public Vector3 Velocity
        {
            get => velocity;
            set => velocity = value;
        }

        public Vector3 AngularVelocity
        {
            get => angularVelocity;
            set => angularVelocity = value;
        }

        public TennisPhysicsConfig Config
        {
            get => config;
            set => config = value;
        }

        public CourtSurfaceType CurrentCourtSurface
        {
            get => currentCourtSurface;
            set => currentCourtSurface = value;
        }

        public bool IsSimulating
        {
            get => isSimulating;
            set => isSimulating = value;
        }

        public float SpeedMps => velocity.magnitude;
        public float SpeedKmh => velocity.magnitude * 3.6f;
        public float SpinRpm => angularVelocity.magnitude * (60f / (2f * Mathf.PI));

        private void Awake()
        {
            if (config.DragCoefficient <= 0f)
            {
                config = TennisPhysicsConfig.CreateDefault();
            }
        }

        private void FixedUpdate()
        {
            if (!isSimulating) return;

            float dt = Time.fixedDeltaTime;
            Vector3 pos = transform.position;

            StepSimulation(ref pos, ref velocity, ref angularVelocity, config, dt);

            // Ground floor collision check
            if (enableFloorBounce && pos.y - BallRadius <= courtFloorY && velocity.y < 0f)
            {
                pos.y = courtFloorY + BallRadius;
                ResolveSurfaceBounce(ref velocity, ref angularVelocity, Vector3.up, currentCourtSurface, config);
                OnCourtBounce?.Invoke(pos, Vector3.up, currentCourtSurface);
            }

            transform.position = pos;
        }

        /// <summary>
        /// Integrates aerodynamic forces, spin decay, and updates position and velocities for a given time delta.
        /// </summary>
        public static void StepSimulation(
            ref Vector3 position,
            ref Vector3 velocity,
            ref Vector3 angularVelocity,
            TennisPhysicsConfig cfg,
            float dt)
        {
            float speed = velocity.magnitude;
            if (speed < 0.001f && angularVelocity.sqrMagnitude < 0.001f)
            {
                // Ball is essentially at rest
                velocity += Vector3.down * (Gravity * dt);
                position += velocity * dt;
                return;
            }

            // 1. Gravity Force: F_g = m * g (downwards)
            Vector3 forceGravity = Vector3.down * (BallMass * Gravity);

            // 2. Aerodynamic Drag: F_d = -0.5 * rho * A * C_d * |v| * v
            Vector3 forceDrag = ComputeDragForce(velocity, cfg.DragCoefficient);

            // 3. Magnus Effect Lift: F_m = 0.5 * rho * A * C_L * |v| * (v x omega_hat)
            Vector3 forceMagnus = ComputeMagnusForce(velocity, angularVelocity, cfg.MagnusLiftScale);

            // Total acceleration: a = F_total / m
            Vector3 totalForce = forceGravity + forceDrag + forceMagnus;
            Vector3 acceleration = totalForce / BallMass;

            // Semi-implicit Euler integration (stable at 120 Hz)
            velocity += acceleration * dt;
            position += velocity * dt;

            // 4. Spin Air Resistance Decay: omega(t + dt) = omega(t) * (1 - damping * dt)
            float spinDampFactor = Mathf.Clamp01(1f - SpinAirDamping * dt);
            angularVelocity *= spinDampFactor;
        }

        /// <summary>
        /// Calculates aerodynamic drag opposing the direction of flight.
        /// Formula: F_d = -0.5 * rho * A * C_d * |v| * v
        /// </summary>
        public static Vector3 ComputeDragForce(Vector3 vel, float dragCoeff)
        {
            float speed = vel.magnitude;
            if (speed < 0.001f) return Vector3.zero;

            float dynamicPressure = 0.5f * AirDensity * speed * speed;
            float dragMagnitude = dynamicPressure * BallCrossSectionArea * dragCoeff;
            return -vel.normalized * dragMagnitude;
        }

        /// <summary>
        /// Calculates the Magnus lift force generated by ball spin and air velocity.
        /// Formula: F_m = 0.5 * rho * A * C_L * |v| * (omega_hat x v)
        /// In Unity coordinates (+X right, +Y up, +Z forward), topspin has omega along +X
        /// (since omega x r_top = (+1,0,0) x (0,r,0) = (0,0,+r) forward), so (omega_hat x v)
        /// points along (0, -1, 0), dipping the ball downward onto the court.
        /// </summary>
        public static Vector3 ComputeMagnusForce(Vector3 vel, Vector3 angVel, float liftScale)
        {
            float speed = vel.magnitude;
            float spinMag = angVel.magnitude;

            if (speed < 0.05f || spinMag < 0.1f) return Vector3.zero;

            // Spin ratio: S = (r * |w|) / |v|
            float spinRatio = (BallRadius * spinMag) / speed;

            // Lift coefficient scales with spin parameter and saturates at high RPM
            float effectiveCl = liftScale * Mathf.Min(spinRatio, 2.5f);

            // Force vector: direction is along (angVel x vel) per handoff equation
            Vector3 cross = Vector3.Cross(angVel, vel);
            if (cross.sqrMagnitude < 0.0001f) return Vector3.zero;

            Vector3 liftDirection = cross.normalized;
            float dynamicPressure = 0.5f * AirDensity * speed * speed;
            float magnusMagnitude = dynamicPressure * BallCrossSectionArea * effectiveCl;

            return liftDirection * magnusMagnitude;
        }

        /// <summary>
        /// Solves normal restitution and tangential spin-friction exchange upon surface impact.
        /// </summary>
        public static void ResolveSurfaceBounce(
            ref Vector3 vel,
            ref Vector3 angVel,
            Vector3 normal,
            CourtSurfaceType surface,
            TennisPhysicsConfig cfg)
        {
            normal = normal.normalized;
            float vn = Vector3.Dot(vel, normal);

            // Ignore if ball is already moving away from surface
            if (vn >= 0f) return;

            // Surface-specific restitution and friction coefficients
            float eN;
            float frictionMu;
            GetSurfaceCoefficients(surface, out eN, out frictionMu);

            // 1. Normal rebound: v_n' = -e_n * v_n
            Vector3 velNormal = vn * normal;
            Vector3 velTangential = vel - velNormal;
            Vector3 newVelNormal = -eN * velNormal;

            // 2. Relative velocity at contact point including spin:
            // Point of contact offset from center: r_contact = -normal * BallRadius
            Vector3 rContact = -normal * BallRadius;
            Vector3 surfaceSpinVel = Vector3.Cross(angVel, rContact);
            Vector3 contactSlipVel = velTangential + surfaceSpinVel;

            Vector3 newVelTangential;
            Vector3 deltaAngVel;

            if (contactSlipVel.sqrMagnitude > 0.001f)
            {
                Vector3 slipDir = contactSlipVel.normalized;

                // Normal impulse magnitude: J_n = m * (1 + eN) * |v_n|
                float jNormal = BallMass * (1f + eN) * Mathf.Abs(vn);

                // Tangential impulse limited by Coulomb friction: J_t <= mu * J_n
                // Maximum impulse needed to achieve pure rolling: J_roll = |contactSlipVel| / (1/m + r^2/I)
                float effMassInv = (1f / BallMass) + (BallRadius * BallRadius / BallMomentOfInertia);
                float jRoll = contactSlipVel.magnitude / effMassInv;

                float jTangential = Mathf.Min(frictionMu * jNormal, jRoll);
                Vector3 frictionImpulse = -slipDir * jTangential;

                newVelTangential = velTangential + (frictionImpulse / BallMass);

                // Torque impulse on spin: Delta_omega = (r_contact x J_friction) / I
                Vector3 torqueImpulse = Vector3.Cross(rContact, frictionImpulse);
                deltaAngVel = torqueImpulse / BallMomentOfInertia;
            }
            else
            {
                newVelTangential = velTangential;
                deltaAngVel = Vector3.zero;
            }

            vel = newVelNormal + newVelTangential;
            angVel += deltaAngVel;
        }

        /// <summary>
        /// Resolves an impact with a vertical wall (e.g. Tennis Practice Wall)
        /// with smart funnel blending towards a home center marker.
        /// </summary>
        public void HandleWallImpact(Vector3 wallPoint, Vector3 wallNormal, float smartFunnelBias = 0f, Vector3? homeCenter = null)
        {
            ResolveSurfaceBounce(ref velocity, ref angularVelocity, wallNormal, CourtSurfaceType.ConcreteWall, config);

            // Apply Smart Funnel bias to maintain rallies within room-scale boundary
            if (smartFunnelBias > 0f && homeCenter.HasValue)
            {
                Vector3 toHome = (homeCenter.Value - wallPoint).normalized;
                // Blend lateral rebound direction towards home center
                Vector3 currentDir = velocity.normalized;
                Vector3 blendedDir = Vector3.Lerp(currentDir, toHome, smartFunnelBias).normalized;
                velocity = blendedDir * velocity.magnitude;
            }

            OnWallBounce?.Invoke(wallPoint, wallNormal);
        }

        /// <summary>
        /// Returns restitution and friction coefficients for each surface type.
        /// </summary>
        public static void GetSurfaceCoefficients(CourtSurfaceType surface, out float restitution, out float friction)
        {
            switch (surface)
            {
                case CourtSurfaceType.Clay:
                    restitution = 0.72f;
                    friction = 0.75f;
                    break;
                case CourtSurfaceType.Grass:
                    restitution = 0.68f;
                    friction = 0.45f;
                    break;
                case CourtSurfaceType.ConcreteWall:
                    restitution = 0.80f;
                    friction = 0.50f;
                    break;
                case CourtSurfaceType.HardCourt:
                default:
                    restitution = 0.75f;
                    friction = 0.60f;
                    break;
            }
        }

        /// <summary>
        /// Launches the ball with a specific linear velocity and spin vector.
        /// </summary>
        public void Launch(Vector3 initialVelocity, Vector3 initialAngularVelocity)
        {
            velocity = initialVelocity;
            angularVelocity = initialAngularVelocity;
            isSimulating = true;
        }

        /// <summary>
        /// Launches the ball using human-readable tennis parameters.
        /// </summary>
        /// <param name="speedKmh">Speed in km/h</param>
        /// <param name="direction">Direction of launch</param>
        /// <param name="topspinRpm">Topspin (+) or slice (-) in RPM</param>
        /// <param name="sidespinRpm">Sidespin in RPM</param>
        public void LaunchWithTennisParameters(float speedKmh, Vector3 direction, float topspinRpm, float sidespinRpm)
        {
            direction = direction.normalized;
            float speedMps = speedKmh / 3.6f;
            velocity = direction * speedMps;

            // Determine local racket/court axes
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            Vector3 up = Vector3.Cross(direction, right).normalized;

            // Topspin axis is +right in Unity's coordinate system (omega x r_top = +forward)
            float radSecPerRpm = (2f * Mathf.PI) / 60f;
            Vector3 topspinVector = right * (topspinRpm * radSecPerRpm);
            Vector3 sidespinVector = up * (sidespinRpm * radSecPerRpm);

            angularVelocity = topspinVector + sidespinVector;
            isSimulating = true;
        }
    }
}
