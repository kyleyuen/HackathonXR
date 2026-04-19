namespace RRX.Core
{
    /// <summary>Copy deck for in-headset guidance UI with two-tier hints (vague → specific after reveal delay).</summary>
    public static class ScenarioCopy
    {
        public static string TitleFor(ScenarioState state)
        {
            switch (state)
            {
                case ScenarioState.SceneSafety:         return "Step 1/6 – Scene Safety";
                case ScenarioState.Arrival:             return "Step 2/6 – Check Response";
                case ScenarioState.OpenAirway:          return "Step 3/6 – Open Airway";
                case ScenarioState.CheckBreathing:      return "Step 3/6 – Open Airway";
                case ScenarioState.CallForHelp:         return "Step 4/6 – Call for Help";
                case ScenarioState.AdministerNarcan:    return "Step 5/6 – Administer Narcan";
                case ScenarioState.RecoveryPosition:    return "Step 6/6 – Recovery Position";
                case ScenarioState.Recovery:            return "Patient Recovering";
                case ScenarioState.CriticalFailure:     return "Critical Failure";
                default:                               return "Scenario";
            }
        }

        /// <summary>Vague, atmospheric hint shown immediately when a step starts.</summary>
        public static string VagueHintFor(ScenarioState state)
        {
            switch (state)
            {
                case ScenarioState.SceneSafety:         return "Is the area safe to approach?";
                case ScenarioState.Arrival:             return "Are they conscious? Check for a response.";
                case ScenarioState.OpenAirway:          return "Tilt the head back to open their airway.";
                case ScenarioState.CheckBreathing:      return "Tilt the head back to open their airway.";
                case ScenarioState.CallForHelp:         return "This is an emergency. Get help now.";
                case ScenarioState.AdministerNarcan:    return "Administer the reversal agent.";
                case ScenarioState.RecoveryPosition:    return "Protect their airway. Position them safely.";
                case ScenarioState.Recovery:            return "Patient is responding. Monitor closely.";
                case ScenarioState.CriticalFailure:     return "Patient lost. Press Reset to retry.";
                default:                               return "Assess the situation.";
            }
        }

        /// <summary>Specific hint revealed after the reveal delay or on the second mistake.</summary>
        public static string SpecificHintFor(ScenarioState state, int failureCount)
        {
            string hint;
            switch (state)
            {
                case ScenarioState.SceneSafety:
                    hint = "Look around the area and press trigger to scan for hazards.";
                    break;
                case ScenarioState.Arrival:
                    hint = "Point at the shoulder and press trigger to check responsiveness.";
                    break;
                case ScenarioState.OpenAirway:
                    hint = "Point at the chin/throat and press trigger to tilt the head back.";
                    break;
                case ScenarioState.CheckBreathing:
                    hint = "Point at the chin/throat and press trigger to tilt the head back.";
                    break;
                case ScenarioState.CallForHelp:
                    hint = "Point at the phone and press trigger to call 911.";
                    break;
                case ScenarioState.AdministerNarcan:
                    hint = "Point at the nose and press trigger to administer Narcan.";
                    break;
                case ScenarioState.RecoveryPosition:
                    hint = "Point at the hip and press trigger to roll the patient.";
                    break;
                case ScenarioState.Recovery:
                    hint = "Patient recovered. Press Reset to practice again.";
                    break;
                case ScenarioState.CriticalFailure:
                    hint = "Patient lost. Press Reset to retry.";
                    break;
                default:
                    hint = "Follow the highlighted hotspot.";
                    break;
            }

            return hint;
        }

        /// <summary>Backwards-compatible hint (returns specific hint always).</summary>
        public static string HintFor(ScenarioState state, int failureCount)
            => SpecificHintFor(state, failureCount);
    }
}
