using System;
using UnityEngine;

namespace RRX.Core
{
    /// <summary>Data-driven presentation of victim condition; animator / procedural visuals read these values.</summary>
    [Serializable]
    public struct PatientVisualState
    {
        /// <summary>Normalized breathing intensity/rate scalar (0 = none, 1 = strong/fast).</summary>
        [Range(0f, 1f)] public float BreathRate;
        public bool IsApnea;
        [Range(0f, 1f)] public float Consciousness;
        [Range(0f, 1f)] public float Cyanosis;
        [Range(0f, 1f)] public float HeadSlump;

        public static PatientVisualState Lerp(in PatientVisualState a, in PatientVisualState b, float t)
        {
            return new PatientVisualState
            {
                BreathRate = Mathf.Lerp(a.BreathRate, b.BreathRate, t),
                IsApnea = t >= 0.5f ? b.IsApnea : a.IsApnea,
                Consciousness = Mathf.Lerp(a.Consciousness, b.Consciousness, t),
                Cyanosis = Mathf.Lerp(a.Cyanosis, b.Cyanosis, t),
                HeadSlump = Mathf.Lerp(a.HeadSlump, b.HeadSlump, t)
            };
        }
    }
}
