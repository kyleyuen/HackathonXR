using TMPro;
using RRX.Core;
using RRX.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace RRX.UI
{
    /// <summary>
    /// Wrist-anchored objective panel with two-tier hints: starts vague, upgrades to specific once
    /// the current hotspot's reveal delay fires or the player makes 2+ mistakes.
    /// </summary>
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
        RRXHotspotHighlight _currentHighlight;

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

            SubscribeToCurrentHighlight();
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

            UnsubscribeHighlight();
        }

        void Update()
        {
            RefreshMeta();
            if (_panelTint != null)
            {
                bool inWarnZone = _clock != null && _clock.ElapsedSeconds >= _clock.WarnSeconds;
                _panelTint.color = Time.realtimeSinceStartup < _failureFlashUntil || inWarnZone
                    ? new Color(0.8f, 0.2f, 0.2f, 0.35f)
                    : new Color(0.1f, 0.12f, 0.15f, 0.22f);
            }
        }

        void LateUpdate()
        {
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

            // Clicking hint upgrades to specific text immediately
            Refresh();
        }

        void OnResetClicked()
        {
            _runner?.ResetScenario();
        }

        void OnStateChanged(ScenarioState _)
        {
            SubscribeToCurrentHighlight();
            Refresh();
        }

        void OnResetRequested(int _)
        {
            _failureFlashUntil = 0f;
            SubscribeToCurrentHighlight();
            Refresh();
        }

        void OnTimePressureWarning()
        {
            _failureFlashUntil = Time.realtimeSinceStartup + 1f;
        }

        void OnHighlightRevealed()
        {
            Refresh();
        }

        void SubscribeToCurrentHighlight()
        {
            UnsubscribeHighlight();

            if (_runner == null)
                return;

            var tag = RRXScenarioHotspotTag.Find(_runner.NextRequiredHotspot);
            if (tag == null) return;

            var highlight = tag.GetComponent<RRXHotspotHighlight>();
            if (highlight == null) return;

            _currentHighlight = highlight;
            _currentHighlight.OnReveal += OnHighlightRevealed;
        }

        void UnsubscribeHighlight()
        {
            if (_currentHighlight != null)
            {
                _currentHighlight.OnReveal -= OnHighlightRevealed;
                _currentHighlight = null;
            }
        }

        void Refresh()
        {
            if (_runner == null)
                return;

            if (_titleText != null)
                _titleText.text = ScenarioCopy.TitleFor(_runner.CurrentState);

            if (_hintText != null)
                _hintText.text = BuildHintText();

            if (_resetButton != null)
            {
                bool terminal = _runner.CurrentState == ScenarioState.Recovery ||
                                _runner.CurrentState == ScenarioState.CriticalFailure;
                _resetButton.gameObject.SetActive(terminal);
            }

            RefreshMeta();
        }

        string BuildHintText()
        {
            var state = _runner.CurrentState;
            var failures = _runner.FailureCount;

            bool showSpecific = failures >= 2
                || (_currentHighlight != null && _currentHighlight.IsRevealed)
                || state == ScenarioState.Recovery
                || state == ScenarioState.CriticalFailure;

            return showSpecific
                ? ScenarioCopy.SpecificHintFor(state, failures)
                : ScenarioCopy.VagueHintFor(state);
        }

        void RefreshMeta()
        {
            if (_metaText == null || _runner == null)
                return;

            string meta = $"Mistakes: {_runner.FailureCount}/3";
            if (_clock != null && _clock.ElapsedSeconds > 0f)
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
