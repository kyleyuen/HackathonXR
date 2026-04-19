using System;
using System.Collections.Generic;
using RRX.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

namespace RRX.Interactions
{
    /// <summary>
    /// Submits scenario actions from ray-hover + trigger edge presses.
    /// Uses WasPressedThisFrame and per-hotspot cooldown to suppress jitter repeats.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RRXTriggerActivatedHotspot : MonoBehaviour
    {
        [SerializeField] ScenarioRunner _runner;
        [SerializeField] XRBaseInteractable _interactable;
        [SerializeField] RRXScenarioHotspotTag _hotspotTag;
        [SerializeField] InputActionReference _leftUiPress;
        [SerializeField] InputActionReference _rightUiPress;
        [SerializeField] float _cooldownSeconds = 0.25f;
        [SerializeField] bool _rightTriggerOnly = true;

        readonly HashSet<XRBaseInteractor> _hovering = new HashSet<XRBaseInteractor>();
        int _hoverCount;
        float _nextAllowedRealtime;

        void Awake()
        {
            if (_runner == null)
                _runner = FindObjectOfType<ScenarioRunner>();
            if (_interactable == null)
                _interactable = GetComponent<XRBaseInteractable>();
            if (_hotspotTag == null)
                _hotspotTag = GetComponent<RRXScenarioHotspotTag>();
        }

        void OnEnable()
        {
            if (_interactable != null)
            {
                _interactable.hoverEntered.AddListener(OnHoverEntered);
                _interactable.hoverExited.AddListener(OnHoverExited);
            }

            if (_runner != null)
                _runner.OnResetRequested += OnResetRequested;
        }

        void OnDisable()
        {
            if (_interactable != null)
            {
                _interactable.hoverEntered.RemoveListener(OnHoverEntered);
                _interactable.hoverExited.RemoveListener(OnHoverExited);
            }

            if (_runner != null)
                _runner.OnResetRequested -= OnResetRequested;
            _hovering.Clear();
            _hoverCount = 0;
        }

        public void SetRunner(ScenarioRunner runner) => _runner = runner;
        public void SetHotspotTag(RRXScenarioHotspotTag hotspotTag) => _hotspotTag = hotspotTag;
        public void SetUiPressActions(InputActionReference leftUiPress, InputActionReference rightUiPress)
        {
            _leftUiPress = leftUiPress;
            _rightUiPress = rightUiPress;
        }

        void Update()
        {
            if (_runner == null || _hotspotTag == null || _hoverCount <= 0)
                return;
            if (Time.realtimeSinceStartup < _nextAllowedRealtime)
                return;

            bool rightPressed = _rightUiPress != null && _rightUiPress.action != null && _rightUiPress.action.WasPressedThisFrame();
            bool leftPressed = !_rightTriggerOnly &&
                               _leftUiPress != null &&
                               _leftUiPress.action != null &&
                               _leftUiPress.action.WasPressedThisFrame();
            if (!leftPressed && !rightPressed)
                return;

            var interactor = PickInteractor(leftPressed, rightPressed);
            if (interactor == null)
                return;

            var action = ScenarioHotspotRegistry.ActionFor(_hotspotTag.HotspotId);
            var submission = new ScenarioActionSubmission(
                action,
                _hotspotTag.HotspotId,
                interactor,
                Time.realtimeSinceStartup);

            var result = _runner.TrySubmit(submission, out _);
            if (result != ScenarioSubmissionResult.RejectedThrottled)
                _nextAllowedRealtime = Time.realtimeSinceStartup + _cooldownSeconds;
        }

        XRBaseInteractor PickInteractor(bool leftPressed, bool rightPressed)
        {
            XRBaseInteractor fallback = null;
            foreach (var interactor in _hovering)
            {
                if (interactor == null) continue;
                fallback ??= interactor;

                var lower = interactor.name.ToLowerInvariant();
                if (leftPressed && lower.Contains("left"))
                    return interactor;
                if (rightPressed && lower.Contains("right"))
                    return interactor;
            }

            return fallback;
        }

        void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (args.interactorObject is XRBaseInteractor inputInteractor)
                _hovering.Add(inputInteractor);
            _hoverCount++;
        }

        void OnHoverExited(HoverExitEventArgs args)
        {
            if (args.interactorObject is XRBaseInteractor inputInteractor)
                _hovering.Remove(inputInteractor);
            _hoverCount = Mathf.Max(0, _hoverCount - 1);
        }

        void OnResetRequested(int _)
        {
            _nextAllowedRealtime = 0f;
        }
    }
}
