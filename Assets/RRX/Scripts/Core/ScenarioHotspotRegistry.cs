namespace RRX.Core
{
    /// <summary>Single source of truth for state, action, and hotspot mappings.</summary>
    public static class ScenarioHotspotRegistry
    {
        public static ScenarioAction ActionFor(ScenarioHotspotId id)
        {
            switch (id)
            {
                case ScenarioHotspotId.Shoulder:
                    return ScenarioAction.CheckResponsiveness;
                case ScenarioHotspotId.Phone:
                    return ScenarioAction.Call911;
                case ScenarioHotspotId.Nose:
                    return ScenarioAction.AdministerNarcan;
                default:
                    return ScenarioAction.None;
            }
        }

        public static ScenarioHotspotId ExpectedHotspot(ScenarioState state)
        {
            switch (state)
            {
                case ScenarioState.Arrival:
                    return ScenarioHotspotId.Shoulder;
                case ScenarioState.CallForHelp:
                    return ScenarioHotspotId.Phone;
                case ScenarioState.AdministerNarcan:
                    return ScenarioHotspotId.Nose;
                default:
                    return ScenarioHotspotId.None;
            }
        }

        public static ScenarioState StateFromAcceptedHotspot(ScenarioHotspotId id)
        {
            switch (id)
            {
                case ScenarioHotspotId.Shoulder:
                    return ScenarioState.CallForHelp;
                case ScenarioHotspotId.Phone:
                    return ScenarioState.AdministerNarcan;
                case ScenarioHotspotId.Nose:
                    return ScenarioState.Recovery;
                default:
                    return ScenarioState.Arrival;
            }
        }
    }
}
