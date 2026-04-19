using System;
using UnityEngine;

namespace RRX.Core
{
    /// <summary>Optional clock for urgency / timeout pressure.</summary>
    public sealed class ScenarioClock : MonoBehaviour
    {
        [SerializeField] float _warnSeconds = 60f;
        [SerializeField] float _expireSeconds = 90f;

        bool _running;
        bool _warned;
        float _startedRealtime;

        public event Action Warned;
        public event Action Expired;

        public float ElapsedSeconds => _running ? Time.realtimeSinceStartup - _startedRealtime : 0f;
        public float WarnSeconds => _warnSeconds;

        public void StartClock()
        {
            _startedRealtime = Time.realtimeSinceStartup;
            _running = true;
            _warned = false;
        }

        public void ResetClock()
        {
            _running = false;
            _warned = false;
            _startedRealtime = 0f;
        }

        public void SetElapsedSeconds(float elapsedSeconds)
        {
            var clamped = Mathf.Max(0f, elapsedSeconds);
            _startedRealtime = Time.realtimeSinceStartup - clamped;
            _running = true;
            _warned = clamped >= _warnSeconds;
        }

        void Update()
        {
            if (!_running)
                return;

            float elapsed = Time.realtimeSinceStartup - _startedRealtime;
            if (!_warned && elapsed >= _warnSeconds)
            {
                _warned = true;
                Warned?.Invoke();
            }

            if (elapsed >= _expireSeconds)
            {
                _running = false;
                Expired?.Invoke();
            }
        }
    }
}
