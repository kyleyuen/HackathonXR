using UnityEngine;

namespace RRX.Runtime
{
    /// <summary>Generates tiny runtime clips so the scenario has SFX without imported assets.</summary>
    [DisallowMultipleComponent]
    public sealed class RRXProceduralAudio : MonoBehaviour
    {
        [SerializeField] int _sampleRate = 44100;

        public AudioClip ClipOk { get; private set; }
        public AudioClip ClipBad { get; private set; }
        public AudioClip ClipRecovered { get; private set; }
        /// <summary>Seamless looping mall murmur (deterministic harmonics, no imported assets).</summary>
        public AudioClip ClipCrowdAmbience { get; private set; }

        void Awake()
        {
            ClipOk = CreateTone("RRX_Clip_Ok", 880f, 0.09f, false);
            ClipBad = CreateTone("RRX_Clip_Bad", 160f, 0.30f, true);
            ClipRecovered = CreateArpeggio("RRX_Clip_Recovered", 0.45f);
            ClipCrowdAmbience = CreateCrowdMurmurLoop("RRX_Clip_CrowdAmbience", 5.5f);
        }

        AudioClip CreateTone(string name, float frequency, float duration, bool square)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(duration * _sampleRate));
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)_sampleRate;
                float wave = Mathf.Sin(2f * Mathf.PI * frequency * t);
                if (square)
                    wave = wave >= 0f ? 1f : -1f;
                float envelope = 1f - Mathf.Clamp01(i / (float)samples);
                data[i] = wave * envelope * 0.2f;
            }

            var clip = AudioClip.Create(name, samples, 1, _sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        AudioClip CreateArpeggio(string name, float duration)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(duration * _sampleRate));
            var data = new float[samples];
            float[] notes = { 261.63f, 329.63f, 392f };
            int segment = Mathf.Max(1, samples / notes.Length);

            for (int i = 0; i < samples; i++)
            {
                int noteIndex = Mathf.Min(notes.Length - 1, i / segment);
                float t = i / (float)_sampleRate;
                float wave = Mathf.Sin(2f * Mathf.PI * notes[noteIndex] * t);
                float envelope = 1f - Mathf.Clamp01(i / (float)samples);
                data[i] = wave * envelope * 0.2f;
            }

            var clip = AudioClip.Create(name, samples, 1, _sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Periodic “shoppers” bed: only frequencies that are integer multiples of the loop fundamental,
        /// so the clip loops without a click.
        /// </summary>
        AudioClip CreateCrowdMurmurLoop(string name, float durationSeconds)
        {
            int samples = Mathf.Max(_sampleRate / 2, Mathf.RoundToInt(durationSeconds * _sampleRate));
            var data = new float[samples];
            float fundamental = 2f * Mathf.PI / samples;
            // Few dozen inharmonic partials in a speech-ish band.
            int[] partials =
            {
                3, 5, 7, 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29, 31, 33, 35, 37, 39, 41, 43, 45, 47, 49,
                51, 53, 55, 57, 59, 61, 63, 65, 67, 69, 71, 73, 75, 77, 79, 81, 83, 85, 87, 89, 91, 93, 95, 97
            };
            float[] weights = new float[partials.Length];
            for (var k = 0; k < weights.Length; k++)
            {
                float n = partials[k];
                weights[k] = (1f / Mathf.Sqrt(n + 2f)) * (0.55f + 0.45f * Mathf.Sin(n * 0.37f + 1.1f));
            }

            for (var i = 0; i < samples; i++)
            {
                float ph = i * fundamental;
                float murmur = 0f;
                for (var k = 0; k < partials.Length; k++)
                    murmur += Mathf.Sin(ph * partials[k]) * weights[k];
                float slow = 0.72f + 0.28f * Mathf.Sin(ph * 2f);
                float mid = 0.88f + 0.12f * Mathf.Sin(ph * 7.3f);
                float v = murmur * slow * mid * 0.018f;
                data[i] = Mathf.Clamp(v, -0.95f, 0.95f);
            }

            // Gentle RMS normalize so mall bed sits under one-shots.
            double sumSq = 0d;
            for (var i = 0; i < samples; i++)
                sumSq += data[i] * data[i];
            float rms = Mathf.Sqrt((float)(sumSq / Mathf.Max(1, samples)));
            float gain = rms > 1e-6f ? 0.085f / rms : 1f;
            for (var i = 0; i < samples; i++)
                data[i] = Mathf.Clamp(data[i] * gain, -0.35f, 0.35f);

            var clip = AudioClip.Create(name, samples, 1, _sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
