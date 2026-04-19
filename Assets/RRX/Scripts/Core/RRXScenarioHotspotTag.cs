using System.Collections.Generic;
using UnityEngine;

namespace RRX.Core
{
    /// <summary>Tags hotspot objects with a stable ID and registers scene lookups.</summary>
    public sealed class RRXScenarioHotspotTag : MonoBehaviour
    {
        static readonly Dictionary<ScenarioHotspotId, RRXScenarioHotspotTag> Registry =
            new Dictionary<ScenarioHotspotId, RRXScenarioHotspotTag>();

        [SerializeField] ScenarioHotspotId _hotspotId = ScenarioHotspotId.None;

        public ScenarioHotspotId HotspotId => _hotspotId;

        void OnEnable()
        {
            Registry[_hotspotId] = this;
        }

        void OnDisable()
        {
            if (Registry.TryGetValue(_hotspotId, out var current) && current == this)
                Registry.Remove(_hotspotId);
        }

        public static RRXScenarioHotspotTag Find(ScenarioHotspotId id)
        {
            Registry.TryGetValue(id, out var tag);
            return tag;
        }
    }
}
