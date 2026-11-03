using System.Collections.Generic;
using UnityEngine;

namespace PipeWorld.Landscape
{
    public sealed class Ring
    {
        public int index;
        public float radius;
        public float step;
        public float baseHeight;
        public float gapCenterDeg;
        public float gapHalfWidthDeg;
        public float wobbleSeed;
        public bool hasGap;
    }

    public readonly struct Pad
    {
        public readonly Vector2 center;
        public readonly float radius;
        public readonly float blend;

        public Pad(Vector2 center, float radius, float blend)
        {
            this.center = center;
            this.radius = radius;
            this.blend = blend;
        }
    }

    public readonly struct Mesa
    {
        public readonly Vector2 center;
        public readonly float radius;
        public readonly float yawDeg;

        public Mesa(Vector2 center, float radius, float yawDeg)
        {
            this.center = center;
            this.radius = radius;
            this.yawDeg = yawDeg;
        }
    }

    public sealed class WorldShape
    {
        public const float HalfSize = 430f;
        public const float WallTransition = 2.5f;
        public const float RampTransition = 24f;

        static readonly float[] RingRadii = { 50f, 110f, 175f, 245f, 320f, 400f };
        static readonly float[] RingSteps = { 5f, 6f, 7f, 8f, 9f, 45f };
        static readonly float[] GapCenters = { 20f, 150f, 280f, 40f, 170f, 0f };
        static readonly float[] GapHalfWidths = { 17f, 15f, 14f, 13f, 12f, 0f };

        public readonly Ring[] Rings;
        public readonly List<Pad> Pads = new List<Pad>();
        public readonly List<Mesa> Mesas = new List<Mesa>();

        readonly float noiseX;
        readonly float noiseZ;

        public WorldShape(int seed)
        {
            var rng = new System.Random(seed);
            noiseX = 100f + (float)rng.NextDouble() * 500f;
            noiseZ = 100f + (float)rng.NextDouble() * 500f;

            Rings = new Ring[RingRadii.Length];
            var height = 0f;
            for (var i = 0; i < Rings.Length; i++)
            {
                Rings[i] = new Ring
                {
                    index = i,
                    radius = RingRadii[i],
                    step = RingSteps[i],
                    baseHeight = height,
                    gapCenterDeg = GapCenters[i],
                    gapHalfWidthDeg = GapHalfWidths[i],
                    hasGap = GapHalfWidths[i] > 0f,
                    wobbleSeed = 37f * i + (float)rng.NextDouble() * 50f,
                };
                height += RingSteps[i];
            }

            Pads.Add(new Pad(Vector2.zero, 28f, 22f));
            for (var k = 1; k < Rings.Length; k++)
                PlaceTerraceFeatures(rng, k);
        }

        public float OuterRadius => Rings[Rings.Length - 1].radius;

        public static float Smooth(float edge0, float edge1, float x)
        {
            var t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        public float RingRadius(Ring ring, float theta)
        {
            var wobble = Mathf.PerlinNoise(ring.wobbleSeed + Mathf.Cos(theta) * 1.5f + 10f, Mathf.Sin(theta) * 1.5f + 10f);
            return ring.radius + (wobble - 0.5f) * 10f;
        }

        public float GapMask(Ring ring, float theta)
        {
            if (!ring.hasGap)
                return 0f;
            var d = Mathf.Abs(Mathf.DeltaAngle(theta * Mathf.Rad2Deg, ring.gapCenterDeg));
            return 1f - Smooth(ring.gapHalfWidthDeg - 2.5f, ring.gapHalfWidthDeg + 2.5f, d);
        }

        public float TerraceHeight(float x, float z)
        {
            var r = Mathf.Sqrt(x * x + z * z);
            var theta = Mathf.Atan2(z, x);
            var h = 0f;
            foreach (var ring in Rings)
            {
                var t = Mathf.Lerp(WallTransition, RampTransition, GapMask(ring, theta));
                var radius = RingRadius(ring, theta);
                h += ring.step * Smooth(radius - t * 0.5f, radius + t * 0.5f, r);
            }
            return h;
        }

        // Distance to the nearest solid wall; ramp gaps count as far away.
        public float WallDistance(float x, float z)
        {
            var r = Mathf.Sqrt(x * x + z * z);
            var theta = Mathf.Atan2(z, x);
            var best = float.MaxValue;
            foreach (var ring in Rings)
            {
                var d = Mathf.Abs(r - RingRadius(ring, theta)) + GapMask(ring, theta) * 500f;
                if (d < best)
                    best = d;
            }
            return best;
        }

        public float FlatMask(float x, float z)
        {
            var mask = 0f;
            var p = new Vector2(x, z);
            foreach (var pad in Pads)
            {
                var m = 1f - Smooth(pad.radius, pad.radius + pad.blend, Vector2.Distance(p, pad.center));
                if (m > mask)
                    mask = m;
            }
            return mask;
        }

        public float Height(float x, float z)
        {
            var terrace = TerraceHeight(x, z);
            var flat = FlatMask(x, z);
            if (flat >= 1f)
                return terrace;

            var slow = (Mathf.PerlinNoise(x / 70f + noiseX, z / 70f + noiseZ) - 0.5f) * 3.2f;
            var fine = (Mathf.PerlinNoise(x / 26f + noiseZ, z / 26f + noiseX) - 0.5f) * 1f;
            var nearWall = Smooth(5f, 16f, WallDistance(x, z));
            return terrace + (slow + fine) * (1f - flat) * nearWall;
        }

        public Vector3 Normal(float x, float z)
        {
            const float e = 1f;
            var dx = Height(x + e, z) - Height(x - e, z);
            var dz = Height(x, z + e) - Height(x, z - e);
            return new Vector3(-dx, 2f * e, -dz).normalized;
        }

        public Biome BiomeAt(float x, float z)
        {
            var warp = (Mathf.PerlinNoise(x / 90f + 7f, z / 90f + 3f) - 0.5f) * 1.4f;
            var r = Mathf.Sqrt(x * x + z * z) + warp * 14f;
            if (r < 60f)
                return Biome.Tundra;
            var sector = Mathf.Repeat((Mathf.Atan2(z, x) + warp + Mathf.PI) / (Mathf.PI * 2f) * 3f, 3f);
            switch ((int)sector)
            {
                case 0: return Biome.Rock;
                case 1: return Biome.Ice;
                default: return Biome.Tundra;
            }
        }

        public bool IsClear(float x, float z, float margin)
        {
            var p = new Vector2(x, z);
            foreach (var pad in Pads)
                if (Vector2.Distance(p, pad.center) < pad.radius + margin)
                    return false;
            return true;
        }

        void PlaceTerraceFeatures(System.Random rng, int k)
        {
            var inner = Rings[k - 1].radius;
            var outer = Rings[k].radius;
            var maxRadius = Mathf.Min(22f, (outer - inner - 34f) * 0.5f);

            var padCount = 3 + (k == 3 ? 1 : 0);
            for (var i = 0; i < padCount; i++)
            {
                var radius = Mathf.Lerp(6f, maxRadius, (float)rng.NextDouble());
                if (TryPlace(rng, inner, outer, radius, out var center))
                    Pads.Add(new Pad(center, radius, 6f));
            }

            var mesaCount = k % 2 == 0 ? 2 : 1;
            for (var i = 0; i < mesaCount; i++)
            {
                var radius = Mathf.Lerp(8f, Mathf.Min(11f, maxRadius - 3f), (float)rng.NextDouble());
                if (TryPlace(rng, inner, outer, radius + 3f, out var center))
                {
                    Mesas.Add(new Mesa(center, radius, (float)rng.NextDouble() * 360f));
                    Pads.Add(new Pad(center, radius + 3f, 6f));
                }
            }
        }

        bool TryPlace(System.Random rng, float inner, float outer, float radius, out Vector2 center)
        {
            var minR = inner + radius + 17f;
            var maxR = outer - radius - 17f;
            center = Vector2.zero;
            if (maxR <= minR)
                return false;

            for (var attempt = 0; attempt < 40; attempt++)
            {
                var r = Mathf.Lerp(minR, maxR, (float)rng.NextDouble());
                var a = (float)rng.NextDouble() * Mathf.PI * 2f;
                var candidate = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                var overlaps = false;
                foreach (var pad in Pads)
                    if (Vector2.Distance(candidate, pad.center) < radius + pad.radius + 10f)
                    {
                        overlaps = true;
                        break;
                    }
                if (!overlaps)
                {
                    center = candidate;
                    return true;
                }
            }
            return false;
        }
    }
}
