using UnityEngine;

namespace RRX.Core
{
    /// <summary>
    /// Public plaza walkable disc on XZ: distance from origin in XZ should stay within <see cref="RadiusMeters"/>.
    /// The blockout mall / galleries use a separate layout radius in the Editor so shrinking the central disc
    /// does not shrink storefront geometry — see plaza generation in <c>RRXCubeBlockoutMenu</c>.
    /// </summary>
    public static class RRXPlayArea
    {
        /// <summary>Walkable plaza radius in meters (circular footprint on XZ). Mall scale is independent.</summary>
        public const float RadiusMeters = 4f;

        /// <summary>
        /// Extra meters beyond <see cref="RadiusMeters"/> used when carving virtual floor tiles and aligning the
        /// passthrough/occlusion tube so the MR domain matches visually.
        /// </summary>
        public const float VirtualFloorHoleClearanceMeters = 0.25f;

        /// <summary>Outer radius of the carved floor hole (matches blockout + passthrough tube).</summary>
        public static float VirtualFloorHoleRadiusMeters => RadiusMeters + VirtualFloorHoleClearanceMeters;

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
