using RRX.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace RRX.Runtime
{
    /// <summary>Crossfades patient breath loops from live scenario snapshots.</summary>
    [DisallowMultipleComponent]
    public sealed class RRXPatientBreathAudio : MonoBehaviour
    {
        [SerializeField] ScenarioRunner _runner;
        [SerializeField] RRXProceduralAudio _audioBank;
        [SerializeField] AudioSource _normalSource;
        [SerializeField] AudioSource _laboredSource;
        [SerializeField] AudioMixerGroup _patientMixerGroup;
        [SerializeField] float _fadeSpeed = 4f;

        float _targetNormalVol;
        float _targetLaboredVol;

        void Awake()
        {
            if (_runner == null)
                _runner = FindObjectOfType<ScenarioRunner>();
            if (_audioBank == null)
                _audioBank = FindObjectOfType<RRXProceduralAudio>();

            if (_normalSource == null)
                _normalSource = CreateSource("BreathNormal");
            if (_laboredSource == null)
                _laboredSource = CreateSource("BreathLabored");

            ApplyBankClips();
            StartIfClipPresent(_normalSource);
            StartIfClipPresent(_laboredSource);
        }

        void OnEnable()
        {
            if (_runner == null)
                return;
            _runner.OnPatientSnapshot += OnPatientSnapshot;
            OnPatientSnapshot(_runner.CurrentPatientVisual);
        }

        void OnDisable()
        {
            if (_runner != null)
                _runner.OnPatientSnapshot -= OnPatientSnapshot;
        }

        void Update()
        {
            if (_normalSource != null)
                _normalSource.volume = Mathf.MoveTowards(_normalSource.volume, _targetNormalVol, Time.deltaTime * _fadeSpeed);
            if (_laboredSource != null)
                _laboredSource.volume = Mathf.MoveTowards(_laboredSource.volume, _targetLaboredVol, Time.deltaTime * _fadeSpeed);
        }

        void OnPatientSnapshot(PatientVisualState state)
        {
            if (_audioBank == null)
                _audioBank = FindObjectOfType<RRXProceduralAudio>();
            ApplyBankClips();
            StartIfClipPresent(_normalSource);
            StartIfClipPresent(_laboredSource);

            if (state.IsApnea || state.BreathRate <= 0.02f)
            {
                _targetNormalVol = 0f;
                _targetLaboredVol = 0f;
                return;
            }

            var breath01 = Mathf.Clamp01(state.BreathRate);
            _targetNormalVol = Mathf.Clamp01((breath01 - 0.45f) / 0.55f) * 0.18f;
            _targetLaboredVol = (1f - breath01) * 0.26f;
        }

        AudioSource CreateSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 0.5f;
            src.maxDistance = 4f;
            src.dopplerLevel = 0f;
            src.volume = 0f;
            src.priority = 96;
            if (_patientMixerGroup != null)
                src.outputAudioMixerGroup = _patientMixerGroup;
            return src;
        }

        void ApplyBankClips()
        {
            if (_audioBank == null)
                return;
            if (_normalSource != null)
                _normalSource.clip = _audioBank.ClipBreathNormal;
            if (_laboredSource != null)
                _laboredSource.clip = _audioBank.ClipBreathLabored;
        }

        static void StartIfClipPresent(AudioSource src)
        {
            if (src != null && src.clip != null && !src.isPlaying)
                src.Play();
        }
    }
}
