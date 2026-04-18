using RRX.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace RRX.Interactions
{
    /// <summary>Fires a scenario action when an <see cref="XRGrabInteractable"/> is selected (grab).</summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class ScenarioXRSelectAction : MonoBehaviour
    {
        [SerializeField] ScenarioRunner _runner;
        [SerializeField] ScenarioAction _action = ScenarioAction.AdministerNarcan;
        [SerializeField] bool _once = true;

        XRGrabInteractable _grab;
        bool _fired;

        void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _grab.selectEntered.AddListener(OnSelect);
        }

        void OnDestroy()
        {
            if (_grab != null)
                _grab.selectEntered.RemoveListener(OnSelect);
        }

        void OnSelect(SelectEnterEventArgs _)
        {
            if (_once && _fired) return;
            _fired = true;
            _runner?.SubmitAction(_action);
        }
    }
}
