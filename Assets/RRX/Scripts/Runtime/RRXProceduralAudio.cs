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

        void Awake()
        {
            ClipOk = CreateTone("RRX_Clip_Ok", 880f, 0.09f, false);
            ClipBad = CreateTone("RRX_Clip_Bad", 160f, 0.30f, true);
            ClipRecovered = CreateArpeggio("RRX_Clip_Recovered", 0.45f);
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
    }
}
