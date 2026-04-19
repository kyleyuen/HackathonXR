using RRX.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace RRX.Interactions
{
    /// <summary>
    /// Fires a scenario action when any <see cref="XRBaseInteractable"/> on this GameObject is selected
    /// (grab or ray/poke select). Works with both <see cref="XRGrabInteractable"/> (phone pickup) and
    /// <see cref="XRSimpleInteractable"/> (patient body hotspots that should not be pickupable).
    /// </summary>
    public sealed class ScenarioXRSelectAction : MonoBehaviour
    {
        [SerializeField] ScenarioRunner _runner;
        [SerializeField] ScenarioAction _action = ScenarioAction.AdministerNarcan;
        [SerializeField] bool _once = true;

        XRBaseInteractable _interactable;
        bool _fired;

        void Awake()
        {
            _interactable = GetComponent<XRBaseInteractable>();
            if (_interactable != null)
                _interactable.selectEntered.AddListener(OnSelect);
        }

        void OnDestroy()
        {
            if (_interactable != null)
                _interactable.selectEntered.RemoveListener(OnSelect);
        }

        public void SetRunner(ScenarioRunner runner) => _runner = runner;
        public void SetAction(ScenarioAction action) => _action = action;

        void OnSelect(SelectEnterEventArgs args)
        {
            if (_once && _fired) return;
            _fired = true;
            if (_runner == null)
                return;

            var interactor = args.interactorObject as XRBaseInteractor;
            var submission = new ScenarioActionSubmission(
                _action,
                ScenarioHotspotId.None,
                interactor,
                Time.realtimeSinceStartup);
            _runner.TrySubmit(submission, out _);
        }
    }
}
