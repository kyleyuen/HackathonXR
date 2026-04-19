using System.Collections.Generic;
using RRX.Core;
using UnityEngine;

namespace RRX.Runtime
{
    /// <summary>
    /// Drives blockout patient visuals from full state snapshots (no animator required).
    /// Adds seizure tremor when unconscious, greenish vomit tint at severe cyanosis,
    /// and a rolling rotation for recovery position.
    /// Works with both the legacy single-mesh body and the detailed multi-primitive anatomy
    /// produced by RRXPatientBuilder; skin tinting reaches all named skin-surface renderers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RRXPatientProceduralVisuals : MonoBehaviour
    {
        [SerializeField] ScenarioRunner _runner;
        [SerializeField] Transform _torso;
        [SerializeField] Transform _head;
        [SerializeField] float _snapshotLerpSeconds = 0.4f;

        readonly List<Renderer> _skinRenderers = new List<Renderer>();
        readonly List<Color> _baseSkinColors = new List<Color>();

        PatientVisualState _current;
        PatientVisualState _target;
        Vector3 _torsoBasePosition;
        Quaternion _headBaseRotation;
        Quaternion _rootBaseRotation;
        float _lerpTimer;

        // Seizure tremor
        float _tremorTimer;
        Vector3 _tremorOffset;
        const float TremorHz = 15f;

        // Recovery roll
        float _rollAngleCurrent;
        float _rollAngleTarget;

        void Awake()
        {
            if (_runner == null)
                _runner = FindObjectOfType<ScenarioRunner>();
            if (_torso == null)
                _torso = transform.Find("Torso");
            if (_head == null)
                _head = transform.Find("Head");

            if (_torso != null)
                _torsoBasePosition = _torso.localPosition;
            if (_head != null)
                _headBaseRotation = _head.localRotation;

            _rootBaseRotation = transform.localRotation;

            CacheSkinRenderers();
        }

        void OnEnable()
        {
            if (_runner == null)
                return;

            _runner.OnPatientSnapshot += OnPatientSnapshot;
            _runner.OnStateChanged.AddListener(OnStateChanged);
            _runner.OnResetRequested += OnResetRequested;
            _target = _runner.CurrentPatientVisual;
            _current = _target;
            ApplyPose(_current, 0f);
        }

        void OnDisable()
        {
            if (_runner == null)
                return;

            _runner.OnPatientSnapshot -= OnPatientSnapshot;
            _runner.OnStateChanged.RemoveListener(OnStateChanged);
            _runner.OnResetRequested -= OnResetRequested;
        }

        void Update()
        {
            if (_snapshotLerpSeconds <= 0f)
            {
                _current = _target;
            }
            else if (_lerpTimer < _snapshotLerpSeconds)
            {
                _lerpTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_lerpTimer / _snapshotLerpSeconds);
                _current = PatientVisualState.Lerp(_current, _target, t);
            }

            ApplyPose(_current, Time.time);

            // Smoothly roll toward the recovery position angle
            _rollAngleCurrent = Mathf.Lerp(_rollAngleCurrent, _rollAngleTarget, Time.deltaTime * 2.5f);
            transform.localRotation = _rootBaseRotation * Quaternion.Euler(0f, 0f, _rollAngleCurrent);
        }

        void OnPatientSnapshot(PatientVisualState snapshot)
        {
            _target = snapshot;
            _lerpTimer = 0f;
        }

        void OnStateChanged(ScenarioState state)
        {
            if (state == ScenarioState.Recovery)
            {
                _rollAngleTarget = 80f;
                // Slower, dramatic wake-up lerp
                _snapshotLerpSeconds = 0.8f;
            }
        }

        void OnResetRequested(int _)
        {
            _rollAngleCurrent = 0f;
            _rollAngleTarget = 0f;
            _snapshotLerpSeconds = 0.4f;
            transform.localRotation = _rootBaseRotation;
        }

        // Substrings that identify skin-coloured renderers in the detailed anatomy hierarchy.
        // Checked against renderer.name (lower-case) via Contains — ordered from most to least common
        // to short-circuit early. Only skin-coloured parts are included; shirt/pants/shoe parts are
        // intentionally omitted so cyanosis tinting stays medically accurate.
        static readonly string[] SkinSubstrings =
        {
            "skull", "jaw", "chin", "nosebridge", "nosetip", "nostril",
            "mouth", "upperlip", "ear", "eyelid", "brow",
            "neck", "shoulder", "elbow", "forearm", "wrist", "hand", "palm", "finger", "ankle"
        };

        void CacheSkinRenderers()
        {
            _skinRenderers.Clear();
            _baseSkinColors.Clear();

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                string lower = r.name.ToLowerInvariant();
                bool isSkin = false;
                foreach (var key in SkinSubstrings)
                {
                    if (lower.Contains(key)) { isSkin = true; break; }
                }
                if (!isSkin) continue;

                _skinRenderers.Add(r);
                _baseSkinColors.Add(r.sharedMaterial != null ? r.sharedMaterial.color : Color.white);
            }
        }

        void ApplyPose(in PatientVisualState state, float now)
        {
            ApplySkin(state);
            ApplyBreathingBob(state, now);
            ApplyHeadSlump(state);
            ApplySeizureTremor(state);
        }

        void ApplyBreathingBob(in PatientVisualState state, float now)
        {
            if (_torso == null) return;
            float amplitude = Mathf.Lerp(0.004f, 0.018f, Mathf.Clamp01(state.BreathRate));
            float speed     = Mathf.Lerp(0.8f, 3.5f, Mathf.Clamp01(state.BreathRate));
            float bob       = Mathf.Sin(now * speed * Mathf.PI * 2f) * amplitude;
            _torso.localPosition = _torsoBasePosition + new Vector3(0f, bob, 0f) + _tremorOffset;
        }

        void ApplyHeadSlump(in PatientVisualState state)
        {
            if (_head == null) return;
            float slumpDegrees = Mathf.Lerp(0f, 28f, Mathf.Clamp01(state.HeadSlump));
            _head.localRotation = _headBaseRotation * Quaternion.Euler(slumpDegrees, 0f, 0f);
        }

        void ApplySeizureTremor(in PatientVisualState state)
        {
            if (state.Consciousness >= 0.1f)
            {
                // Fade out tremor
                _tremorOffset = Vector3.Lerp(_tremorOffset, Vector3.zero, Time.deltaTime * 8f);
                return;
            }

            _tremorTimer += Time.deltaTime;
            if (_tremorTimer >= 1f / TremorHz)
            {
                _tremorTimer = 0f;
                float mag = Mathf.Lerp(0.012f, 0.004f, Mathf.Clamp01(state.Consciousness * 10f));
                _tremorOffset = new Vector3(
                    Random.Range(-mag, mag),
                    Random.Range(-mag * 0.5f, mag * 0.5f),
                    Random.Range(-mag, mag));
            }
        }

        void ApplySkin(in PatientVisualState state)
        {
            Color cyanTint  = new Color(0.45f, 0.6f, 0.95f, 1f);
            Color vomitTint = new Color(0.55f, 0.72f, 0.30f, 1f);
            float consciousness = Mathf.Clamp01(state.Consciousness);
            float cyanosis      = Mathf.Clamp01(state.Cyanosis);
            // Vomit tint kicks in when cyanosis is very high (patient severely compromised)
            float vomitBlend = Mathf.Clamp01((cyanosis - 0.85f) / 0.15f);

            for (int i = 0; i < _skinRenderers.Count; i++)
            {
                var renderer = _skinRenderers[i];
                if (renderer == null || renderer.material == null)
                    continue;

                Color baseColor   = _baseSkinColors[i];
                Color withCyanosis = Color.Lerp(baseColor, cyanTint, cyanosis);
                Color gray         = Color.Lerp(withCyanosis, Color.gray, 0.6f);
                Color withConscious = Color.Lerp(gray, withCyanosis, consciousness);
                Color final        = Color.Lerp(withConscious, vomitTint, vomitBlend);
                renderer.material.color = final;
            }
        }
    }
}
