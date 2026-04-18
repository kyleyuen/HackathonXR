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

        /// <summary>Minor deterioration when learner attempts wrong action.</summary>
        public static PatientVisualState Worsen(in PatientVisualState current)
        {
            return new PatientVisualState
            {
                BreathRate = current.BreathRate * 0.85f,
                Consciousness = current.Consciousness * 0.8f,
                Cyanosis = UnityEngine.Mathf.Min(1f, current.Cyanosis + 0.07f),
                HeadSlump = UnityEngine.Mathf.Min(1f, current.HeadSlump + 0.05f)
            };
        }
    }
}
