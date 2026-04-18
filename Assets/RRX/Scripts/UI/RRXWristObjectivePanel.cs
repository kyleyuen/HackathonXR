using TMPro;
using RRX.Core;
using RRX.Runtime;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;

namespace RRX.UI
{
    /// <summary>Wrist-anchored objective panel for step/hint/failure guidance.</summary>
    [DisallowMultipleComponent]
    public sealed class RRXWristObjectivePanel : MonoBehaviour
    {
        [SerializeField] ScenarioRunner _runner;
        [SerializeField] XROrigin _origin;
        [SerializeField] ScenarioClock _clock;
        [SerializeField] TMP_Text _titleText;
        [SerializeField] TMP_Text _hintText;
        [SerializeField] TMP_Text _metaText;
        [SerializeField] Button _hintButton;
        [SerializeField] Button _resetButton;
        [SerializeField] Image _panelTint;
        [SerializeField] float _faceLerp = 8f;

        Quaternion _restLocalRotation;
        float _failureFlashUntil;

        void Awake()
        {
            if (_runner == null)
                _runner = FindObjectOfType<ScenarioRunner>();
            if (_origin == null)
                _origin = FindObjectOfType<XROrigin>();
            if (_clock == null)
                _clock = FindObjectOfType<ScenarioClock>();

            _restLocalRotation = transform.localRotation;
            BindButtons();
        }

        void OnEnable()
        {
            if (_runner != null)
            {
                _runner.OnStateChanged.AddListener(OnStateChanged);
                _runner.OnResetRequested += OnResetRequested;
                _runner.OnTimePressureWarning += OnTimePressureWarning;
            }
            Refresh();
        }

        void OnDisable()
        {
            if (_runner != null)
            {
                _runner.OnStateChanged.RemoveListener(OnStateChanged);
                _runner.OnResetRequested -= OnResetRequested;
                _runner.OnTimePressureWarning -= OnTimePressureWarning;
            }
        }

        void Update()
        {
            RefreshMeta();
            if (_panelTint != null)
            {
                _panelTint.color = Time.realtimeSinceStartup < _failureFlashUntil
                    ? new Color(0.8f, 0.2f, 0.2f, 0.35f)
                    : new Color(0.1f, 0.12f, 0.15f, 0.22f);
            }
        }

        void LateUpdate()
        {
            if (_origin == null || _origin.Camera == null)
                return;

            var cam = _origin.Camera.transform;
            var toCam = (cam.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, toCam);
            if (angle > 70f)
            {
                transform.localRotation = Quaternion.Slerp(
                    transform.localRotation,
                    _restLocalRotation,
                    1f - Mathf.Exp(-_faceLerp * Time.deltaTime));
                return;
            }

            var targetWorld = Quaternion.LookRotation(toCam, Vector3.up);
            var local = Quaternion.Inverse(transform.parent.rotation) * targetWorld;
            var e = local.eulerAngles;
            float yaw = Mathf.DeltaAngle(0f, e.y);
            float pitch = Mathf.DeltaAngle(0f, e.x);
            yaw = Mathf.Clamp(yaw, -45f, 45f);
            pitch = Mathf.Clamp(pitch, -35f, 35f);
            var clamped = Quaternion.Euler(pitch, yaw, 0f);
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                clamped,
                1f - Mathf.Exp(-_faceLerp * Time.deltaTime));
        }

        public void FlashFailure()
        {
            _failureFlashUntil = Time.realtimeSinceStartup + 0.6f;
            Refresh();
        }

        void OnHintClicked()
        {
            if (_runner == null)
                return;
            var tag = RRXScenarioHotspotTag.Find(_runner.NextRequiredHotspot);
            if (tag != null)
            {
                var highlight = tag.GetComponent<RRXHotspotHighlight>();
                highlight?.PulseNow();
            }
        }

        void OnResetClicked()
        {
            _runner?.ResetScenario();
        }

        void OnStateChanged(ScenarioState _)
        {
            Refresh();
        }

        void OnResetRequested(int _)
        {
            _failureFlashUntil = 0f;
            Refresh();
        }

        void OnTimePressureWarning()
        {
            _failureFlashUntil = Time.realtimeSinceStartup + 1f;
        }

        void Refresh()
        {
            if (_runner == null)
                return;

            if (_titleText != null)
                _titleText.text = ScenarioCopy.TitleFor(_runner.CurrentState);
            if (_hintText != null)
                _hintText.text = ScenarioCopy.HintFor(_runner.CurrentState, _runner.FailureCount);

            if (_resetButton != null)
            {
                bool terminal = _runner.CurrentState == ScenarioState.Recovery ||
                                _runner.CurrentState == ScenarioState.CriticalFailure;
                _resetButton.gameObject.SetActive(terminal);
            }

            RefreshMeta();
        }

        void RefreshMeta()
        {
            if (_metaText == null || _runner == null)
                return;

            string meta = $"Mistakes: {_runner.FailureCount}/3";
            if (_clock != null)
                meta = $"{meta}  Time: {Mathf.RoundToInt(_clock.ElapsedSeconds)}s";
            _metaText.text = meta;
        }

        public void Configure(
            TMP_Text titleText,
            TMP_Text hintText,
            TMP_Text metaText,
            Button hintButton,
            Button resetButton,
            Image panelTint)
        {
            _titleText = titleText;
            _hintText = hintText;
            _metaText = metaText;
            _hintButton = hintButton;
            _resetButton = resetButton;
            _panelTint = panelTint;
            BindButtons();
        }

        void BindButtons()
        {
            if (_hintButton != null)
            {
                _hintButton.onClick.RemoveListener(OnHintClicked);
                _hintButton.onClick.AddListener(OnHintClicked);
            }

            if (_resetButton != null)
            {
                _resetButton.onClick.RemoveListener(OnResetClicked);
                _resetButton.onClick.AddListener(OnResetClicked);
            }
        }
    }
}
