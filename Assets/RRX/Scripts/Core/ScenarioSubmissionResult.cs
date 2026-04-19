namespace RRX.Core
{
    /// <summary>Typed outcome for a scenario action submission.</summary>
    public enum ScenarioSubmissionResult
    {
        Accepted = 0,
        RejectedWrongAction = 1,
        RejectedOutOfOrder = 2,
        RejectedDuplicate = 3,
        RejectedTerminalState = 4,
        RejectedThrottled = 5,
        RejectedInvalid = 6
    }
}
