using System.Collections.Generic;
using RRX.Core;
using UnityEngine;

namespace RRX.Runtime
{
    /// <summary>
    /// Drives blockout patient visuals from full state snapshots (no animator required).
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
        float _lerpTimer;

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

            CacheSkinRenderers();
        }

        void OnEnable()
        {
            if (_runner == null)
                return;

            _runner.OnPatientSnapshot += OnPatientSnapshot;
            _target = _runner.CurrentPatientVisual;
            _current = _target;
            ApplyPose(_current, 0f);
        }

        void OnDisable()
        {
            if (_runner != null)
                _runner.OnPatientSnapshot -= OnPatientSnapshot;
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
        }

        void OnPatientSnapshot(PatientVisualState snapshot)
        {
            _target = snapshot;
            _lerpTimer = 0f;
        }

        void CacheSkinRenderers()
        {
            _skinRenderers.Clear();
            _baseSkinColors.Clear();

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;
                string lower = renderer.name.ToLowerInvariant();
                if (!lower.Contains("head") && !lower.Contains("neck") && !lower.Contains("forearm") &&
                    !lower.Contains("hand"))
                    continue;

                _skinRenderers.Add(renderer);
                _baseSkinColors.Add(renderer.sharedMaterial != null ? renderer.sharedMaterial.color : Color.white);
            }
        }

        void ApplyPose(in PatientVisualState state, float now)
        {
            ApplySkin(state);

            if (_torso != null)
            {
                float amplitude = Mathf.Lerp(0.004f, 0.018f, Mathf.Clamp01(state.BreathRate));
                float speed = Mathf.Lerp(0.8f, 3.5f, Mathf.Clamp01(state.BreathRate));
                float bob = Mathf.Sin(now * speed * Mathf.PI * 2f) * amplitude;
                _torso.localPosition = _torsoBasePosition + new Vector3(0f, bob, 0f);
            }

            if (_head != null)
            {
                float slumpDegrees = Mathf.Lerp(0f, 28f, Mathf.Clamp01(state.HeadSlump));
                _head.localRotation = _headBaseRotation * Quaternion.Euler(slumpDegrees, 0f, 0f);
            }
        }

        void ApplySkin(in PatientVisualState state)
        {
            Color cyanTint = new Color(0.45f, 0.6f, 0.95f, 1f);
            float consciousness = Mathf.Clamp01(state.Consciousness);
            for (int i = 0; i < _skinRenderers.Count; i++)
            {
                var renderer = _skinRenderers[i];
                if (renderer == null || renderer.material == null)
                    continue;

                Color baseColor = _baseSkinColors[i];
                Color withCyanosis = Color.Lerp(baseColor, cyanTint, Mathf.Clamp01(state.Cyanosis));
                Color gray = Color.Lerp(withCyanosis, Color.gray, 0.6f);
                Color final = Color.Lerp(gray, withCyanosis, consciousness);
                renderer.material.color = final;
            }
        }
    }
}
