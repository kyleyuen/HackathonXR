using RRX.Core;
using RRX.UI;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace RRX.Runtime
{
    /// <summary>Audio + haptic feedback for scenario submissions.</summary>
    [DisallowMultipleComponent]
    public sealed class RRXScenarioFeedback : MonoBehaviour
    {
        [SerializeField] ScenarioRunner _runner;
        [SerializeField] RRXProceduralAudio _audioBank;
        [SerializeField] AudioSource _audioSource;
        [SerializeField] RRXWristObjectivePanel _wristPanel;

        [SerializeField] float _okHapticAmplitude = 0.35f;
        [SerializeField] float _okHapticDuration = 0.08f;
        [SerializeField] float _badHapticAmplitude = 0.65f;
        [SerializeField] float _badHapticDuration = 0.12f;

        void Awake()
        {
            if (_runner == null)
                _runner = FindObjectOfType<ScenarioRunner>();
            if (_audioBank == null)
                _audioBank = GetComponent<RRXProceduralAudio>();
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();
            if (_wristPanel == null)
                _wristPanel = FindObjectOfType<RRXWristObjectivePanel>();
        }

        void OnEnable()
        {
            if (_runner == null)
                return;

            _runner.OnSubmissionResolved += OnSubmissionResolved;
            _runner.OnStateChanged.AddListener(OnStateChanged);
            _runner.OnResetRequested += OnResetRequested;
        }

        void OnDisable()
        {
            if (_runner == null)
                return;

            _runner.OnSubmissionResolved -= OnSubmissionResolved;
            _runner.OnStateChanged.RemoveListener(OnStateChanged);
            _runner.OnResetRequested -= OnResetRequested;
        }

        void OnSubmissionResolved(ScenarioActionSubmission submission, ScenarioSubmissionResult result, string _)
        {
            bool ok = result == ScenarioSubmissionResult.Accepted;
            if (ok)
            {
                PlayClip(_audioBank != null ? _audioBank.ClipOk : null, 0.75f);
                SendHaptic(submission.Interactor, _okHapticAmplitude, _okHapticDuration);
            }
            else
            {
                float failure01 = _runner != null ? Mathf.Clamp01(_runner.FailureCount / 3f) : 0f;
                PlayClip(_audioBank != null ? _audioBank.ClipBad : null, Mathf.Lerp(0.5f, 1f, failure01));
                SendHaptic(submission.Interactor, _badHapticAmplitude, _badHapticDuration);
                _wristPanel?.FlashFailure();
            }
        }

        void OnStateChanged(ScenarioState state)
        {
            if (state == ScenarioState.Recovery)
                PlayClip(_audioBank != null ? _audioBank.ClipRecovered : null, 1f);
        }

        void OnResetRequested(int _)
        {
            if (_audioSource != null)
                _audioSource.Stop();
        }

        void PlayClip(AudioClip clip, float volumeScale)
        {
            if (clip == null || _audioSource == null)
                return;
            _audioSource.PlayOneShot(clip, volumeScale);
        }

        static void SendHaptic(XRBaseInteractor interactor, float amplitude, float duration)
        {
            if (interactor == null)
                return;
            var send = interactor.GetType().GetMethod(
                "SendHapticImpulse",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(float), typeof(float) },
                null);
            if (send != null)
                send.Invoke(interactor, new object[] { amplitude, duration });
        }
    }
}
