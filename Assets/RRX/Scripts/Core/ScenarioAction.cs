namespace RRX.Core
{
    /// <summary>Discrete learner actions tracked by the overdose Phase 1 flow.</summary>
    public enum ScenarioAction
    {
        None = 0,
        CheckResponsiveness = 1,
        Call911 = 2,
        AdministerNarcan = 3,
        Rewind = 4
    }
}
