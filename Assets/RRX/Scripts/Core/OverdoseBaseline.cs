namespace RRX.Core
{
    /// <summary>Static tuning for victim presentation across scenario beats.</summary>
    public static class OverdoseBaseline
    {
        public static PatientVisualState ForState(ScenarioState state)
        {
            switch (state)
            {
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
                case ScenarioState.CallForHelp:
                    return new PatientVisualState
                    {
                        BreathRate = 0.28f,
                        Consciousness = 0.15f,
                        Cyanosis = 0.62f,
                        HeadSlump = 0.72f
                    };
                case ScenarioState.AdministerNarcan:
                    return new PatientVisualState
                    {
                        BreathRate = 0.22f,
                        Consciousness = 0.08f,
                        Cyanosis = 0.72f,
                        HeadSlump = 0.82f
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
