using UnityEngine;

namespace RRX.Core
{
    /// <summary>
    /// Public plaza walkable disc on XZ: distance from origin in XZ should stay within <see cref="RadiusMeters"/>.
    /// Surrounding storefront blockout extends beyond this radius; locomotion and sensing should use
    /// <see cref="ContainsWalkableDiscXZ"/> for the training floor.
    /// </summary>
    public static class RRXPlayArea
    {
        /// <summary>Walkable plaza radius in meters (circular footprint on XZ).</summary>
        public const float RadiusMeters = 10f;

        /// <summary>True if (x,z) lies inside the walkable disc (inclusive of boundary).</summary>
        public static bool ContainsWalkableDiscXZ(float x, float z)
        {
            return x * x + z * z <= RadiusMeters * RadiusMeters;
        }

        /// <summary>True if the point's XZ projection lies inside the walkable disc.</summary>
        public static bool ContainsWalkableDiscXZ(Vector3 worldPosition)
        {
            return ContainsWalkableDiscXZ(worldPosition.x, worldPosition.z);
        }
    }
}
