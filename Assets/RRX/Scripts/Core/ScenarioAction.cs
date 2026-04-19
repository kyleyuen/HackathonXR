namespace RRX.Core
{
    /// <summary>Discrete learner actions tracked by the 7-step BLS-adjacent flow.</summary>
    public enum ScenarioAction
    {
        None = 0,
        CheckResponsiveness = 1,
        Call911 = 2,
        AdministerNarcan = 3,
        Rewind = 4,
        ScanScene = 5,
        OpenAirway = 6,
        CheckBreathing = 7,
        RecoveryPosition = 8
    }
}
