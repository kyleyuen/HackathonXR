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

        public AudioClip ClipDrone { get; private set; }
        public AudioClip ClipCrowdMurmur { get; private set; }
        public AudioClip ClipSiren { get; private set; }
        public AudioClip ClipGasp { get; private set; }
        public AudioClip ClipHeartbeat { get; private set; }

        void Awake()
        {
            ClipOk = CreateTone("RRX_Clip_Ok", 880f, 0.09f, false);
            ClipBad = CreateTone("RRX_Clip_Bad", 160f, 0.30f, true);
            ClipRecovered = CreateArpeggio("RRX_Clip_Recovered", 0.45f);

            ClipDrone = CreateDroneLoop("RRX_Clip_Drone", 2.5f);
            ClipCrowdMurmur = CreateCrowdMurmurLoop("RRX_Clip_Crowd", 2f);
            ClipSiren = CreateSirenLoop("RRX_Clip_Siren", 3f);
            ClipGasp = CreateGaspLoop("RRX_Clip_Gasp", 2f);
            ClipHeartbeat = CreateHeartbeatLoop("RRX_Clip_Heartbeat", 2f);
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

        AudioClip CreateDroneLoop(string name, float duration)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(duration * _sampleRate));
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)_sampleRate;
                float w =
                    Mathf.Sin(2f * Mathf.PI * 52f * t) * 0.07f +
                    Mathf.Sin(2f * Mathf.PI * 73f * t) * 0.05f +
                    Mathf.Sin(2f * Mathf.PI * 104f * t) * 0.03f;
                float env = 0.95f + 0.05f * Mathf.Sin(t * 0.7f);
                data[i] = w * env;
            }

            var clip = AudioClip.Create(name, samples, 1, _sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        AudioClip CreateCrowdMurmurLoop(string name, float duration)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(duration * _sampleRate));
            var data = new float[samples];
            float leak = 0f;
            for (int i = 0; i < samples; i++)
            {
                leak = leak * 0.92f + Random.Range(-1f, 1f) * 0.35f;
                float t = i / (float)_sampleRate;
                float chatter =
                    Mathf.Sin(2f * Mathf.PI * (180f + 40f * Mathf.Sin(t * 1.7f)) * t) * 0.08f +
                    Mathf.Sin(2f * Mathf.PI * (240f + 30f * Mathf.Sin(t * 2.3f)) * t) * 0.06f;
                data[i] = Mathf.Clamp(leak * 0.12f + chatter, -1f, 1f);
            }

            var clip = AudioClip.Create(name, samples, 1, _sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        AudioClip CreateSirenLoop(string name, float duration)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(duration * _sampleRate));
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)_sampleRate;
                float sweep = Mathf.Lerp(520f, 980f, (Mathf.Sin(t * 1.8f * Mathf.PI * 2f / duration) + 1f) * 0.5f);
                float wave = Mathf.Sin(2f * Mathf.PI * sweep * t);
                data[i] = wave * 0.14f;
            }

            var clip = AudioClip.Create(name, samples, 1, _sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        AudioClip CreateGaspLoop(string name, float duration)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(duration * _sampleRate));
            var data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float u = i / (float)samples;
                float cycle = Mathf.Repeat(u * 3f, 1f);
                float breath = Mathf.Exp(-cycle * 6f);
                float noise = Random.Range(-1f, 1f);
                phase = phase * 0.85f + noise * 0.25f;
                float hiss = phase * breath * 0.35f;
                data[i] = Mathf.Clamp(hiss, -1f, 1f);
            }

            var clip = AudioClip.Create(name, samples, 1, _sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        AudioClip CreateHeartbeatLoop(string name, float duration)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(duration * _sampleRate));
            var data = new float[samples];
            float bpm = 72f;
            float beatPeriod = 60f / bpm;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)_sampleRate;
                float bt = Mathf.Repeat(t, beatPeriod);
                float lub = Mathf.Exp(-Mathf.Pow(bt - 0.06f, 2f) / 0.00012f);
                float dub = Mathf.Exp(-Mathf.Pow(bt - 0.14f, 2f) / 0.00012f);
                float thump = (lub + dub * 0.65f) * 0.45f;
                float body = Mathf.Sin(2f * Mathf.PI * 42f * t) * thump * 0.25f;
                data[i] = Mathf.Clamp(thump + body, -1f, 1f);
            }

            var clip = AudioClip.Create(name, samples, 1, _sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
