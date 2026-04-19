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
        [SerializeField] bool _timePressureEnabled = true;
        [SerializeField] int _maxFailures = 3;
        [SerializeField] float _runnerCooldownSeconds = 0.15f;
        [SerializeField] float _duplicateSuppressionSeconds = 0.6f;
        [SerializeField] ScenarioClock _clock;

        readonly RewindSnapshotStore _snapshots = new RewindSnapshotStore();
        SessionTelemetry _telemetry;

        ScenarioState _state = ScenarioState.SceneSafety;
        PatientVisualState _patientVisual;
        bool _phoneConnected;
        bool _narcanUsed;
        float _scenarioStartTime;
        int _rewindCount;
        int _failureCount;
        int _resetGeneration;
        float _lastSubmitRealtime = -1f;
        float _lastAcceptedRealtime = -1f;
        ScenarioAction _lastAcceptedAction = ScenarioAction.None;
        bool _clockStarted;

        public ScenarioState CurrentState => _state;
        public PatientVisualState CurrentPatientVisual => _patientVisual;
        public RewindSnapshotStore Snapshots => _snapshots;
        public int RewindCount => _rewindCount;
        public int FailureCount => _failureCount;
        public ScenarioAction NextRequiredAction => ScenarioHotspotRegistry.ActionFor(ScenarioHotspotRegistry.ExpectedHotspot(_state));
        public ScenarioHotspotId NextRequiredHotspot => ScenarioHotspotRegistry.ExpectedHotspot(_state);

        public UnityEvent<ScenarioState> OnStateChanged;
        public UnityEvent<ScenarioAction, string> OnActionHandled;
        public event Action<ScenarioActionSubmission, ScenarioSubmissionResult, string> OnSubmissionResolved;
        public event Action<PatientVisualState> OnPatientSnapshot;
        public event Action<int> OnResetRequested;
        public event Action OnTimePressureWarning;

        void Awake()
        {
            _telemetry = new SessionTelemetry();
            if (OnStateChanged == null)
                OnStateChanged = new UnityEvent<ScenarioState>();
            if (OnActionHandled == null)
                OnActionHandled = new UnityEvent<ScenarioAction, string>();
            if (_clock == null)
                _clock = GetComponent<ScenarioClock>();
        }

        void OnEnable()
        {
            if (_clock != null)
            {
                _clock.Warned += OnClockWarned;
                _clock.Expired += OnClockExpired;
            }
        }

        void Start()
        {
            _scenarioStartTime = Time.time;
            _telemetry.Begin();
            _patientVisual = OverdoseBaseline.ForState(_state);
            ApplyPatientSnapshot(_patientVisual);
            _snapshots.Clear();
            _snapshots.Push(CreateCheckpoint(ScenarioState.SceneSafety));
            RaiseStateChanged();
            LogTelemetry(ScenarioAction.None, "enter");

            // Clock starts immediately so idle time costs the player
            if (_timePressureEnabled && _clock != null)
            {
                _clock.StartClock();
                _clockStarted = true;
            }
        }

        void OnDisable()
        {
            if (_clock != null)
            {
                _clock.Warned -= OnClockWarned;
                _clock.Expired -= OnClockExpired;
            }
        }

        void OnDestroy()
        {
            _telemetry?.End();
        }

        [Obsolete("Use TrySubmit(in ScenarioActionSubmission, out string).")]
        public void SubmitAction(ScenarioAction action)
        {
            if (action == ScenarioAction.Rewind)
            {
                Debug.LogWarning("[RRX] Use RewindToCheckpoint(int) for rewind.");
                return;
            }

            var submission = new ScenarioActionSubmission(
                action,
                ScenarioHotspotId.None,
                null,
                Time.realtimeSinceStartup);
            TrySubmit(submission, out _);
        }

        public ScenarioSubmissionResult TrySubmit(in ScenarioActionSubmission submission)
        {
            return TrySubmit(submission, out _);
        }

        public ScenarioSubmissionResult TrySubmit(in ScenarioActionSubmission submission, out string reason)
        {
            reason = string.Empty;
            var now = submission.SubmittedAtRealtime > 0f ? submission.SubmittedAtRealtime : Time.realtimeSinceStartup;

            ScenarioSubmissionResult result;
            if (_state == ScenarioState.CriticalFailure || _state == ScenarioState.Recovery)
            {
                result = ScenarioSubmissionResult.RejectedTerminalState;
                reason = "terminal_state";
            }
            else if (_lastSubmitRealtime > 0f && now - _lastSubmitRealtime < _runnerCooldownSeconds)
            {
                result = ScenarioSubmissionResult.RejectedThrottled;
                reason = "throttled";
            }
            else if (_lastAcceptedAction == submission.Action &&
                     _lastAcceptedRealtime > 0f &&
                     now - _lastAcceptedRealtime < _duplicateSuppressionSeconds)
            {
                result = ScenarioSubmissionResult.RejectedDuplicate;
                reason = "duplicate";
            }
            else
            {
                var expectedAction = NextRequiredAction;
                var expectedHotspot = NextRequiredHotspot;

                if (expectedAction == ScenarioAction.None)
                {
                    result = ScenarioSubmissionResult.RejectedInvalid;
                    reason = "no_expected_action";
                }
                else if (submission.Action != expectedAction)
                {
                    result = ScenarioSubmissionResult.RejectedWrongAction;
                    reason = $"expected_{expectedAction}";
                    EscalateFailure(reason);
                }
                else if (expectedHotspot != ScenarioHotspotId.None &&
                         submission.HotspotId != ScenarioHotspotId.None &&
                         submission.HotspotId != expectedHotspot)
                {
                    result = ScenarioSubmissionResult.RejectedOutOfOrder;
                    reason = "out_of_order_hotspot";
                    EscalateFailure(reason);
                }
                else
                {
                    result = ScenarioSubmissionResult.Accepted;
                    reason = "ok";
                    ApplyAcceptedAction(submission.Action);
                    _lastAcceptedAction = submission.Action;
                    _lastAcceptedRealtime = now;
                    // Clock already started in Start(); no need to re-start on first action
                }
            }

            _lastSubmitRealtime = now;
            FinalizeSubmission(submission, result, reason);
            return result;
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
            _failureCount = cp.FailureCount;
            _scenarioStartTime = Time.time - cp.ScenarioTimeSeconds;
            _lastAcceptedAction = ScenarioAction.None;
            _lastAcceptedRealtime = -1f;
            _lastSubmitRealtime = -1f;

            ApplyPatientSnapshot(_patientVisual);
            _snapshots.TrimAfter(index);
            RaiseStateChanged();

            LogTelemetry(ScenarioAction.Rewind, $"rewind_to_{index}");
            OnActionHandled?.Invoke(ScenarioAction.Rewind, $"checkpoint_{index}");
        }

        public void RewindPreviousCheckpoint()
        {
            if (_snapshots.LastIndex <= 0) return;
            RewindToCheckpoint(_snapshots.LastIndex - 1);
        }

        public void ResetScenario()
        {
            _state = ScenarioState.SceneSafety;
            _phoneConnected = false;
            _narcanUsed = false;
            _failureCount = 0;
            _lastAcceptedAction = ScenarioAction.None;
            _lastSubmitRealtime = -1f;
            _lastAcceptedRealtime = -1f;
            _scenarioStartTime = Time.time;

            _patientVisual = OverdoseBaseline.ForState(_state);
            ApplyPatientSnapshot(_patientVisual);
            _snapshots.Clear();
            _snapshots.Push(CreateCheckpoint(ScenarioState.SceneSafety));
            RaiseStateChanged();

            _resetGeneration++;
            OnResetRequested?.Invoke(_resetGeneration);
            if (_clock != null)
            {
                _clock.ResetClock();
                _clockStarted = false;
                if (_timePressureEnabled)
                {
                    _clock.StartClock();
                    _clockStarted = true;
                }
            }

            LogTelemetry(ScenarioAction.None, "reset");
        }

        void ApplyAcceptedAction(ScenarioAction action)
        {
            switch (_state)
            {
                case ScenarioState.SceneSafety when action == ScenarioAction.ScanScene:
                    _state = ScenarioState.Arrival;
                    break;
                case ScenarioState.Arrival when action == ScenarioAction.CheckResponsiveness:
                    _state = ScenarioState.OpenAirway;
                    break;
                case ScenarioState.OpenAirway when action == ScenarioAction.OpenAirway:
                    _state = ScenarioState.CheckBreathing;
                    break;
                case ScenarioState.CheckBreathing when action == ScenarioAction.CheckBreathing:
                    _state = ScenarioState.CallForHelp;
                    break;
                case ScenarioState.CallForHelp when action == ScenarioAction.Call911:
                    _phoneConnected = true;
                    _state = ScenarioState.AdministerNarcan;
                    break;
                case ScenarioState.AdministerNarcan when action == ScenarioAction.AdministerNarcan:
                    _narcanUsed = true;
                    _state = ScenarioState.RecoveryPosition;
                    break;
                case ScenarioState.RecoveryPosition when action == ScenarioAction.RecoveryPosition:
                    _state = ScenarioState.Recovery;
                    break;
                default:
                    TransitionToTerminal(ScenarioState.CriticalFailure, "invalid_transition");
                    return;
            }

            _patientVisual = OverdoseBaseline.ForState(_state);
            ApplyPatientSnapshot(_patientVisual);
            _snapshots.Push(CreateCheckpoint(_state));
            RaiseStateChanged();
        }

        void EscalateFailure(string reason)
        {
            _failureCount++;
            var baseline = OverdoseBaseline.ForState(_state);
            _patientVisual = OverdoseBaseline.Worsen(baseline, _failureCount);
            ApplyPatientSnapshot(_patientVisual);

            if (_failureCount >= _maxFailures)
                TransitionToTerminal(ScenarioState.CriticalFailure, $"too_many_failures:{reason}");
        }

        void TransitionToTerminal(ScenarioState terminal, string reason)
        {
            _state = terminal;
            _patientVisual = OverdoseBaseline.ForState(_state);
            ApplyPatientSnapshot(_patientVisual);
            RaiseStateChanged();
            if (_logToConsole)
                Debug.Log($"[RRX] Terminal transition -> {terminal} ({reason})");
        }

        ScenarioCheckpoint CreateCheckpoint(ScenarioState checkpointLabelState)
        {
            return new ScenarioCheckpoint
            {
                State = checkpointLabelState,
                Patient = _patientVisual,
                PhoneConnected = _phoneConnected,
                NarcanUsed = _narcanUsed,
                FailureCount = _failureCount,
                ScenarioTimeSeconds = Time.time - _scenarioStartTime,
                RewindGeneration = _rewindCount
            };
        }

        void ApplyPatientSnapshot(in PatientVisualState snapshot)
        {
            _patient?.Apply(snapshot);
            OnPatientSnapshot?.Invoke(snapshot);
        }

        void RaiseStateChanged()
        {
            OnStateChanged?.Invoke(_state);
        }

        void FinalizeSubmission(in ScenarioActionSubmission submission, ScenarioSubmissionResult result, string reason)
        {
            if (_logToConsole)
                Debug.Log($"[RRX] Action {submission.Action} -> {result} ({reason}) state={_state}");

            OnSubmissionResolved?.Invoke(submission, result, reason);
            OnActionHandled?.Invoke(submission.Action, reason);
            LogTelemetry(submission.Action, result == ScenarioSubmissionResult.Accepted ? "ok" : reason);
        }

        void OnClockWarned()
        {
            OnTimePressureWarning?.Invoke();
        }

        void OnClockExpired()
        {
            TransitionToTerminal(ScenarioState.CriticalFailure, "timeout");
            var timeoutSubmission = new ScenarioActionSubmission(
                ScenarioAction.None,
                ScenarioHotspotId.None,
                null,
                Time.realtimeSinceStartup);
            FinalizeSubmission(timeoutSubmission, ScenarioSubmissionResult.RejectedInvalid, "timeout");
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
