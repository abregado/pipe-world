using System.Collections.Generic;
using UnityEngine;

namespace PipeWorld.Landscape
{
    public static class GrassBuilder
    {
        public const float TileSize = 48f;
        public const float Range = 230f;
        const float CellSize = 1f;
        const float PatchScale = 11f;
        const float PatchThreshold = 0.42f;
        const float MinNormalY = 0.85f;

        static readonly Color[] Colors =
        {
            new Color(0.40f, 0.50f, 0.30f), new Color(0.52f, 0.55f, 0.34f),
            new Color(0.60f, 0.58f, 0.40f), new Color(0.44f, 0.54f, 0.36f), new Color(0.72f, 0.80f, 0.74f),
        };
        static readonly Color[] MeadowColors =
        {
            new Color(0.34f, 0.56f, 0.24f), new Color(0.42f, 0.62f, 0.26f), new Color(0.30f, 0.48f, 0.22f), new Color(0.50f, 0.64f, 0.30f),
        };

        public static Dictionary<(int tx, int tz), MeshBuilder> Build(WorldShape shape, ShapeLibrary library, int seed)
        {
            var tiles = new Dictionary<(int, int), MeshBuilder>();
            var rng = new System.Random(seed);
            var cells = Mathf.FloorToInt(2f * Range / CellSize);

            for (var gx = 0; gx < cells; gx++)
            for (var gz = 0; gz < cells; gz++)
            {
                var x = -Range + (gx + (float)rng.NextDouble()) * CellSize;
                var z = -Range + (gz + (float)rng.NextDouble()) * CellSize;
                if (!shape.InPlayArea(x, z, 14f))
                    continue;
                var biome = shape.BiomeAt(x, z);
                if (biome != Biome.Tundra && biome != Biome.Grass)
                    continue;
                // Tundra grass grows in patches near the start; grassland is covered almost everywhere.
                var threshold = biome == Biome.Grass ? 0.25f : PatchThreshold;
                if (biome == Biome.Tundra && x * x + z * z > 170f * 170f)
                    continue;
                if (Mathf.PerlinNoise(x / PatchScale + 50f, z / PatchScale + 50f) < threshold)
                    continue;
                if (shape.WallDistance(x, z) < 7f)
                    continue;
                if (shape.Normal(x, z).y < MinNormalY)
                    continue;

                var key = (Mathf.FloorToInt((x + Range) / TileSize), Mathf.FloorToInt((z + Range) / TileSize));
                if (!tiles.TryGetValue(key, out var builder))
                    tiles[key] = builder = new MeshBuilder(1, true);

                AddClump(builder, library, rng, new Vector3(x, shape.Height(x, z), z), biome == Biome.Grass ? MeadowColors : Colors);
            }
            return tiles;
        }

        static float Rand(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        static void AddClump(MeshBuilder builder, ShapeLibrary library, System.Random rng, Vector3 origin, Color[] palette)
        {
            var baseColor = palette[rng.Next(palette.Length)];
            var variation = Rand(rng, 0.9f, 1.1f);
            var color = new Color(Mathf.Clamp01(baseColor.r * variation), Mathf.Clamp01(baseColor.g * variation), Mathf.Clamp01(baseColor.b * variation));
            var clumpHeight = Rand(rng, 0.32f, 0.62f);
            var blades = rng.Next(6, 11);

            for (var i = 0; i < blades; i++)
            {
                var angle = Rand(rng, 0f, Mathf.PI * 2f);
                var radial = Mathf.Sqrt((float)rng.NextDouble()) * 0.35f;
                var position = origin + new Vector3(Mathf.Cos(angle) * radial, -0.02f, Mathf.Sin(angle) * radial);
                var rotation = Quaternion.Euler(Rand(rng, -15f, 15f), Rand(rng, 0f, 360f), Rand(rng, -12f, 12f));
                var height = clumpHeight * Rand(rng, 0.7f, 1.2f);
                var width = Rand(rng, 0.08f, 0.14f);
                builder.Append(library.GrassBlade(rng), Matrix4x4.TRS(position, rotation, new Vector3(width, height, 1f)), color);
            }
        }
    }
}
