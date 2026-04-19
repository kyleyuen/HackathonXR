using System;
using UnityEngine;

namespace RRX.Core
{
    /// <summary>Checkpoint-sized rewind payload (no full physics).</summary>
    [Serializable]
    public class ScenarioCheckpoint
    {
        public ScenarioState State;
        public PatientVisualState Patient;
        public bool PhoneConnected;
        public bool NarcanUsed;
        public int FailureCount;
        public float ScenarioTimeSeconds;
        public float ClockElapsedSeconds;
        public int RewindGeneration;
    }
}
