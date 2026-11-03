using UnityEngine;

namespace PipeWorld.Landscape
{
    public static class TerrainMeshBuilder
    {
        // One submesh per Biome, in enum order.
        public static Mesh Build(WorldShape shape, float cell)
        {
            var n = Mathf.RoundToInt(2f * WorldShape.HalfSize / cell);
            var heights = new float[n + 1, n + 1];
            for (var ix = 0; ix <= n; ix++)
            for (var iz = 0; iz <= n; iz++)
                heights[ix, iz] = shape.Height(-WorldShape.HalfSize + ix * cell, -WorldShape.HalfSize + iz * cell);

            var builder = new MeshBuilder(3);
            var white = new Color32(255, 255, 255, 0);
            for (var ix = 0; ix < n; ix++)
            for (var iz = 0; iz < n; iz++)
            {
                var x0 = -WorldShape.HalfSize + ix * cell;
                var z0 = -WorldShape.HalfSize + iz * cell;
                var p00 = new Vector3(x0, heights[ix, iz], z0);
                var p10 = new Vector3(x0 + cell, heights[ix + 1, iz], z0);
                var p01 = new Vector3(x0, heights[ix, iz + 1], z0 + cell);
                var p11 = new Vector3(x0 + cell, heights[ix + 1, iz + 1], z0 + cell);

                if (((ix + iz) & 1) == 0)
                {
                    AddTri(builder, shape, p00, p01, p11, white);
                    AddTri(builder, shape, p00, p11, p10, white);
                }
                else
                {
                    AddTri(builder, shape, p00, p01, p10, white);
                    AddTri(builder, shape, p01, p11, p10, white);
                }
            }
            return builder.ToMesh("Terrain");
        }

        static void AddTri(MeshBuilder builder, WorldShape shape, Vector3 a, Vector3 b, Vector3 c, Color32 color)
        {
            var centroid = (a + b + c) / 3f;
            var biome = shape.BiomeAt(centroid.x, centroid.z);
            builder.AddTriangle(a, b, c, (int)biome, color, Vector3.up);
        }
    }
}
