using RRX.Core;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace RRX.Interactions
{
    /// <summary>Keyboard fallback for headset-less testing (Editor / Development).</summary>
    public sealed class ScenarioDebugHotkeys : MonoBehaviour
    {
        [SerializeField] ScenarioRunner _runner;

        void Submit(ScenarioAction action)
        {
            if (_runner == null) return;
            var submission = new ScenarioActionSubmission(
                action,
                ScenarioHotspotId.None,
                null,
                Time.realtimeSinceStartup);
            _runner.TrySubmit(submission, out _);
        }

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_runner == null) return;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame)
                Submit(ScenarioAction.CheckResponsiveness);
            if (kb.digit2Key.wasPressedThisFrame)
                Submit(ScenarioAction.Call911);
            if (kb.digit3Key.wasPressedThisFrame)
                Submit(ScenarioAction.AdministerNarcan);
            if (kb.rKey.wasPressedThisFrame)
                _runner.RewindPreviousCheckpoint();
#else
            if (Input.GetKeyDown(KeyCode.Alpha1))
                Submit(ScenarioAction.CheckResponsiveness);
            if (Input.GetKeyDown(KeyCode.Alpha2))
                Submit(ScenarioAction.Call911);
            if (Input.GetKeyDown(KeyCode.Alpha3))
                Submit(ScenarioAction.AdministerNarcan);
            if (Input.GetKeyDown(KeyCode.R))
                _runner.RewindPreviousCheckpoint();
#endif
#endif
        }
    }
}
