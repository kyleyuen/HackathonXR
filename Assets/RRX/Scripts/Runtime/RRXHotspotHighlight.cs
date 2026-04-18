using RRX.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace RRX.Runtime
{
    /// <summary>Highlights the currently required hotspot from the scenario registry.</summary>
    [DisallowMultipleComponent]
    public sealed class RRXHotspotHighlight : MonoBehaviour
    {
        [SerializeField] ScenarioRunner _runner;
        [SerializeField] RRXScenarioHotspotTag _tag;
        [SerializeField] XRBaseInteractable _interactable;
        [SerializeField] Renderer _renderer;
        [SerializeField] Color _inactiveColor = new Color(0.35f, 0.2f, 0.2f, 0.55f);
        [SerializeField] Color _activeColor = new Color(0.25f, 1f, 0.35f, 0.75f);
        [SerializeField] Color _hoverColor = new Color(0.15f, 0.95f, 1f, 0.8f);
        [SerializeField] float _pulseSpeed = 4f;

        bool _isCurrentTarget;
        bool _isHovered;
        Material _materialInstance;

        void Awake()
        {
            if (_runner == null)
                _runner = FindObjectOfType<ScenarioRunner>();
            if (_tag == null)
                _tag = GetComponent<RRXScenarioHotspotTag>();
            if (_interactable == null)
                _interactable = GetComponent<XRBaseInteractable>();
            if (_renderer == null)
                _renderer = GetComponent<Renderer>();

            if (_renderer != null)
                _materialInstance = _renderer.material;
        }

        void OnEnable()
        {
            if (_runner != null)
            {
                _runner.OnStateChanged.AddListener(OnStateChanged);
                _runner.OnResetRequested += OnResetRequested;
            }

            if (_interactable != null)
            {
                _interactable.hoverEntered.AddListener(OnHoverEntered);
                _interactable.hoverExited.AddListener(OnHoverExited);
            }

            EvaluateTarget();
        }

        void OnDisable()
        {
            if (_runner != null)
            {
                _runner.OnStateChanged.RemoveListener(OnStateChanged);
                _runner.OnResetRequested -= OnResetRequested;
            }

            if (_interactable != null)
            {
                _interactable.hoverEntered.RemoveListener(OnHoverEntered);
                _interactable.hoverExited.RemoveListener(OnHoverExited);
            }
        }

        void Update()
        {
            if (_materialInstance == null)
                return;

            Color target;
            if (_isHovered)
                target = _hoverColor;
            else if (_isCurrentTarget)
            {
                float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * _pulseSpeed);
                target = _activeColor * pulse;
                target.a = _activeColor.a;
            }
            else
                target = _inactiveColor;

            _materialInstance.color = target;
        }

        public void PulseNow()
        {
            _isCurrentTarget = true;
        }

        void OnStateChanged(ScenarioState _)
        {
            EvaluateTarget();
        }

        void OnResetRequested(int _)
        {
            _isHovered = false;
            EvaluateTarget();
        }

        void EvaluateTarget()
        {
            if (_runner == null || _tag == null)
            {
                _isCurrentTarget = false;
                return;
            }

            _isCurrentTarget = _runner.NextRequiredHotspot == _tag.HotspotId;
        }

        void OnHoverEntered(HoverEnterEventArgs _)
        {
            _isHovered = true;
        }

        void OnHoverExited(HoverExitEventArgs _)
        {
            _isHovered = false;
        }
    }
}
