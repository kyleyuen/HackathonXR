using RRX.Core;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RRX.UI
{
    /// <summary>
    /// World-space overdose scenario HUD root: binds optional UI <see cref="UnityEngine.UI.Button"/>s to
    /// <see cref="ScenarioRunner"/> and assigns the XR camera for world canvases.
    /// </summary>
    public sealed class RRXWorldScenarioUiRoot : MonoBehaviour
    {
        [SerializeField] ScenarioRunner _runner;
        [SerializeField] Canvas _canvas;
        [SerializeField] XROrigin _xrOrigin;
        [SerializeField] Component _check;
        [SerializeField] Component _call;
        [SerializeField] Component _narcan;
        [SerializeField] Component _rewind;
        [SerializeField] Component _step;
        [SerializeField] Component _hint;

        void Awake()
        {
            if (_runner == null)
                _runner = FindObjectOfType<ScenarioRunner>();
            if (_canvas == null)
                _canvas = GetComponentInChildren<Canvas>(true);
            if (_xrOrigin == null)
                _xrOrigin = FindObjectOfType<XROrigin>();
        }

        void Start()
        {
            if (_canvas != null && _xrOrigin != null && _xrOrigin.Camera != null)
                _canvas.worldCamera = _xrOrigin.Camera;

            WireButton(_check, () => Submit(ScenarioAction.CheckResponsiveness));
            WireButton(_call, () => Submit(ScenarioAction.Call911));
            WireButton(_narcan, () => Submit(ScenarioAction.AdministerNarcan));
            WireButton(_rewind, () => _runner?.RewindPreviousCheckpoint());
        }

        static void WireButton(Component c, UnityAction action)
        {
            if (c == null || action == null)
                return;

            var button = c as Button ?? c.GetComponent<Button>();
            if (button == null)
                return;

            button.onClick.AddListener(action);
        }

        void Submit(ScenarioAction action)
        {
            if (_runner == null)
                return;
            var submission = new ScenarioActionSubmission(
                action,
                ScenarioHotspotId.WristUi,
                null,
                Time.realtimeSinceStartup);
            _runner.TrySubmit(submission, out _);
        }
    }
}
