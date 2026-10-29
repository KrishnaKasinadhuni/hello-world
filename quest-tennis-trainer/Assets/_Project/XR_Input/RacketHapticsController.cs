using System;
using UnityEngine;
using UnityEngine.XR;
using QuestTennisTrainer.Physics;

namespace QuestTennisTrainer.XR_Input
{
    /// <summary>
    /// Categories of tactile feedback on Meta Quest 3 Touch Plus controllers.
    /// </summary>
    public enum HapticEventType
    {
        SweetSpotHit,
        FrameMishit,
        ServeTossRelease
    }

    /// <summary>
    /// Parametric haptic waveform descriptor for runtime playback and NUnit verification.
    /// </summary>
    [Serializable]
    public struct HapticPulseDescriptor
    {
        public HapticEventType EventType;
        public XRNode ControllerNode;
        public float FrequencyHz;
        public float DurationSeconds;
        public float Amplitude; // Normalized [0, 1]
    }

    /// <summary>
    /// Parametric haptics controller for Meta Quest 3 Touch Plus controllers.
    /// Differentiates crisp 200 Hz sweet-spot stringbed hits from 75 Hz frame mishits
    /// and 100 Hz micro-ticks on serve ball toss release.
    /// </summary>
    public class RacketHapticsController : MonoBehaviour
    {
        // --- Canonical Parametric Haptic Specifications (from Handoff Spec) ---
        public const float SweetSpotFrequencyHz = 200f;
        public const float SweetSpotDurationSeconds = 0.015f; // 15 ms

        public const float FrameMishitFrequencyHz = 75f;
        public const float FrameMishitDurationSeconds = 0.035f; // 35 ms

        public const float ServeTossFrequencyHz = 100f;
        public const float ServeTossDurationSeconds = 0.005f; // 5 ms
        public const float ServeTossDefaultAmplitude = 0.35f;

        [Header("Controller Assignment")]
        [SerializeField] private XRNode dominantRacketHand = XRNode.RightHand;
        [SerializeField] private XRNode nonDominantTossHand = XRNode.LeftHand;

        [Header("Physics & Audio References")]
        [SerializeField] private SweptRacketCollider racketCollider;
        [SerializeField] private AudioSource impactAudioSource;
        [SerializeField] private AudioClip sweetSpotAudioClip;
        [SerializeField] private AudioClip frameMishitAudioClip;

        [Header("Scaling Parameters")]
        [Tooltip("Relative ball-racket speed (m/s) that produces 100% haptic amplitude (~110 km/h)")]
        [SerializeField] private float maxReferenceImpactSpeedMps = 30f;
        [SerializeField] private float minImpactAmplitude = 0.20f;

        // --- Events ---
        public event Action<HapticPulseDescriptor> OnHapticPulseTriggered;

        // --- Public Properties ---
        public XRNode DominantRacketHand
        {
            get => dominantRacketHand;
            set => dominantRacketHand = value;
        }

        public XRNode NonDominantTossHand
        {
            get => nonDominantTossHand;
            set => nonDominantTossHand = value;
        }

        private void OnEnable()
        {
            if (racketCollider != null)
            {
                racketCollider.OnRacketHit += HandleRacketHit;
            }
        }

        private void OnDisable()
        {
            if (racketCollider != null)
            {
                racketCollider.OnRacketHit -= HandleRacketHit;
            }
        }

        /// <summary>
        /// Callback invoked by SweptRacketCollider whenever the ball impacts the stringbed or frame.
        /// </summary>
        public void HandleRacketHit(ImpactData impact)
        {
            HapticPulseDescriptor pulse = BuildImpactHapticPulse(
                impact,
                dominantRacketHand,
                maxReferenceImpactSpeedMps,
                minImpactAmplitude);

            DispatchHapticPulse(pulse);
            PlayImpactAudio(impact);
        }

        /// <summary>
        /// Triggers the subtle 100 Hz, 5 ms micro-tick on the non-dominant controller
        /// as the tennis ball leaves the player's fingertips during a serve toss.
        /// </summary>
        public HapticPulseDescriptor TriggerServeTossReleaseTick()
        {
            HapticPulseDescriptor pulse = new HapticPulseDescriptor
            {
                EventType = HapticEventType.ServeTossRelease,
                ControllerNode = nonDominantTossHand,
                FrequencyHz = ServeTossFrequencyHz,
                DurationSeconds = ServeTossDurationSeconds,
                Amplitude = ServeTossDefaultAmplitude
            };

            DispatchHapticPulse(pulse);
            return pulse;
        }

        /// <summary>
        /// Pure calculation method that maps ImpactData into a parametric HapticPulseDescriptor.
        /// Sweet-spot hits emit 200 Hz for 15 ms; frame mishits emit 75 Hz for 35 ms.
        /// </summary>
        public static HapticPulseDescriptor BuildImpactHapticPulse(
            ImpactData impact,
            XRNode controllerNode,
            float maxRefSpeedMps = 30f,
            float minAmplitude = 0.20f)
        {
            float normalizedSpeed = Mathf.Clamp01(impact.RelativeVelocityMagnitude / Mathf.Max(maxRefSpeedMps, 1f));
            float baseAmplitude = Mathf.Lerp(minAmplitude, 1.0f, normalizedSpeed);

            if (impact.IsFrameHit)
            {
                // Low-frequency jarring frame vibration (75 Hz, 35 ms)
                return new HapticPulseDescriptor
                {
                    EventType = HapticEventType.FrameMishit,
                    ControllerNode = controllerNode,
                    FrequencyHz = FrameMishitFrequencyHz,
                    DurationSeconds = FrameMishitDurationSeconds,
                    Amplitude = Mathf.Clamp01(baseAmplitude * 0.90f)
                };
            }

            // Crisp high-frequency sweet-spot pulse (200 Hz, 15 ms) boosted by center accuracy
            float accuracyBoost = Mathf.Lerp(0.85f, 1.0f, impact.SweetSpotAccuracy);
            return new HapticPulseDescriptor
            {
                EventType = HapticEventType.SweetSpotHit,
                ControllerNode = controllerNode,
                FrequencyHz = SweetSpotFrequencyHz,
                DurationSeconds = SweetSpotDurationSeconds,
                Amplitude = Mathf.Clamp01(baseAmplitude * accuracyBoost)
            };
        }

        /// <summary>
        /// Synthesizes a modulated PCM haptic buffer and dispatches it to the OpenXR / Quest 3 controller.
        /// Falls back to standard SendHapticImpulse if buffer playback is unsupported by the active runtime.
        /// </summary>
        private void DispatchHapticPulse(HapticPulseDescriptor pulse)
        {
            OnHapticPulseTriggered?.Invoke(pulse);

            InputDevice device = InputDevices.GetDeviceAtXRNode(pulse.ControllerNode);
            if (!device.isValid) return;

            if (device.TryGetHapticCapabilities(out HapticCapabilities capabilities))
            {
                if (capabilities.supportsBuffer && capabilities.bufferFrequencyHz > 0)
                {
                    byte[] pcmBuffer = SynthesizePcmWaveform(
                        pulse.FrequencyHz,
                        pulse.DurationSeconds,
                        pulse.Amplitude,
                        (int)capabilities.bufferFrequencyHz);

                    device.SendHapticBuffer(0u, pcmBuffer);
                    return;
                }

                if (capabilities.supportsImpulse)
                {
                    device.SendHapticImpulse(0u, pulse.Amplitude, pulse.DurationSeconds);
                }
            }
        }

        /// <summary>
        /// Generates an 8-bit unsigned PCM sine-envelope buffer at the target carrier frequency.
        /// </summary>
        public static byte[] SynthesizePcmWaveform(
            float frequencyHz,
            float durationSeconds,
            float amplitude,
            int sampleRateHz)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(durationSeconds * sampleRateHz));
            byte[] buffer = new byte[sampleCount];
            amplitude = Mathf.Clamp01(amplitude);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRateHz;
                float normalizedProgress = (float)i / sampleCount;

                // Percussive attack-decay envelope (sharp transient, smooth tail)
                float envelope = 1f - normalizedProgress;
                float carrier = Mathf.Abs(Mathf.Sin(2f * Mathf.PI * frequencyHz * t));
                float sampleValue = amplitude * envelope * carrier;

                buffer[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(sampleValue * 255f), 0, 255);
            }

            return buffer;
        }

        private void PlayImpactAudio(ImpactData impact)
        {
            if (impactAudioSource == null) return;

            AudioClip clip = impact.IsFrameHit ? frameMishitAudioClip : sweetSpotAudioClip;
            if (clip == null) return;

            // Frame mishits have dampened pitch and lower acoustic clarity
            impactAudioSource.pitch = impact.IsFrameHit
                ? UnityEngine.Random.Range(0.78f, 0.86f)
                : UnityEngine.Random.Range(0.96f, 1.05f);

            float volume = Mathf.Clamp(impact.RelativeVelocityMagnitude / maxReferenceImpactSpeedMps, 0.25f, 1.0f);
            if (impact.IsFrameHit) volume *= 0.65f;

            impactAudioSource.PlayOneShot(clip, volume);
        }
    }
}

