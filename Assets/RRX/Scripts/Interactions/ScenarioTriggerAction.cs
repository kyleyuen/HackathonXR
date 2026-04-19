using RRX.Core;
using UnityEngine;

namespace RRX.Interactions
{
    /// <summary>Simple trigger volume → scenario action (shoulder check, zones).</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ScenarioTriggerAction : MonoBehaviour
    {
        [SerializeField] ScenarioRunner _runner;
        [SerializeField] ScenarioAction _action = ScenarioAction.CheckResponsiveness;
        [Tooltip("If set, only colliders with this tag register.")]
        [SerializeField] string _requireTag;

        void OnTriggerEnter(Collider other)
        {
            if (!string.IsNullOrEmpty(_requireTag) && !other.CompareTag(_requireTag))
                return;
            if (_runner == null)
                return;

            var submission = new ScenarioActionSubmission(
                _action,
                ScenarioHotspotId.None,
                null,
                Time.realtimeSinceStartup);
            _runner.TrySubmit(submission, out _);
        }
    }
}
