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

        void HandleNumberedAction(int number)
        {
            switch (number)
            {
                case 1:
                    Submit(ScenarioAction.ScanScene);
                    break;
                case 2:
                    Submit(ScenarioAction.CheckResponsiveness);
                    break;
                case 3:
                    Submit(ScenarioAction.OpenAirway);
                    break;
                case 4:
                    Submit(ScenarioAction.CheckBreathing);
                    break;
                case 5:
                    Submit(ScenarioAction.Call911);
                    break;
                case 6:
                    Submit(ScenarioAction.AdministerNarcan);
                    break;
                case 7:
                    Submit(ScenarioAction.RecoveryPosition);
                    break;
            }
        }

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
                HandleNumberedAction(1);
            if (kb.digit2Key.wasPressedThisFrame)
                HandleNumberedAction(2);
            if (kb.digit3Key.wasPressedThisFrame)
                HandleNumberedAction(3);
            if (kb.digit4Key.wasPressedThisFrame)
                HandleNumberedAction(4);
            if (kb.digit5Key.wasPressedThisFrame)
                HandleNumberedAction(5);
            if (kb.digit6Key.wasPressedThisFrame)
                HandleNumberedAction(6);
            if (kb.digit7Key.wasPressedThisFrame)
                HandleNumberedAction(7);
            if (kb.rKey.wasPressedThisFrame)
                _runner.RewindPreviousCheckpoint();
#else
            if (Input.GetKeyDown(KeyCode.Alpha1))
                HandleNumberedAction(1);
            if (Input.GetKeyDown(KeyCode.Alpha2))
                HandleNumberedAction(2);
            if (Input.GetKeyDown(KeyCode.Alpha3))
                HandleNumberedAction(3);
            if (Input.GetKeyDown(KeyCode.Alpha4))
                HandleNumberedAction(4);
            if (Input.GetKeyDown(KeyCode.Alpha5))
                HandleNumberedAction(5);
            if (Input.GetKeyDown(KeyCode.Alpha6))
                HandleNumberedAction(6);
            if (Input.GetKeyDown(KeyCode.Alpha7))
                HandleNumberedAction(7);
            if (Input.GetKeyDown(KeyCode.R))
                _runner.RewindPreviousCheckpoint();
#endif
#endif
        }
    }
}
