using TMPro;
using RRX.Core;
using RRX.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace RRX.UI
{
    /// <summary>Wrist-anchored objective panel for step/hint/failure guidance.</summary>
    [DisallowMultipleComponent]
    public sealed class RRXWristObjectivePanel : MonoBehaviour
    {
        [SerializeField] ScenarioRunner _runner;
        [SerializeField] ScenarioClock _clock;
        [SerializeField] TMP_Text _titleText;
        [SerializeField] TMP_Text _hintText;
        [SerializeField] TMP_Text _metaText;
        [SerializeField] Button _hintButton;
        [SerializeField] Button _resetButton;
        [SerializeField] Image _panelTint;
        [SerializeField] Vector3 _controllerLocalPosition = new Vector3(0f, 0.055f, 0.015f);
        [SerializeField] Vector3 _controllerLocalEuler = new Vector3(0f, 0f, 0f);
        [SerializeField] Vector3 _controllerLocalScale = new Vector3(0.0007f, 0.0007f, 0.0007f);

        float _failureFlashUntil;

        void Awake()
        {
            if (_runner == null)
                _runner = FindObjectOfType<ScenarioRunner>();
            if (_clock == null)
                _clock = FindObjectOfType<ScenarioClock>();

            BindButtons();
            ApplyControllerLocalAnchor();
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
            // Keep hints fixed above the controller axis with a constant perpendicular orientation.
            ApplyControllerLocalAnchor();
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

        void ApplyControllerLocalAnchor()
        {
            transform.localPosition = _controllerLocalPosition;
            transform.localRotation = Quaternion.Euler(_controllerLocalEuler);
            transform.localScale = _controllerLocalScale;
        }
    }
}
