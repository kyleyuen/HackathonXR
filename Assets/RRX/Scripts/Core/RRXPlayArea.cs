namespace RRX.Core
{
    /// <summary>
    /// Square play volume on XZ: coordinates stay within [-RadiusMeters, RadiusMeters] from the scene origin.
    /// Passthrough shows your real room inside; virtual boundary walls sit at the edges.
    /// Change <see cref="RadiusMeters"/> to <c>3f</c> for a smaller room.
    /// </summary>
    public static class RRXPlayArea
    {
        /// <summary>Half-extent along X and Z from center (meters). Total width/depth = 2 × this value.</summary>
        public const float RadiusMeters = 5f;
    }
}
