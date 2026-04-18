using System.Collections.Generic;
using UnityEngine;

namespace RRX.Core
{
    /// <summary>Discrete checkpoint rewind — not continuous time simulation.</summary>
    public sealed class RewindSnapshotStore
    {
        readonly List<ScenarioCheckpoint> _checkpoints = new List<ScenarioCheckpoint>();

        public IReadOnlyList<ScenarioCheckpoint> Checkpoints => _checkpoints;

        public void Push(ScenarioCheckpoint checkpoint)
        {
            if (checkpoint == null) return;
            _checkpoints.Add(checkpoint);
            Debug.Log($"[RRX] Checkpoint saved: {_checkpoints.Count - 1} → {checkpoint.State}");
        }

        /// <summary>Keep checkpoints [0 .. keepThroughInclusive]; drop later indices (branch discarded after rewind).</summary>
        public void TrimAfter(int keepThroughInclusive)
        {
            if (_checkpoints.Count == 0) return;
            var removeFrom = Mathf.Clamp(keepThroughInclusive + 1, 0, _checkpoints.Count);
            if (removeFrom < _checkpoints.Count)
                _checkpoints.RemoveRange(removeFrom, _checkpoints.Count - removeFrom);
        }

        public ScenarioCheckpoint Get(int index)
        {
            if (index < 0 || index >= _checkpoints.Count) return null;
            return _checkpoints[index];
        }

        public void Clear()
        {
            _checkpoints.Clear();
        }

        public int LastIndex => _checkpoints.Count > 0 ? _checkpoints.Count - 1 : -1;
    }
}
