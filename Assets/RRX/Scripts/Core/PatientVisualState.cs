using System;
using UnityEngine;

namespace RRX.Core
{
    /// <summary>Data-driven presentation of victim condition; animator reads these parameters.</summary>
    [Serializable]
    public struct PatientVisualState
    {
        [Range(0f, 2f)] public float BreathRate;
        [Range(0f, 1f)] public float Consciousness;
        [Range(0f, 1f)] public float Cyanosis;
        [Range(0f, 1f)] public float HeadSlump;

        public static PatientVisualState Lerp(in PatientVisualState a, in PatientVisualState b, float t)
        {
            return new PatientVisualState
            {
                BreathRate = Mathf.Lerp(a.BreathRate, b.BreathRate, t),
                Consciousness = Mathf.Lerp(a.Consciousness, b.Consciousness, t),
                Cyanosis = Mathf.Lerp(a.Cyanosis, b.Cyanosis, t),
                HeadSlump = Mathf.Lerp(a.HeadSlump, b.HeadSlump, t)
            };
        }
    }
}
