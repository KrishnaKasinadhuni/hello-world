using System;
using UnityEngine;
using QuestTennisTrainer.Core;
using QuestTennisTrainer.Physics;

namespace QuestTennisTrainer.XR_Input
{
    /// <summary>
    /// Acoustic profile descriptor returned when synthesizing a procedural tennis impact waveform.
    /// </summary>
    [Serializable]
    public struct ProceduralImpactAudioReport
    {
        public string SoundCategory;
        public int SampleRateHz;
        public int SampleCount;
        public float FundamentalFreqHz;
        public float PeakAmplitude;
        public float RmsEnergy;
        public float[] PcmSamples;
    }

    /// <summary>
    /// Session 13: Zero-asset procedural acoustic synthesizer for Quest 3 Tennis Trainer.
    /// Generates 44.1 kHz mono PCM waveforms for sweet-spot stringbed "pop", graphite frame "clank",
    /// and court/wall bounce thuds paired 1:1 with RacketHapticsController.
    /// </summary>
    public class RacketAudioSynthesizer : MonoBehaviour
    {
        public const int DefaultSampleRateHz = 44100;
        public const float SweetSpotFundamentalHz = 420f;
        public const float SweetSpotHarmonicHz = 1120f;
        public const float FrameClankPrimaryHz = 940f;
        public const float FrameClankSecondaryHz = 1610f;

        [Header("Spatial Audio Output")]
        [SerializeField] private AudioSource racketAudioSource;

        public ProceduralImpactAudioReport LastSynthesizedReport { get; private set; }

        /// <summary>
        /// Synthesizes and plays the appropriate impact sound (Sweet-Spot Pop vs Frame Clank) from a racket ImpactData event.
        /// </summary>
        public ProceduralImpactAudioReport PlayRacketImpactAudio(ImpactData impact)
        {
            float normalizedVelocity = Mathf.Clamp(impact.OutgoingSpeedKmh / 160f, 0.25f, 1.0f);

            ProceduralImpactAudioReport report = impact.IsFrameHit
                ? SynthesizeFrameClank(normalizedVelocity)
                : SynthesizeSweetSpotPop(normalizedVelocity, impact.SweetSpotAccuracy);

            LastSynthesizedReport = report;
            PlayReportOnAudioSource(report);
            return report;
        }

        /// <summary>
        /// Synthesizes a crisp sweet-spot stringbed "pop" (420 Hz fundamental + 1120 Hz stringbed overtone + 4ms felt transient).
        /// </summary>
        public static ProceduralImpactAudioReport SynthesizeSweetSpotPop(
            float normalizedVelocity,
            float sweetSpotAccuracy,
            int sampleRateHz = DefaultSampleRateHz)
        {
            float durationSec = 0.085f;
            int sampleCount = Mathf.Max(64, Mathf.RoundToInt(durationSec * sampleRateHz));
            float[] pcm = new float[sampleCount];

            float gain = Mathf.Clamp(normalizedVelocity, 0.2f, 1.0f) * Mathf.Lerp(0.75f, 1.0f, Mathf.Clamp01(sweetSpotAccuracy));
            float pitchShift = Mathf.Lerp(0.95f, 1.08f, Mathf.Clamp01(sweetSpotAccuracy));
            float f0 = SweetSpotFundamentalHz * pitchShift;
            float f1 = SweetSpotHarmonicHz * pitchShift;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRateHz;

                // Fast 1.5 ms attack envelope followed by exponential stringbed decay (tau = 16 ms)
                float attack = Mathf.Clamp01(t / 0.0015f);
                float bodyDecay = (float)Math.Exp(-t / 0.016f);
                float overtoneDecay = (float)Math.Exp(-t / 0.0065f);

                // Deterministic pseudo-noise burst during the first 4 ms for felt compression snap
                float transientEnv = (float)Math.Exp(-t / 0.0025f);
                float feltSnap = Mathf.Sin(2f * Mathf.PI * 2450f * t + Mathf.Sin(2f * Mathf.PI * 680f * t)) * 0.22f * transientEnv;

                float fundamental = Mathf.Sin(2f * Mathf.PI * f0 * t) * 0.68f * bodyDecay;
                float harmonic = Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.24f * overtoneDecay;

                float sample = attack * gain * (fundamental + harmonic + feltSnap);
                pcm[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            return BuildReport("SweetSpotPop", sampleRateHz, f0, pcm);
        }

        /// <summary>
        /// Synthesizes an off-center graphite frame "clank" (inharmonic 940 Hz + 1610 Hz metallic ring).
        /// </summary>
        public static ProceduralImpactAudioReport SynthesizeFrameClank(
            float normalizedVelocity,
            int sampleRateHz = DefaultSampleRateHz)
        {
            float durationSec = 0.110f;
            int sampleCount = Mathf.Max(64, Mathf.RoundToInt(durationSec * sampleRateHz));
            float[] pcm = new float[sampleCount];

            float gain = Mathf.Clamp(normalizedVelocity, 0.2f, 1.0f) * 0.85f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRateHz;
                float attack = Mathf.Clamp01(t / 0.001f);
                float ringDecay = (float)Math.Exp(-t / 0.028f);

                float modeA = Mathf.Sin(2f * Mathf.PI * FrameClankPrimaryHz * t) * 0.58f;
                float modeB = Mathf.Sin(2f * Mathf.PI * FrameClankSecondaryHz * t) * 0.42f;

                float sample = attack * gain * ringDecay * (modeA + modeB);
                pcm[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            return BuildReport("FrameClank", sampleRateHz, FrameClankPrimaryHz, pcm);
        }

        /// <summary>
        /// Synthesizes a court or concrete backboard bounce acoustic thud.
        /// </summary>
        public static ProceduralImpactAudioReport SynthesizeBounceThud(
            CourtSurfaceType surface,
            float impactSpeedMps,
            int sampleRateHz = DefaultSampleRateHz)
        {
            float durationSec = 0.065f;
            int sampleCount = Mathf.Max(64, Mathf.RoundToInt(durationSec * sampleRateHz));
            float[] pcm = new float[sampleCount];

            float baseFreqHz = surface == CourtSurfaceType.ConcreteWall
                ? 195f
                : (surface == CourtSurfaceType.HardCourt ? 150f : 120f);

            float gain = Mathf.Clamp(impactSpeedMps / 35f, 0.15f, 0.95f);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRateHz;
                float env = Mathf.Clamp01(t / 0.0012f) * (float)Math.Exp(-t / 0.013f);
                // Slight downward pitch sweep characteristic of a pressurized hollow rubber core bounce
                float sweptFreq = baseFreqHz * (1f + 0.35f * (float)Math.Exp(-t / 0.005f));
                float sample = gain * env * Mathf.Sin(2f * Mathf.PI * sweptFreq * t);
                pcm[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            return BuildReport($"Bounce_{surface}", sampleRateHz, baseFreqHz, pcm);
        }

        private static ProceduralImpactAudioReport BuildReport(
            string category,
            int sampleRateHz,
            float fundamentalHz,
            float[] pcm)
        {
            float peak = 0f;
            double sumSq = 0.0;
            for (int i = 0; i < pcm.Length; i++)
            {
                float abs = Mathf.Abs(pcm[i]);
                if (abs > peak) peak = abs;
                sumSq += (double)pcm[i] * pcm[i];
            }

            float rms = pcm.Length > 0 ? (float)Math.Sqrt(sumSq / pcm.Length) : 0f;

            return new ProceduralImpactAudioReport
            {
                SoundCategory = category,
                SampleRateHz = sampleRateHz,
                SampleCount = pcm.Length,
                FundamentalFreqHz = fundamentalHz,
                PeakAmplitude = peak,
                RmsEnergy = rms,
                PcmSamples = pcm
            };
        }

        private void PlayReportOnAudioSource(ProceduralImpactAudioReport report)
        {
#if UNITY_5_3_OR_NEWER
            if (racketAudioSource == null || report.PcmSamples == null || report.PcmSamples.Length == 0)
            {
                return;
            }

            AudioClip runtimeClip = AudioClip.Create(
                report.SoundCategory,
                report.SampleCount,
                1,
                report.SampleRateHz,
                false);
            runtimeClip.SetData(report.PcmSamples, 0);
            racketAudioSource.PlayOneShot(runtimeClip, 1.0f);
#endif
        }
    }
}

