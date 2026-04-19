using RRX.Core;
using UnityEngine;

namespace RRX.Runtime
{
    /// <summary>
    /// Escalating environmental stress that rises through 4 phases as the scenario progresses.
    /// Phase 1: faint drone + distant siren (start of scene).
    /// Phase 2: louder siren + crowd murmur (after first action OR 10 s idle).
    /// Phase 3: patient gasping + red pulsing light (after step 3 OR 30 s elapsed).
    /// Phase 4: maximum urgency — peak siren + crowd + gasping + fast light pulse (after step 5 OR 60 s).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RRXEmergencyAmbience : MonoBehaviour
    {
        [SerializeField] ScenarioRunner _runner;
        [SerializeField] RRXProceduralAudio _audio;

        AudioSource _droneSrc;
        AudioSource _sirenSrc;
        AudioSource _gaspSrc;
        AudioSource _heartbeatSrc;
        AudioSource _crowdSrc;
        Light _emergencyLight;

        int _currentPhase;
        int _stepsCompleted;
        float _sceneStartTime;

        static readonly float[] SirenVols  = { 0.04f, 0.18f, 0.30f, 0.45f };
        static readonly float[] CrowdVols  = { 0.00f, 0.12f, 0.20f, 0.32f };
        static readonly float[] GaspVols   = { 0.00f, 0.00f, 0.22f, 0.38f };
        static readonly float[] DroneVols  = { 0.08f, 0.10f, 0.12f, 0.14f };
        static readonly float[] HbVols     = { 0.00f, 0.00f, 0.10f, 0.22f };

        void Awake()
        {
            if (_runner == null)
                _runner = FindObjectOfType<ScenarioRunner>();
            if (_audio == null)
                _audio = FindObjectOfType<RRXProceduralAudio>();

            CreateAudioSources();
            CreateEmergencyLight();
        }

        void OnEnable()
        {
            if (_runner != null)
            {
                _runner.OnStateChanged.AddListener(OnStateChanged);
                _runner.OnResetRequested += OnReset;
            }
        }

        void OnDisable()
        {
            if (_runner != null)
            {
                _runner.OnStateChanged.RemoveListener(OnStateChanged);
                _runner.OnResetRequested -= OnReset;
            }
        }

        void Start()
        {
            _sceneStartTime = Time.time;
            _currentPhase = -1;
            SetPhase(0);

            // Start looping audio immediately at Phase 0 volumes
            StartLooping(_droneSrc);
            StartLooping(_sirenSrc);
            StartLooping(_crowdSrc);
            StartLooping(_gaspSrc);
            StartLooping(_heartbeatSrc);
        }

        void Update()
        {
            float elapsed = Time.time - _sceneStartTime;

            // Time-driven phase escalation (backup if player is idle)
            int targetPhase = _currentPhase;
            if (elapsed >= 60f)      targetPhase = Mathf.Max(targetPhase, 3);
            else if (elapsed >= 30f) targetPhase = Mathf.Max(targetPhase, 2);
            else if (elapsed >= 10f) targetPhase = Mathf.Max(targetPhase, 1);

            if (targetPhase > _currentPhase)
                SetPhase(targetPhase);

            // Animate emergency light
            AnimateLight();

            // Smoothly interpolate audio volumes
            LerpAudioVolumes();
        }

        public void SetRunner(ScenarioRunner runner)
        {
            _runner = runner;
        }

        public void SetAudio(RRXProceduralAudio audio)
        {
            _audio = audio;

            // Re-assign clips now that the audio component is available
            if (_droneSrc != null)      _droneSrc.clip      = audio.ClipCrowdMurmur;
            if (_sirenSrc != null)      _sirenSrc.clip      = audio.ClipSiren;
            if (_gaspSrc != null)       _gaspSrc.clip       = audio.ClipGasp;
            if (_heartbeatSrc != null)  _heartbeatSrc.clip  = audio.ClipHeartbeat;
            if (_crowdSrc != null)      _crowdSrc.clip      = audio.ClipCrowdMurmur;
        }

        void OnStateChanged(ScenarioState state)
        {
            // Count completed scenario steps to drive phase escalation
            switch (state)
            {
                case ScenarioState.Arrival:
                    _stepsCompleted = 1;
                    break;
                case ScenarioState.OpenAirway:
                    _stepsCompleted = 2;
                    break;
                case ScenarioState.CheckBreathing:
                    _stepsCompleted = 3;
                    break;
                case ScenarioState.CallForHelp:
                    _stepsCompleted = 4;
                    break;
                case ScenarioState.AdministerNarcan:
                    _stepsCompleted = 5;
                    break;
                case ScenarioState.RecoveryPosition:
                    _stepsCompleted = 6;
                    break;
                case ScenarioState.Recovery:
                    SetPhase(0); // Calm down after successful recovery
                    return;
            }

            int targetPhase = _currentPhase;
            if (_stepsCompleted >= 5)      targetPhase = 3;
            else if (_stepsCompleted >= 3) targetPhase = 2;
            else if (_stepsCompleted >= 1) targetPhase = Mathf.Max(1, _currentPhase);

            if (targetPhase > _currentPhase)
                SetPhase(targetPhase);
        }

        void OnReset(int _)
        {
            _stepsCompleted = 0;
            _sceneStartTime = Time.time;
            SetPhase(0);
        }

        void SetPhase(int phase)
        {
            _currentPhase = Mathf.Clamp(phase, 0, 3);
        }

        void LerpAudioVolumes()
        {
            int p = _currentPhase;
            float dt = Time.deltaTime * 1.5f; // blending speed

            if (_droneSrc != null)      _droneSrc.volume      = Mathf.Lerp(_droneSrc.volume,      DroneVols[p], dt);
            if (_sirenSrc != null)      _sirenSrc.volume      = Mathf.Lerp(_sirenSrc.volume,      SirenVols[p], dt);
            if (_crowdSrc != null)      _crowdSrc.volume      = Mathf.Lerp(_crowdSrc.volume,      CrowdVols[p], dt);
            if (_gaspSrc != null)       _gaspSrc.volume       = Mathf.Lerp(_gaspSrc.volume,       GaspVols[p],  dt);
            if (_heartbeatSrc != null)  _heartbeatSrc.volume  = Mathf.Lerp(_heartbeatSrc.volume,  HbVols[p],    dt);
        }

        void AnimateLight()
        {
            if (_emergencyLight == null) return;

            float maxIntensity = _currentPhase switch
            {
                0 => 0f,
                1 => 0.4f,
                2 => 1.2f,
                3 => 2.5f,
                _ => 0f
            };

            float pulseSpeed = _currentPhase >= 3 ? 8f : 4f;
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            _emergencyLight.intensity = Mathf.Lerp(0f, maxIntensity, t);
            _emergencyLight.enabled = maxIntensity > 0f;
        }

        static void StartLooping(AudioSource src)
        {
            if (src == null) return;
            src.loop = true;
            src.volume = 0f;
            if (src.clip != null)
                src.Play();
        }

        void CreateAudioSources()
        {
            _droneSrc      = AddSource("Drone",     spatialBlend: 0f);
            _sirenSrc      = AddSource("Siren",     spatialBlend: 0.4f, pitch: 1f);
            _crowdSrc      = AddSource("Crowd",     spatialBlend: 0.3f);
            _gaspSrc       = AddSource("Gasp",      spatialBlend: 0.8f);
            _heartbeatSrc  = AddSource("Heartbeat", spatialBlend: 0.5f);
        }

        AudioSource AddSource(string label, float spatialBlend = 0f, float pitch = 1f)
        {
            var child = new GameObject($"RRX_Ambience_{label}");
            child.transform.SetParent(transform, false);
            var src = child.AddComponent<AudioSource>();
            src.spatialBlend = spatialBlend;
            src.pitch = pitch;
            src.playOnAwake = false;
            src.loop = true;
            src.volume = 0f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 1f;
            src.maxDistance = 20f;
            return src;
        }

        void CreateEmergencyLight()
        {
            var lightGO = new GameObject("RRX_EmergencyLight");
            lightGO.transform.SetParent(transform, false);
            lightGO.transform.localPosition = new Vector3(0f, 2f, 1.5f);
            _emergencyLight = lightGO.AddComponent<Light>();
            _emergencyLight.type = LightType.Point;
            _emergencyLight.color = new Color(1f, 0.15f, 0.1f);
            _emergencyLight.intensity = 0f;
            _emergencyLight.range = 8f;
            _emergencyLight.enabled = false;
        }
    }
}
