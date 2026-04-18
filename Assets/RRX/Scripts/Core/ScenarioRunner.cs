using System;
using UnityEngine;
using UnityEngine.Events;

namespace RRX.Core
{
    /// <summary>Finite-state runner for Phase 1 overdose scenario + checkpoint rewind.</summary>
    public sealed class ScenarioRunner : MonoBehaviour
    {
        [SerializeField] PatientPresenter _patient;
        [SerializeField] bool _logToConsole = true;

        readonly RewindSnapshotStore _snapshots = new RewindSnapshotStore();
        SessionTelemetry _telemetry;

        ScenarioState _state = ScenarioState.Arrival;
        PatientVisualState _patientVisual;
        bool _phoneConnected;
        bool _narcanUsed;
        float _scenarioStartTime;
        int _rewindCount;

        public ScenarioState CurrentState => _state;
        public PatientVisualState CurrentPatientVisual => _patientVisual;
        public RewindSnapshotStore Snapshots => _snapshots;
        public int RewindCount => _rewindCount;

        public UnityEvent<ScenarioState> OnStateChanged;
        public UnityEvent<ScenarioAction, string> OnActionHandled;

        void Awake()
        {
            _telemetry = new SessionTelemetry();
        }

        void Start()
        {
            _scenarioStartTime = Time.time;
            _telemetry.Begin();
            _patientVisual = OverdoseBaseline.ForState(_state);
            _patient?.Apply(_patientVisual);
            _snapshots.Push(CreateCheckpoint(ScenarioState.Arrival));
            RaiseStateChanged();
            LogTelemetry(ScenarioAction.None, "enter");
        }

        void OnDestroy()
        {
            _telemetry?.End();
        }

        /// <summary>Entry point for XR interactables and debug UI.</summary>
        public void SubmitAction(ScenarioAction action)
        {
            if (action == ScenarioAction.Rewind)
            {
                Debug.LogWarning("[RRX] Use RewindToCheckpoint(int) for rewind.");
                return;
            }

            var stateBefore = _state;
            var ok = TryTransition(action, out var reason);
            if (_logToConsole)
                Debug.Log($"[RRX] Action {action} → ok={ok} reason={reason} state {stateBefore}->{_state}");

            OnActionHandled?.Invoke(action, reason);
            LogTelemetry(action, ok ? "ok" : reason);
        }

        public void RewindToCheckpoint(int index)
        {
            var cp = _snapshots.Get(index);
            if (cp == null)
            {
                LogTelemetry(ScenarioAction.Rewind, "invalid_checkpoint");
                return;
            }

            _rewindCount++;
            _state = cp.State;
            _patientVisual = cp.Patient;
            _phoneConnected = cp.PhoneConnected;
            _narcanUsed = cp.NarcanUsed;
            _scenarioStartTime = Time.time - cp.ScenarioTimeSeconds;

            _patient?.Apply(_patientVisual);
            _snapshots.TrimAfter(index);
            RaiseStateChanged();

            LogTelemetry(ScenarioAction.Rewind, $"rewind_to_{index}");
            OnActionHandled?.Invoke(ScenarioAction.Rewind, $"checkpoint_{index}");
        }

        /// <summary>Jump back one saved checkpoint (demo / hotkey).</summary>
        public void RewindPreviousCheckpoint()
        {
            if (_snapshots.LastIndex <= 0) return;
            RewindToCheckpoint(_snapshots.LastIndex - 1);
        }

        bool TryTransition(ScenarioAction action, out string reason)
        {
            reason = string.Empty;

            if (_state == ScenarioState.CriticalFailure || _state == ScenarioState.Recovery)
            {
                reason = "terminal_state";
                return false;
            }

            switch (_state)
            {
                case ScenarioState.Arrival:
                    if (action == ScenarioAction.CheckResponsiveness)
                    {
                        TransitionTo(ScenarioState.CallForHelp, CheckpointAfter.AssessDone);
                        return true;
                    }

                    reason = "expected_check_responsiveness";
                    PunishWrongAction();
                    return false;

                case ScenarioState.AssessResponsiveness:
                    // Unused in slim flow — kept for future expansion
                    reason = "unexpected_state";
                    return false;

                case ScenarioState.CallForHelp:
                    if (action == ScenarioAction.Call911)
                    {
                        _phoneConnected = true;
                        TransitionTo(ScenarioState.AdministerNarcan, CheckpointAfter.CallDone);
                        return true;
                    }

                    if (action == ScenarioAction.AdministerNarcan)
                    {
                        reason = "narcan_before_help";
                        TransitionToTerminal(ScenarioState.CriticalFailure);
                        return false;
                    }

                    reason = "expected_call_911";
                    PunishWrongAction();
                    return false;

                case ScenarioState.AdministerNarcan:
                    if (action == ScenarioAction.AdministerNarcan)
                    {
                        _narcanUsed = true;
                        TransitionTo(ScenarioState.Recovery, CheckpointAfter.NarcanDone);
                        return true;
                    }

                    reason = "expected_narcan";
                    PunishWrongAction();
                    return false;

                default:
                    reason = "unknown_state";
                    return false;
            }
        }

        enum CheckpointAfter
        {
            None,
            AssessDone,
            CallDone,
            NarcanDone
        }

        void TransitionTo(ScenarioState next, CheckpointAfter checkpointAfter)
        {
            _state = next;
            _patientVisual = OverdoseBaseline.ForState(_state);
            _patient?.Apply(_patientVisual);

            MaybePushCheckpoint(checkpointAfter);
            RaiseStateChanged();
        }

        void TransitionToTerminal(ScenarioState terminal)
        {
            _state = terminal;
            _patientVisual = OverdoseBaseline.ForState(terminal);
            _patient?.Apply(_patientVisual);
            RaiseStateChanged();
        }

        void MaybePushCheckpoint(CheckpointAfter when)
        {
            ScenarioCheckpoint cp = null;
            switch (when)
            {
                case CheckpointAfter.AssessDone:
                    cp = CreateCheckpoint(ScenarioState.CallForHelp);
                    break;
                case CheckpointAfter.CallDone:
                    cp = CreateCheckpoint(ScenarioState.AdministerNarcan);
                    break;
                case CheckpointAfter.NarcanDone:
                    cp = CreateCheckpoint(ScenarioState.Recovery);
                    break;
                case CheckpointAfter.None:
                    return;
            }

            _snapshots.Push(cp);
        }

        ScenarioCheckpoint CreateCheckpoint(ScenarioState checkpointLabelState)
        {
            return new ScenarioCheckpoint
            {
                State = checkpointLabelState,
                Patient = _patientVisual,
                PhoneConnected = _phoneConnected,
                NarcanUsed = _narcanUsed,
                ScenarioTimeSeconds = Time.time - _scenarioStartTime,
                RewindGeneration = _rewindCount
            };
        }

        void PunishWrongAction()
        {
            _patientVisual = OverdoseBaseline.Worsen(_patientVisual);
            _patient?.Apply(_patientVisual);
        }

        void RaiseStateChanged()
        {
            OnStateChanged?.Invoke(_state);
        }

        void LogTelemetry(ScenarioAction action, string result)
        {
            if (_telemetry == null) return;
            var idx = _snapshots.LastIndex;
            _telemetry.Write(new TelemetryEvent(
                _telemetry.SessionId,
                Application.version,
                Time.realtimeSinceStartup,
                idx,
                _state,
                action,
                result,
                _rewindCount));
        }
    }
}
