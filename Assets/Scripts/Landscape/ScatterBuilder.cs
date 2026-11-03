using System.Collections.Generic;
using UnityEngine;

namespace PipeWorld.Landscape
{
    public static class ScatterBuilder
    {
        public const float TileSize = 96f;
        const float CellSize = 5.5f;
        const float MinNormalY = 0.8f;

        static readonly Color[] TuftColors =
        {
            new Color(0.58f, 0.58f, 0.40f), new Color(0.46f, 0.56f, 0.40f), new Color(0.78f, 0.85f, 0.80f),
        };
        static readonly Color[] RockColors =
        {
            new Color(0.48f, 0.45f, 0.44f), new Color(0.40f, 0.38f, 0.40f), new Color(0.55f, 0.50f, 0.46f), new Color(0.36f, 0.34f, 0.36f),
        };
        static readonly Color[] CrystalColors =
        {
            new Color(0.62f, 0.88f, 0.96f), new Color(0.78f, 0.95f, 1.00f), new Color(0.50f, 0.80f, 0.92f),
        };
        static readonly Color MoundColor = new Color(0.92f, 0.95f, 0.97f);

        public static Dictionary<(Biome biome, int tx, int tz), MeshBuilder> Build(WorldShape shape, ShapeLibrary library, int seed)
        {
            var tiles = new Dictionary<(Biome, int, int), MeshBuilder>();
            var rng = new System.Random(seed);
            var cells = Mathf.FloorToInt(2f * WorldShape.HalfSize / CellSize);
            var limit = shape.OuterRadius - 14f;

            for (var gx = 0; gx < cells; gx++)
            for (var gz = 0; gz < cells; gz++)
            {
                var x = -WorldShape.HalfSize + (gx + (float)rng.NextDouble()) * CellSize;
                var z = -WorldShape.HalfSize + (gz + (float)rng.NextDouble()) * CellSize;
                if (x * x + z * z > limit * limit)
                    continue;
                if (!shape.IsClear(x, z, 4f) || shape.WallDistance(x, z) < 9f)
                    continue;

                var biome = shape.BiomeAt(x, z);
                if (rng.NextDouble() > Density(biome))
                    continue;

                var normal = shape.Normal(x, z);
                if (normal.y < MinNormalY)
                    continue;

                var key = (biome,
                    Mathf.FloorToInt((x + WorldShape.HalfSize) / TileSize),
                    Mathf.FloorToInt((z + WorldShape.HalfSize) / TileSize));
                if (!tiles.TryGetValue(key, out var builder))
                    tiles[key] = builder = new MeshBuilder();

                var origin = new Vector3(x, shape.Height(x, z), z);
                var frame = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
                switch (biome)
                {
                    case Biome.Tundra: AddTundraClump(builder, library, rng, origin, frame); break;
                    case Biome.Rock: AddRockPile(builder, library, rng, origin, frame); break;
                    default: AddCrystalCluster(builder, library, rng, origin, frame); break;
                }
            }
            return tiles;
        }

        static float Density(Biome biome)
        {
            switch (biome)
            {
                case Biome.Tundra: return 0.5f;
                case Biome.Rock: return 0.34f;
                default: return 0.32f;
            }
        }

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        static Color32 Vary(System.Random rng, Color baseColor)
        {
            var v = Range(rng, 0.88f, 1.1f);
            return new Color(Mathf.Clamp01(baseColor.r * v), Mathf.Clamp01(baseColor.g * v), Mathf.Clamp01(baseColor.b * v));
        }

        static void AddTundraClump(MeshBuilder builder, ShapeLibrary library, System.Random rng, Vector3 origin, Quaternion frame)
        {
            var size = Range(rng, 0.8f, 1.6f);
            var bladeCount = rng.Next(8, 15);
            var color = Vary(rng, TuftColors[rng.Next(TuftColors.Length)]);

            for (var i = 0; i < bladeCount; i++)
            {
                var angle = Range(rng, 0f, Mathf.PI * 2f);
                var radial = Mathf.Pow((float)rng.NextDouble(), 0.7f) * 0.8f * size;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var lean = Quaternion.AngleAxis(Range(rng, 5f, 28f), Vector3.Cross(Vector3.up, outward));
                var height = Range(rng, 0.5f, 1.2f) * size;
                var width = Range(rng, 0.14f, 0.26f) * size;
                var rotation = frame * lean * Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
                var position = origin + frame * (outward * radial + Vector3.down * 0.02f);
                builder.Append(library.Blade(rng), Matrix4x4.TRS(position, rotation, new Vector3(width, height, width)), color);
            }

            if (rng.NextDouble() < 0.4)
            {
                var span = Range(rng, 1.2f, 2.4f) * size;
                var offset = frame * new Vector3(Range(rng, -0.6f, 0.6f), -0.05f, Range(rng, -0.6f, 0.6f));
                builder.Append(library.Mound(rng),
                    Matrix4x4.TRS(origin + offset, frame * Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Vector3(span, span * Range(rng, 0.25f, 0.45f), span)),
                    Vary(rng, MoundColor));
            }
        }

        static void AddRockPile(MeshBuilder builder, ShapeLibrary library, System.Random rng, Vector3 origin, Quaternion frame)
        {
            var size = Range(rng, 0.9f, 1.8f);
            var mainHeight = size * Range(rng, 0.6f, 1f);
            var mainColor = Vary(rng, RockColors[rng.Next(RockColors.Length)]);
            builder.Append(library.Rock(rng),
                Matrix4x4.TRS(origin + frame * (Vector3.down * 0.1f), frame * Quaternion.Euler(Range(rng, -15f, 15f), Range(rng, 0f, 360f), Range(rng, -15f, 15f)),
                    new Vector3(size * Range(rng, 1f, 1.4f), mainHeight, size * Range(rng, 1f, 1.4f))),
                mainColor);

            var extra = rng.Next(3, 7);
            for (var i = 0; i < extra; i++)
            {
                var small = size * Range(rng, 0.3f, 0.8f);
                var color = Vary(rng, RockColors[rng.Next(RockColors.Length)]);
                Vector3 local;
                if (rng.NextDouble() < 0.35)
                    local = new Vector3(Range(rng, -0.4f, 0.4f) * size, mainHeight * 0.6f, Range(rng, -0.4f, 0.4f) * size);
                else
                {
                    var angle = Range(rng, 0f, Mathf.PI * 2f);
                    var distance = Range(rng, 0.6f, 1.8f) * size;
                    local = new Vector3(Mathf.Cos(angle) * distance, -0.05f, Mathf.Sin(angle) * distance);
                }
                var rotation = frame * Quaternion.Euler(Range(rng, -35f, 35f), Range(rng, 0f, 360f), Range(rng, -35f, 35f));
                builder.Append(library.Rock(rng),
                    Matrix4x4.TRS(origin + frame * local, rotation, new Vector3(small, small * Range(rng, 0.6f, 1.1f), small)),
                    color);
            }
        }

        static void AddCrystalCluster(MeshBuilder builder, ShapeLibrary library, System.Random rng, Vector3 origin, Quaternion frame)
        {
            var size = Range(rng, 0.8f, 1.5f);
            var count = rng.Next(4, 9);
            var color = Vary(rng, CrystalColors[rng.Next(CrystalColors.Length)]);

            for (var i = 0; i < count; i++)
            {
                var angle = Range(rng, 0f, Mathf.PI * 2f);
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var tilt = i == 0 ? Range(rng, 0f, 10f) : Range(rng, 12f, 40f);
                var lean = Quaternion.AngleAxis(tilt, Vector3.Cross(Vector3.up, outward));
                var height = (i == 0 ? Range(rng, 2f, 3.2f) : Range(rng, 0.8f, 2.4f)) * size;
                var width = Range(rng, 0.25f, 0.5f) * size;
                var rotation = frame * lean * Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
                var position = origin + frame * (outward * (i == 0 ? 0f : Range(rng, 0.05f, 0.45f) * size) + Vector3.down * 0.05f);
                builder.Append(library.Crystal(rng), Matrix4x4.TRS(position, rotation, new Vector3(width, height, width)), color);
            }
        }
    }
}
