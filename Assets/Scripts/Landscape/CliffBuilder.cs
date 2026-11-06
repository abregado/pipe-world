using UnityEngine;

namespace PipeWorld.Landscape
{
    public static class CliffBuilder
    {
        const float Embed = 3f;
        const float TopLift = 0.03f;
        const float MesaTierHeight = 1f;

        static readonly Color32 White = new Color32(255, 255, 255, 0);

        // One builder per Biome, in enum order.
        public static MeshBuilder[] Build(WorldShape shape, ShapeLibrary library, int seed)
        {
            var builders = new MeshBuilder[System.Enum.GetValues(typeof(Biome)).Length];
            for (var i = 0; i < builders.Length; i++)
                builders[i] = new MeshBuilder();
            var rng = new System.Random(seed);

            foreach (var ring in shape.Rings)
                PlaceWall(shape, library, builders, rng, ring);
            foreach (var plateau in shape.Plateaus)
                PlacePlateau(shape, library, builders, rng, plateau);
            foreach (var mesa in shape.Mesas)
                PlaceMesa(shape, library, builders, rng, mesa);
            for (var k = 1; k < shape.Rings.Length; k++)
                PlaceOutcrops(shape, library, builders, rng, k);
            PlaceDropRocks(shape, library, builders, rng);

            return builders;
        }

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        // Neighbouring blocks overlap, and their flat tops would be coplanar. Where two biomes meet, each block's top is
        // raised by its biome's priority, so the same biome always shows instead of the two z-fighting.
        const float PriorityLift = 0.02f;

        // Indexed by Biome: Tundra, Rock, Ice, Grass. Higher wins.
        static readonly int[] TopPriority = { 0, 3, 2, 1 };

        static void AddBlock(WorldShape shape, MeshBuilder[] builders, Tri[] template, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var biome = shape.BiomeAt(position.x, position.z);
            scale.y += TopPriority[(int)biome] * PriorityLift;
            builders[(int)biome].Append(template, Matrix4x4.TRS(position, rotation, scale), White);
        }

        static void Add(WorldShape shape, MeshBuilder[] builders, Tri[] template, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var biome = shape.BiomeAt(position.x, position.z);
            builders[(int)biome].Append(template, Matrix4x4.TRS(position, rotation, scale), White);
        }

        static void PlaceWall(WorldShape shape, ShapeLibrary library, MeshBuilder[] builders, System.Random rng, Ring ring)
        {
            var lowBase = ring.baseHeight;
            var top = lowBase + ring.step + TopLift;
            var bottom = lowBase - Embed;

            var theta = 0f;
            while (theta < Mathf.PI * 2f)
            {
                var radius = shape.RingRadius(ring, theta);
                var gap = shape.GapMask(ring, theta);
                // The outer wall stops across the open arc; the drop beyond it is terrain.
                if (ring.index == shape.Rings.Length - 1)
                    gap = Mathf.Max(gap, WorldShape.OpenMask(theta));
                var cos = Mathf.Cos(theta);
                var sin = Mathf.Sin(theta);
                var yaw = 90f - theta * Mathf.Rad2Deg;
                var step = Range(rng, 5f, 7f) / radius;

                // Beside a cave the wall is built by PlaceCave; only the back wall, which sits inside the recess, comes from here.
                var inCaveZone = InCaveZone(shape, ring, theta, out var insideRecess);
                if (inCaveZone && !insideRecess)
                {
                    theta += step;
                    continue;
                }

                if (gap < 0.5f)
                {
                    var rc = radius - 1f;
                    // Where the ground beside the wall falls away (the ends of the open arc), the block reaches down to it.
                    var floor = Mathf.Min(bottom, LowestGround(shape, cos, sin, rc) - Embed);
                    var scale = new Vector3(Range(rng, 8f, 14f), top - floor, Range(rng, 8f, 12f));
                    var position = new Vector3(cos * rc, floor, sin * rc);
                    AddBlock(shape, builders, library.FlatBlock(rng), position, Quaternion.Euler(0f, yaw + Range(rng, -22f, 22f), 0f), scale);

                    if (gap < 0.05f && !inCaveZone)
                    {
                        if (rng.NextDouble() < 0.55)
                            PlaceFootBoulder(shape, library, builders, rng, ring, theta, radius, yaw);
                        PlaceLedges(shape, library, builders, rng, ring, theta, radius, yaw);
                    }
                }

                theta += step;
            }

            foreach (var cave in shape.Caves)
                if (cave.ring == ring.index)
                    PlaceCave(shape, library, builders, rng, ring, cave);
        }

        static float LowestGround(WorldShape shape, float cos, float sin, float radius)
        {
            var lowest = float.MaxValue;
            for (var d = -6f; d <= 6f; d += 3f)
                lowest = Mathf.Min(lowest, shape.Height(cos * (radius + d), sin * (radius + d)));
            return lowest;
        }

        // A ring of wall blocks around the plateau rim, left open where its ramp is.
        static void PlacePlateau(WorldShape shape, ShapeLibrary library, MeshBuilder[] builders, System.Random rng, Plateau plateau)
        {
            var top = plateau.baseHeight + plateau.height + TopLift;
            var bottom = plateau.baseHeight - Embed;
            var angle = 0f;
            while (angle < Mathf.PI * 2f)
            {
                var radius = shape.PlateauRadius(plateau, angle);
                var step = Range(rng, 4.5f, 6.5f) / radius;
                if (shape.PlateauGap(plateau, angle) < 0.5f)
                {
                    var cos = Mathf.Cos(angle);
                    var sin = Mathf.Sin(angle);
                    var yaw = 90f - angle * Mathf.Rad2Deg;
                    var rc = radius - 1f;
                    var position = new Vector3(plateau.center.x + cos * rc, bottom, plateau.center.y + sin * rc);
                    var scale = new Vector3(Range(rng, 7f, 11f), top - bottom, Range(rng, 7f, 10f));
                    AddBlock(shape, builders, library.FlatBlock(rng), position, Quaternion.Euler(0f, yaw + Range(rng, -18f, 18f), 0f), scale);

                    if (shape.PlateauGap(plateau, angle) < 0.05f && rng.NextDouble() < 0.35)
                    {
                        var r = radius + Range(rng, 3.5f, 5.5f);
                        var boulder = new Vector3(Range(rng, 3f, 6f), Range(rng, 2.5f, Mathf.Min(6f, plateau.height * 0.7f)), Range(rng, 3f, 6f));
                        Add(shape, builders, library.Rock(rng), new Vector3(plateau.center.x + cos * r, plateau.baseHeight - 0.8f, plateau.center.y + sin * r),
                            Quaternion.Euler(Range(rng, -20f, 20f), yaw + Range(rng, -50f, 50f), Range(rng, -20f, 20f)), boulder);
                    }
                }
                angle += step;
            }
        }

        // The zone is the recess plus the 9 m either side where PlaceCave puts its pillars.
        static bool InCaveZone(WorldShape shape, Ring ring, float theta, out bool insideRecess)
        {
            insideRecess = false;
            foreach (var cave in shape.Caves)
            {
                if (cave.ring != ring.index)
                    continue;
                var offset = Mathf.Abs(shape.CaveOffset(cave, ring, theta));
                if (offset < cave.halfWidth + 9f)
                {
                    insideRecess = offset <= cave.halfWidth;
                    return true;
                }
            }
            return false;
        }

        // Two side pillars, a back wall and a roof block over the recess. The terrain floor already runs through it,
        // because the heightfield's wall was pushed outward by the same radius function.
        static void PlaceCave(WorldShape shape, ShapeLibrary library, MeshBuilder[] builders, System.Random rng, Ring ring, Cave cave)
        {
            var lowBase = ring.baseHeight;
            var top = lowBase + ring.step + TopLift;
            var bottom = lowBase - Embed;
            var ceiling = lowBase + cave.height;

            var theta = cave.thetaDeg * Mathf.Deg2Rad;
            var radial = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
            var tangent = new Vector2(-radial.y, radial.x);
            var yaw = 90f - cave.thetaDeg;

            var wallRadius = shape.RingRadiusBase(ring, theta);
            var faceRadius = wallRadius - 5.5f;
            var backRadius = wallRadius + cave.depth + 3.5f;
            var middle = (faceRadius + backRadius) * 0.5f;
            var span = backRadius - faceRadius;

            const float pillarWidth = 9f;
            for (var side = -1; side <= 1; side += 2)
            {
                var offset = side * (cave.halfWidth + 0.5f + pillarWidth * 0.5f);
                var center = radial * middle + tangent * offset;
                AddBlock(shape, builders, library.FlatBlock(rng), new Vector3(center.x, bottom, center.y),
                    Quaternion.Euler(0f, yaw + Range(rng, -3f, 3f), 0f), new Vector3(pillarWidth, top - bottom, span));
            }

            var roofCenter = radial * middle;
            AddBlock(shape, builders, library.FlatBlock(rng), new Vector3(roofCenter.x, ceiling, roofCenter.y),
                Quaternion.Euler(0f, yaw, 0f), new Vector3(2f * (cave.halfWidth + 1.5f), top - ceiling, span));

            var backCenter = radial * (wallRadius + cave.depth - 1f);
            AddBlock(shape, builders, library.FlatBlock(rng), new Vector3(backCenter.x, bottom, backCenter.y),
                Quaternion.Euler(0f, yaw, 0f), new Vector3(2f * cave.halfWidth + 8f, top - bottom, 10f));
        }

        static void PlaceLedges(WorldShape shape, ShapeLibrary library, MeshBuilder[] builders, System.Random rng, Ring ring, float theta, float wallRadius, float yaw)
        {
            var count = rng.Next(0, 2 + (int)(ring.step / 12f));
            for (var i = 0; i < count; i++)
            {
                var scale = new Vector3(Range(rng, 3.5f, 7f), Range(rng, 2.5f, 5f), Range(rng, 4f, 7f));
                var height = Range(rng, ring.baseHeight + 1.5f, ring.baseHeight + ring.step - 2.5f);
                var r = wallRadius - 5.2f - Range(rng, 0f, 1.5f);
                var position = new Vector3(Mathf.Cos(theta) * r, height - scale.y * 0.3f, Mathf.Sin(theta) * r);
                var rotation = Quaternion.Euler(Range(rng, -25f, 25f), yaw + Range(rng, -40f, 40f), Range(rng, -25f, 25f));
                Add(shape, builders, library.Rock(rng), position, rotation, scale);
            }
        }

        static void PlaceFootBoulder(WorldShape shape, ShapeLibrary library, MeshBuilder[] builders, System.Random rng, Ring ring, float theta, float wallRadius, float yaw)
        {
            var r = wallRadius - 5.5f - Range(rng, 1f, 3f);
            var tiltedYaw = yaw + Range(rng, -50f, 50f);
            var scale = new Vector3(Range(rng, 5f, 9f), Range(rng, 3.5f, Mathf.Min(9f, ring.step * 0.7f)), Range(rng, 5f, 8f));
            var position = new Vector3(Mathf.Cos(theta) * r, ring.baseHeight - 1f, Mathf.Sin(theta) * r);
            var rotation = Quaternion.Euler(Range(rng, -20f, 20f), tiltedYaw, Range(rng, -20f, 20f));
            Add(shape, builders, library.Rock(rng), position, rotation, scale);
        }

        static void PlaceMesa(WorldShape shape, ShapeLibrary library, MeshBuilder[] builders, System.Random rng, Mesa mesa)
        {
            var ground = shape.Height(mesa.center.x, mesa.center.y);
            var width = mesa.radius * 2f;

            var tier1Top = ground + MesaTierHeight + TopLift;
            var tier1Bottom = ground - 1.5f;
            AddBlock(shape, builders, library.FlatBlock(rng),
                new Vector3(mesa.center.x, tier1Bottom, mesa.center.y),
                Quaternion.Euler(0f, mesa.yawDeg, 0f),
                new Vector3(width, tier1Top - tier1Bottom, width));

            var offset = new Vector2(Range(rng, -1f, 1f), Range(rng, -1f, 1f)) * mesa.radius * 0.15f;
            var tier2Bottom = tier1Top - 0.5f;
            var tier2Top = tier1Top + MesaTierHeight;
            AddBlock(shape, builders, library.FlatBlock(rng),
                new Vector3(mesa.center.x + offset.x, tier2Bottom, mesa.center.y + offset.y),
                Quaternion.Euler(0f, mesa.yawDeg + Range(rng, 15f, 40f), 0f),
                new Vector3(width * 0.55f, tier2Top - tier2Bottom, width * 0.55f));
        }

        static void PlaceOutcrops(WorldShape shape, ShapeLibrary library, MeshBuilder[] builders, System.Random rng, int k)
        {
            var inner = shape.Rings[k - 1].radius + 20f;
            var outer = shape.Rings[k].radius - 20f;

            for (var placed = 0; placed < 5; placed++)
            {
                for (var attempt = 0; attempt < 30; attempt++)
                {
                    var r = Range(rng, inner, outer);
                    var a = Range(rng, 0f, Mathf.PI * 2f);
                    var x = Mathf.Cos(a) * r;
                    var z = Mathf.Sin(a) * r;
                    if (!shape.IsClear(x, z, 8f) || shape.WallDistance(x, z) < 20f)
                        continue;

                    BuildOutcrop(shape, library, builders, rng, x, z);
                    break;
                }
            }
        }

        // Outcrops down the drop and across the near lowland, to give the view from the rim something to judge distance by.
        static void PlaceDropRocks(WorldShape shape, ShapeLibrary library, MeshBuilder[] builders, System.Random rng)
        {
            for (var i = 0; i < 90; i++)
            {
                var angle = (WorldShape.OpenCenterDeg + Range(rng, -WorldShape.OpenHalfDeg + 6f, WorldShape.OpenHalfDeg - 6f)) * Mathf.Deg2Rad;
                var start = shape.DropStart(angle);
                var r = start + 25f + Mathf.Pow((float)rng.NextDouble(), 1.5f) * 650f;
                var x = Mathf.Cos(angle) * r;
                var z = Mathf.Sin(angle) * r;
                BuildOutcrop(shape, library, builders, rng, x, z, r < start + WorldShape.DropLength ? 1f : Range(rng, 1.5f, 3f));
            }
        }

        static void BuildOutcrop(WorldShape shape, ShapeLibrary library, MeshBuilder[] builders, System.Random rng, float x, float z, float size = 1f)
        {
            var ground = shape.Height(x, z);
            var bigHeight = Range(rng, 3f, 7f) * size;
            var bigWidth = Range(rng, 4f, 8f) * size;
            var bigRotation = Quaternion.Euler(Range(rng, -15f, 15f), Range(rng, 0f, 360f), Range(rng, -15f, 15f));
            Add(shape, builders, library.Rock(rng), new Vector3(x, ground - 1.2f * size, z), bigRotation,
                new Vector3(bigWidth, bigHeight, bigWidth * Range(rng, 0.7f, 1.2f)));

            var companions = rng.Next(2, 4);
            for (var i = 0; i < companions; i++)
            {
                var angle = Range(rng, 0f, Mathf.PI * 2f);
                var distance = bigWidth * Range(rng, 0.45f, 0.8f);
                var cx = x + Mathf.Cos(angle) * distance;
                var cz = z + Mathf.Sin(angle) * distance;
                var companion = Range(rng, 2f, 4f) * size;
                var rotation = Quaternion.Euler(Range(rng, -30f, 30f), Range(rng, 0f, 360f), Range(rng, -30f, 30f));
                Add(shape, builders, library.Rock(rng), new Vector3(cx, shape.Height(cx, cz) - 0.8f * size, cz), rotation,
                    new Vector3(companion, companion * Range(rng, 0.6f, 1.2f), companion));
            }

            var stackSize = Range(rng, 2f, 3.2f) * size;
            Add(shape, builders, library.Rock(rng),
                new Vector3(x + Range(rng, -1f, 1f), ground + bigHeight * 0.55f, z + Range(rng, -1f, 1f)),
                Quaternion.Euler(Range(rng, -25f, 25f), Range(rng, 0f, 360f), Range(rng, -25f, 25f)),
                new Vector3(stackSize, stackSize * 0.8f, stackSize));
        }
    }
}
