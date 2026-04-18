using System;
using UnityEngine;

namespace RRX.Core
{
    /// <summary>Optional clock for urgency / timeout pressure.</summary>
    public sealed class ScenarioClock : MonoBehaviour
    {
        [SerializeField] float _warnSeconds = 90f;
        [SerializeField] float _expireSeconds = 120f;

        bool _running;
        bool _warned;
        float _startedRealtime;

        public event Action Warned;
        public event Action Expired;

        public float ElapsedSeconds => _running ? Time.realtimeSinceStartup - _startedRealtime : 0f;

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
