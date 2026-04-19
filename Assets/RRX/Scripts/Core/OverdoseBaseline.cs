namespace RRX.Core
{
    /// <summary>Static tuning for victim presentation across all 7 scenario beats.</summary>
    public static class OverdoseBaseline
    {
        public static PatientVisualState ForState(ScenarioState state)
        {
            switch (state)
            {
                case ScenarioState.SceneSafety:
                    return new PatientVisualState
                    {
                        BreathRate = 0.40f,
                        Consciousness = 0.30f,
                        Cyanosis = 0.45f,
                        HeadSlump = 0.55f
                    };
                case ScenarioState.Arrival:
                    return new PatientVisualState
                    {
                        BreathRate = 0.35f,
                        Consciousness = 0.25f,
                        Cyanosis = 0.55f,
                        HeadSlump = 0.65f
                    };
                case ScenarioState.AssessResponsiveness:
                    return ForState(ScenarioState.Arrival);
                case ScenarioState.OpenAirway:
                    return new PatientVisualState
                    {
                        BreathRate = 0.30f,
                        Consciousness = 0.18f,
                        Cyanosis = 0.60f,
                        HeadSlump = 0.70f
                    };
                case ScenarioState.CheckBreathing:
                    return new PatientVisualState
                    {
                        BreathRate = 0.25f,
                        Consciousness = 0.12f,
                        Cyanosis = 0.65f,
                        HeadSlump = 0.75f
                    };
                case ScenarioState.CallForHelp:
                    return new PatientVisualState
                    {
                        BreathRate = 0.20f,
                        Consciousness = 0.08f,
                        Cyanosis = 0.72f,
                        HeadSlump = 0.80f
                    };
                case ScenarioState.AdministerNarcan:
                    return new PatientVisualState
                    {
                        BreathRate = 0.15f,
                        Consciousness = 0.04f,
                        Cyanosis = 0.82f,
                        HeadSlump = 0.90f
                    };
                case ScenarioState.RecoveryPosition:
                    return new PatientVisualState
                    {
                        BreathRate = 0.55f,
                        Consciousness = 0.40f,
                        Cyanosis = 0.30f,
                        HeadSlump = 0.40f
                    };
                case ScenarioState.Recovery:
                    return new PatientVisualState
                    {
                        BreathRate = 0.85f,
                        Consciousness = 0.75f,
                        Cyanosis = 0.15f,
                        HeadSlump = 0.25f
                    };
                case ScenarioState.CriticalFailure:
                default:
                    return new PatientVisualState
                    {
                        BreathRate = 0.05f,
                        Consciousness = 0f,
                        Cyanosis = 0.95f,
                        HeadSlump = 0.95f
                    };
            }
        }

        /// <summary>
        /// Compounding deterioration by failure count, applied against the current state's baseline snapshot.
        /// </summary>
        public static PatientVisualState Worsen(in PatientVisualState baseline, int failureCount)
        {
            float fail = UnityEngine.Mathf.Max(1, failureCount);
            float breathScale = UnityEngine.Mathf.Pow(0.85f, fail);
            float consciousnessScale = UnityEngine.Mathf.Pow(0.8f, fail);
            float cyanosisLift = 1f - UnityEngine.Mathf.Pow(0.93f, fail);
            float slumpLift = 1f - UnityEngine.Mathf.Pow(0.94f, fail);

            return new PatientVisualState
            {
                BreathRate = UnityEngine.Mathf.Clamp01(baseline.BreathRate * breathScale),
                Consciousness = UnityEngine.Mathf.Clamp01(baseline.Consciousness * consciousnessScale),
                Cyanosis = UnityEngine.Mathf.Clamp01(baseline.Cyanosis + cyanosisLift),
                HeadSlump = UnityEngine.Mathf.Clamp01(baseline.HeadSlump + slumpLift)
            };
        }
    }
}
