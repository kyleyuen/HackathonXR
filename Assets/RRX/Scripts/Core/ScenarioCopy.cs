namespace RRX.Core
{
    /// <summary>Copy deck for in-headset guidance UI.</summary>
    public static class ScenarioCopy
    {
        public static string TitleFor(ScenarioState state)
        {
            switch (state)
            {
                case ScenarioState.Arrival:
                    return "Step 1/3 - Check responsiveness";
                case ScenarioState.CallForHelp:
                    return "Step 2/3 - Call 911";
                case ScenarioState.AdministerNarcan:
                    return "Step 3/3 - Administer Narcan";
                case ScenarioState.Recovery:
                    return "Recovered";
                case ScenarioState.CriticalFailure:
                    return "Critical failure";
                default:
                    return "Scenario";
            }
        }

        public static string HintFor(ScenarioState state, int failureCount)
        {
            var baseHint = state switch
            {
                ScenarioState.Arrival => "Point at the shoulder hotspot and press trigger.",
                ScenarioState.CallForHelp => "Point at the phone and press trigger to call 911.",
                ScenarioState.AdministerNarcan => "Point at the nose hotspot and press trigger.",
                ScenarioState.Recovery => "Patient recovered. Press Reset to practice again.",
                ScenarioState.CriticalFailure => "Patient lost. Press Reset to retry.",
                _ => "Follow the highlighted hotspot."
            };

            return failureCount <= 0
                ? baseHint
                : $"{baseHint} Mistakes: {failureCount}/3";
        }
    }
}
