using UnityEngine;

namespace RRX.Runtime
{
    /// <summary>Generates tiny runtime clips so the scenario has SFX without imported assets.</summary>
    [DisallowMultipleComponent]
    public sealed class RRXProceduralAudio : MonoBehaviour
    {
        [SerializeField] int _sampleRate = 44100;

        public AudioClip ClipOk          { get; private set; }
        public AudioClip ClipBad         { get; private set; }
        public AudioClip ClipRecovered   { get; private set; }
        public AudioClip ClipSiren       { get; private set; }
        public AudioClip ClipGasp        { get; private set; }
        public AudioClip ClipCrowdMurmur { get; private set; }
        public AudioClip ClipHeartbeat   { get; private set; }

        void Awake()
        {
            ClipOk          = CreateTone("RRX_Clip_Ok",  880f,  0.09f, false);
            ClipBad         = CreateTone("RRX_Clip_Bad", 160f,  0.30f, true);
            ClipRecovered   = CreateArpeggio("RRX_Clip_Recovered", 0.45f);
            ClipSiren       = CreateSiren("RRX_Clip_Siren", 0.8f);
            ClipGasp        = CreateGasp("RRX_Clip_Gasp", 0.35f);
            ClipCrowdMurmur = CreateBrownianNoise("RRX_Clip_CrowdMurmur", 1.2f);
            ClipHeartbeat   = CreateHeartbeat("RRX_Clip_Heartbeat", 0.6f);
        }

        // ── existing helpers ────────────────────────────────────────────────

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

        // ── new emergency clips ──────────────────────────────────────────────

        /// <summary>Classic two-tone siren: alternates 660 Hz / 880 Hz square wave.</summary>
        AudioClip CreateSiren(string name, float duration)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(duration * _sampleRate));
            var data = new float[samples];
            int halfSamples = samples / 2;
            for (int i = 0; i < samples; i++)
            {
                float freq = i < halfSamples ? 660f : 880f;
                float t = i / (float)_sampleRate;
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                wave = wave >= 0f ? 1f : -1f;             // square wave
                float fade = Mathf.Clamp01(i / (float)(samples * 0.05f)) *    // attack
                             Mathf.Clamp01(1f - (i - samples * 0.9f) / (float)(samples * 0.1f)); // release
                data[i] = wave * fade * 0.25f;
            }

            var clip = AudioClip.Create(name, samples, 1, _sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Breathy gasp: short filtered white noise burst with sharp attack.</summary>
        AudioClip CreateGasp(string name, float duration)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(duration * _sampleRate));
            var data = new float[samples];
            var rng = new System.Random(42);
            float prev = 0f;
            for (int i = 0; i < samples; i++)
            {
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                // Simple one-pole low-pass at ~2 kHz to give "breathiness"
                prev = prev * 0.7f + noise * 0.3f;
                float norm = i / (float)samples;
                float envelope = Mathf.Clamp01(norm / 0.08f) * Mathf.Pow(1f - norm, 1.5f);
                data[i] = prev * envelope * 0.35f;
            }

            var clip = AudioClip.Create(name, samples, 1, _sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Loopable crowd murmur: brownian (integrated white) noise, low-frequency.</summary>
        AudioClip CreateBrownianNoise(string name, float duration)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(duration * _sampleRate));
            var data = new float[samples];
            var rng = new System.Random(7);
            float acc = 0f;
            for (int i = 0; i < samples; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.02f;
                acc = Mathf.Clamp(acc + white, -1f, 1f);
                // Crossfade first / last 5 % so the clip loops smoothly
                float norm = i / (float)samples;
                float fade = Mathf.Clamp01(norm / 0.05f) * Mathf.Clamp01((1f - norm) / 0.05f);
                data[i] = acc * fade * 0.18f;
            }

            var clip = AudioClip.Create(name, samples, 1, _sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Heartbeat double-thump: lub (low) + dub (slightly higher) sine pair.</summary>
        AudioClip CreateHeartbeat(string name, float duration)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(duration * _sampleRate));
            var data = new float[samples];

            // Lub at 0.05 s, Dub at 0.22 s (approximate resting bpm timing)
            float[] offsets   = { 0.05f, 0.22f };
            float[] freqs     = { 70f,   100f  };
            float[] lengths   = { 0.09f, 0.07f };
            float[] amplitudes = { 0.28f, 0.18f };

            for (int b = 0; b < offsets.Length; b++)
            {
                int start = Mathf.RoundToInt(offsets[b] * _sampleRate);
                int len   = Mathf.RoundToInt(lengths[b] * _sampleRate);
                for (int i = 0; i < len && (start + i) < samples; i++)
                {
                    float t   = i / (float)_sampleRate;
                    float env = Mathf.Sin(Mathf.PI * i / (float)len);
                    data[start + i] += Mathf.Sin(2f * Mathf.PI * freqs[b] * t) * env * amplitudes[b];
                }
            }

            var clip = AudioClip.Create(name, samples, 1, _sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
