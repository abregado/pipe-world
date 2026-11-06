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

    // A raised disc with cliff sides and one ramp. Its height is added on top of the terrace it stands on.
    public readonly struct Plateau
    {
        public readonly Vector2 center;
        public readonly float radius;
        public readonly float height;
        public readonly float rampDeg;
        public readonly float rampHalfDeg;
        public readonly float wobbleSeed;
        public readonly float baseHeight;

        public Plateau(Vector2 center, float radius, float height, float rampDeg, float rampHalfDeg, float wobbleSeed, float baseHeight)
        {
            this.center = center;
            this.radius = radius;
            this.height = height;
            this.rampDeg = rampDeg;
            this.rampHalfDeg = rampHalfDeg;
            this.wobbleSeed = wobbleSeed;
            this.baseHeight = baseHeight;
        }

        // About 22 degrees at its steepest, the same as the ring ramps.
        public float RampLength => height * 3.6f;
        public float Footprint => radius + RampLength * 0.5f;
    }

    // A recess in a ring wall: the wall radius is pushed outward by depth across 2 * halfWidth metres of arc.
    public readonly struct Cave
    {
        public readonly int ring;
        public readonly float thetaDeg;
        public readonly float halfWidth;
        public readonly float depth;
        public readonly float height;

        public Cave(int ring, float thetaDeg, float halfWidth, float depth, float height)
        {
            this.ring = ring;
            this.thetaDeg = thetaDeg;
            this.halfWidth = halfWidth;
            this.depth = depth;
            this.height = height;
        }
    }

    public sealed class WorldShape
    {
        public const float HalfSize = 430f;
        public const float WallTransition = 2.5f;
        public const float RampTransition = 24f;

        // The outer wall is left out across this arc, and the ground beyond falls away to a lowland.
        public const float OpenCenterDeg = 45f;
        public const float OpenHalfDeg = 90f;
        const float OpenEdgeDeg = 4f;
        public const float DropLength = 150f;
        public const float LowlandHeight = -100f;
        // How far the terrain mesh reaches on the open side and along both edges of it.
        public const float FarExtent = 2600f;

        static readonly float[] RingRadii = { 50f, 110f, 175f, 245f, 320f, 400f };
        static readonly float[] RingSteps = { 5f, 6f, 7f, 8f, 9f, 45f };
        static readonly float[] GapCenters = { 20f, 150f, 280f, 40f, 170f, 0f };
        static readonly float[] GapHalfWidths = { 17f, 15f, 14f, 13f, 12f, 0f };

        public readonly Ring[] Rings;
        public readonly List<Pad> Pads = new List<Pad>();
        public readonly List<Mesa> Mesas = new List<Mesa>();
        public readonly List<Cave> Caves = new List<Cave>();
        public readonly List<Plateau> Plateaus = new List<Plateau>();

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

            // On the outer wall, behind where the "inside outer wall" respawn stands.
            Caves.Add(new Cave(Rings.Length - 1, 225f, 5.5f, 8f, 4.2f));

            Pads.Add(new Pad(Vector2.zero, 28f, 22f));
            // Plateaus first: they need the most room.
            for (var k = 1; k < Rings.Length; k++)
                PlacePlateaus(rng, k);
            for (var k = 1; k < Rings.Length; k++)
                PlaceTerraceFeatures(rng, k);
        }

        public float OuterRadius => Rings[Rings.Length - 1].radius;

        public static float Smooth(float edge0, float edge1, float x)
        {
            var t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        // The wall radius without any cave recess.
        public float RingRadiusBase(Ring ring, float theta)
        {
            var wobble = Mathf.PerlinNoise(ring.wobbleSeed + Mathf.Cos(theta) * 1.5f + 10f, Mathf.Sin(theta) * 1.5f + 10f);
            return ring.radius + (wobble - 0.5f) * 10f;
        }

        // Terrain height, wall distance and block placement all read this radius, so a cave shifts them together.
        public float RingRadius(Ring ring, float theta)
        {
            var radius = RingRadiusBase(ring, theta);
            foreach (var cave in Caves)
                if (cave.ring == ring.index)
                    radius += cave.depth * (1f - Smooth(cave.halfWidth, cave.halfWidth + 1.5f, Mathf.Abs(CaveOffset(cave, ring, theta))));
            return radius;
        }

        // Signed distance in metres along the wall from the cave's axis.
        public float CaveOffset(Cave cave, Ring ring, float theta)
        {
            return Mathf.DeltaAngle(cave.thetaDeg, theta * Mathf.Rad2Deg) * Mathf.Deg2Rad * ring.radius;
        }

        // 1 across the open arc of the outer wall, 0 where the wall stands.
        public static float OpenMask(float theta)
        {
            var d = Mathf.Abs(Mathf.DeltaAngle(theta * Mathf.Rad2Deg, OpenCenterDeg));
            return 1f - Smooth(OpenHalfDeg - OpenEdgeDeg, OpenHalfDeg + OpenEdgeDeg, d);
        }

        // Where the ground starts to fall away: just past the last inner ring on the open side, outside the wall elsewhere.
        public float DropStart(float theta)
        {
            return Mathf.Lerp(OuterRadius + 35f, Rings[Rings.Length - 2].radius + 40f, OpenMask(theta));
        }

        // The edge of the walkable crater: the outer wall, or the top of the drop on the open side.
        public float PlayRadius(float theta)
        {
            return Mathf.Lerp(OuterRadius, DropStart(theta), OpenMask(theta));
        }

        public bool InPlayArea(float x, float z, float margin)
        {
            return Mathf.Sqrt(x * x + z * z) < PlayRadius(Mathf.Atan2(z, x)) - margin;
        }

        public float PlateauRadius(Plateau plateau, float angle)
        {
            var wobble = Mathf.PerlinNoise(plateau.wobbleSeed + Mathf.Cos(angle) * 1.2f, Mathf.Sin(angle) * 1.2f + 5f);
            return plateau.radius + (wobble - 0.5f) * 4f;
        }

        public float PlateauGap(Plateau plateau, float angle)
        {
            var d = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, plateau.rampDeg));
            return 1f - Smooth(plateau.rampHalfDeg - 3f, plateau.rampHalfDeg + 3f, d);
        }

        float PlateauLift(Plateau plateau, float x, float z, out float rimDistance)
        {
            var dx = x - plateau.center.x;
            var dz = z - plateau.center.y;
            var d = Mathf.Sqrt(dx * dx + dz * dz);
            var angle = Mathf.Atan2(dz, dx);
            var gap = PlateauGap(plateau, angle);
            var radius = PlateauRadius(plateau, angle);
            var t = Mathf.Lerp(WallTransition, plateau.RampLength, gap);
            rimDistance = Mathf.Abs(d - radius) + gap * 500f;
            return plateau.height * (1f - Smooth(radius - t * 0.5f, radius + t * 0.5f, d));
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
            var outer = Rings.Length - 1;
            foreach (var ring in Rings)
            {
                var t = Mathf.Lerp(WallTransition, RampTransition, GapMask(ring, theta));
                var radius = RingRadius(ring, theta);
                var step = ring.index == outer ? ring.step * (1f - OpenMask(theta)) : ring.step;
                h += step * Smooth(radius - t * 0.5f, radius + t * 0.5f, r);
            }
            foreach (var plateau in Plateaus)
                h += PlateauLift(plateau, x, z, out _);
            return h;
        }

        // Distance to the nearest solid wall; ramp gaps count as far away.
        public float WallDistance(float x, float z)
        {
            var r = Mathf.Sqrt(x * x + z * z);
            var theta = Mathf.Atan2(z, x);
            var best = float.MaxValue;
            var outer = Rings.Length - 1;
            foreach (var ring in Rings)
            {
                var d = Mathf.Abs(r - RingRadius(ring, theta)) + GapMask(ring, theta) * 500f;
                if (ring.index == outer)
                    d += OpenMask(theta) * 500f;
                if (d < best)
                    best = d;
            }
            foreach (var plateau in Plateaus)
            {
                PlateauLift(plateau, x, z, out var rim);
                if (rim < best)
                    best = rim;
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
            var crater = terrace + (slow + fine) * (1f - flat) * nearWall;

            var r = Mathf.Sqrt(x * x + z * z);
            var start = DropStart(Mathf.Atan2(z, x));
            if (r <= start)
                return crater;
            return Mathf.Lerp(crater, Lowland(x, z), Smooth(start, start + DropLength, r));
        }

        // Broad hills far below the crater rim, seen from the open side.
        public float Lowland(float x, float z)
        {
            var ridges = (Mathf.PerlinNoise(x / 700f + noiseX, z / 700f + noiseZ) - 0.5f) * 220f;
            var hills = (Mathf.PerlinNoise(x / 260f + noiseZ, z / 260f + noiseX) - 0.5f) * 70f;
            var rolls = (Mathf.PerlinNoise(x / 90f + noiseX, z / 90f + noiseZ) - 0.5f) * 12f;
            // Hills only rise well away from the crater, so the foot of the drop stays low.
            var rise = Smooth(OuterRadius + 150f, OuterRadius + 700f, Mathf.Sqrt(x * x + z * z));
            return LowlandHeight + rolls + (hills + Mathf.Max(0f, ridges + 40f)) * rise;
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
            // Everything below the drop is grassland.
            if (r > DropStart(Mathf.Atan2(z, x)) + 40f)
                return Biome.Grass;
            // Four sectors. Ice and grass share the open half, so the view over the lowland starts from grass.
            var sector = Mathf.Repeat((Mathf.Atan2(z, x) + warp + Mathf.PI) / (Mathf.PI * 2f) * 4f, 4f);
            switch ((int)sector)
            {
                case 0: return Biome.Rock;
                case 1: return Biome.Ice;
                case 2: return Biome.Grass;
                default: return Biome.Tundra;
            }
        }

        // Pads always block; plateaus block only when asked, so scatter can still grow on their tops.
        public bool IsClear(float x, float z, float margin, bool avoidPlateaus = true)
        {
            var p = new Vector2(x, z);
            foreach (var pad in Pads)
                if (Vector2.Distance(p, pad.center) < pad.radius + margin)
                    return false;
            if (avoidPlateaus)
                foreach (var plateau in Plateaus)
                    if (Vector2.Distance(p, plateau.center) < plateau.Footprint + margin)
                        return false;
            return true;
        }

        void PlacePlateaus(System.Random rng, int k)
        {
            var inner = Rings[k - 1].radius;
            var outer = Rings[k].radius;
            var maxRadius = (outer - inner - 24f) * 0.5f;
            var count = k >= 3 ? 2 : 1;
            for (var i = 0; i < count; i++)
            {
                var radius = Mathf.Lerp(0.7f, 1f, (float)rng.NextDouble()) * maxRadius;
                var height = Mathf.Lerp(6f, 9f + k, (float)rng.NextDouble());
                var rampLength = height * 3.6f;
                for (var attempt = 0; attempt < 40; attempt++)
                {
                    // A plateau may come within 12 m of a wall and block the terrace there. The terrace is a ring, so the
                    // other way round stays open, but the plateau must not stand in front of either wall's ramp gap.
                    if (!TryPlace(rng, inner - 5f, outer + 5f, radius, out var center))
                        break;
                    var theta = Mathf.Atan2(center.y, center.x);
                    var arc = (radius + 30f) / center.magnitude * Mathf.Rad2Deg;
                    if (NearGap(Rings[k - 1], theta, arc) || NearGap(Rings[k], theta, arc))
                        continue;
                    if (!InPlayArea(center.x, center.y, radius + rampLength * 0.5f + 10f))
                        continue;

                    // The ramp runs along the terrace, where there is the most room.
                    var rampDeg = theta * Mathf.Rad2Deg + (rng.NextDouble() < 0.5 ? 90f : -90f) + (float)(rng.NextDouble() - 0.5) * 40f;
                    var rampHalf = 7f / radius * Mathf.Rad2Deg;
                    var baseHeight = Rings[k - 1].baseHeight + Rings[k - 1].step;
                    Plateaus.Add(new Plateau(center, radius, height, rampDeg, rampHalf, 13f * Plateaus.Count + 3f, baseHeight));
                    break;
                }
            }
        }

        static bool NearGap(Ring ring, float theta, float arcDeg)
        {
            return ring.hasGap && Mathf.Abs(Mathf.DeltaAngle(theta * Mathf.Rad2Deg, ring.gapCenterDeg)) < ring.gapHalfWidthDeg + arcDeg;
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
                var overlaps = !InPlayArea(candidate.x, candidate.y, radius + 17f);
                foreach (var pad in Pads)
                    if (Vector2.Distance(candidate, pad.center) < radius + pad.radius + 10f)
                    {
                        overlaps = true;
                        break;
                    }
                foreach (var plateau in Plateaus)
                    if (Vector2.Distance(candidate, plateau.center) < radius + plateau.Footprint + 10f)
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
