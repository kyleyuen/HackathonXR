namespace RRX.Core
{
    /// <summary>Single source of truth for state, action, and hotspot mappings across all 7 BLS steps.</summary>
    public static class ScenarioHotspotRegistry
    {
        public static ScenarioAction ActionFor(ScenarioHotspotId id)
        {
            switch (id)
            {
                case ScenarioHotspotId.SceneScan:   return ScenarioAction.ScanScene;
                case ScenarioHotspotId.Shoulder:    return ScenarioAction.CheckResponsiveness;
                case ScenarioHotspotId.Chin:        return ScenarioAction.OpenAirway;
                case ScenarioHotspotId.Mouth:       return ScenarioAction.CheckBreathing;
                case ScenarioHotspotId.Phone:       return ScenarioAction.Call911;
                case ScenarioHotspotId.Nose:        return ScenarioAction.AdministerNarcan;
                case ScenarioHotspotId.Hip:         return ScenarioAction.RecoveryPosition;
                default:                            return ScenarioAction.None;
            }
        }

        public static ScenarioHotspotId ExpectedHotspot(ScenarioState state)
        {
            switch (state)
            {
                case ScenarioState.SceneSafety:         return ScenarioHotspotId.SceneScan;
                case ScenarioState.Arrival:             return ScenarioHotspotId.Shoulder;
                case ScenarioState.OpenAirway:          return ScenarioHotspotId.Chin;
                case ScenarioState.CheckBreathing:      return ScenarioHotspotId.Mouth;
                case ScenarioState.CallForHelp:         return ScenarioHotspotId.Phone;
                case ScenarioState.AdministerNarcan:    return ScenarioHotspotId.Nose;
                case ScenarioState.RecoveryPosition:    return ScenarioHotspotId.Hip;
                default:                               return ScenarioHotspotId.None;
            }
        }

        public static ScenarioState StateFromAcceptedHotspot(ScenarioHotspotId id)
        {
            switch (id)
            {
                case ScenarioHotspotId.SceneScan:   return ScenarioState.Arrival;
                case ScenarioHotspotId.Shoulder:    return ScenarioState.OpenAirway;
                case ScenarioHotspotId.Chin:        return ScenarioState.CheckBreathing;
                case ScenarioHotspotId.Mouth:       return ScenarioState.CallForHelp;
                case ScenarioHotspotId.Phone:       return ScenarioState.AdministerNarcan;
                case ScenarioHotspotId.Nose:        return ScenarioState.RecoveryPosition;
                case ScenarioHotspotId.Hip:         return ScenarioState.Recovery;
                default:                            return ScenarioState.SceneSafety;
            }
        }
    }
}
