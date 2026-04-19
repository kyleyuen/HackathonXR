namespace RRX.Core
{
    /// <summary>Finite states for the 7-step overdose scenario graph.</summary>
    public enum ScenarioState
    {
        Arrival = 0,
        AssessResponsiveness = 1,
        CallForHelp = 2,
        AdministerNarcan = 3,
        Recovery = 4,
        CriticalFailure = 5,
        SceneSafety = 6,
        OpenAirway = 7,
        CheckBreathing = 8,
        RecoveryPosition = 9
    }
}
