using UnityEngine;

namespace PipeWorld.Landscape
{
    public enum Biome
    {
        Tundra,
        Rock,
        Ice,
        Grass,
    }

    public static class Walkable
    {
        public const float SlopeLimitDegrees = 50f;

        public static float MinNormalY => Mathf.Cos(SlopeLimitDegrees * Mathf.Deg2Rad);
    }
}
