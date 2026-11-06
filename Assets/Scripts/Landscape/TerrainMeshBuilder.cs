using System.Collections.Generic;
using UnityEngine;

namespace PipeWorld.Landscape
{
    public static class TerrainMeshBuilder
    {
        // Beyond the crater the cells grow by this factor per row, up to MaxFarCell, out to WorldShape.FarExtent.
        const float FarGrowth = 1.08f;
        const float MaxFarCell = 64f;
        // Far cells are only built where they lie at least this far toward the open side (the projection of the cell
        // centre on the open direction), so the lowland can be seen; behind the wall nothing is visible.
        const float FarMinProjection = -150f;

        // One submesh per Biome, in enum order. The grid is regular (cell metres) inside HalfSize and coarser beyond,
        // with shared rows and columns, so the two parts meet without cracks.
        public static Mesh Build(WorldShape shape, float cell)
        {
            var xs = Axis(cell);
            var zs = Axis(cell);
            var open = WorldShape.OpenCenterDeg * Mathf.Deg2Rad;
            openDirection = new Vector2(Mathf.Cos(open), Mathf.Sin(open));
            var heights = new float[xs.Count, zs.Count];
            for (var ix = 0; ix < xs.Count; ix++)
            for (var iz = 0; iz < zs.Count; iz++)
            {
                if (!Needed(xs, zs, ix, iz))
                    continue;
                heights[ix, iz] = shape.Height(xs[ix], zs[iz]);
            }

            var builder = new MeshBuilder(System.Enum.GetValues(typeof(Biome)).Length);
            var white = new Color32(255, 255, 255, 0);
            for (var ix = 0; ix < xs.Count - 1; ix++)
            for (var iz = 0; iz < zs.Count - 1; iz++)
            {
                if (!CellBuilt(xs, zs, ix, iz))
                    continue;
                var p00 = new Vector3(xs[ix], heights[ix, iz], zs[iz]);
                var p10 = new Vector3(xs[ix + 1], heights[ix + 1, iz], zs[iz]);
                var p01 = new Vector3(xs[ix], heights[ix, iz + 1], zs[iz + 1]);
                var p11 = new Vector3(xs[ix + 1], heights[ix + 1, iz + 1], zs[iz + 1]);

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

        static Vector2 openDirection;

        static List<float> Axis(float cell)
        {
            var values = new List<float>();
            var n = Mathf.RoundToInt(2f * WorldShape.HalfSize / cell);
            for (var i = 0; i <= n; i++)
                values.Add(-WorldShape.HalfSize + i * cell);
            Extend(values, cell, 1f);
            var negative = new List<float> { -WorldShape.HalfSize };
            Extend(negative, cell, -1f);
            negative.RemoveAt(0);
            negative.Reverse();
            values.InsertRange(0, negative);
            return values;
        }

        static void Extend(List<float> values, float cell, float sign)
        {
            var step = cell;
            var last = values[values.Count - 1];
            while (Mathf.Abs(last) < WorldShape.FarExtent)
            {
                step = Mathf.Min(step * FarGrowth, MaxFarCell);
                last += sign * step;
                values.Add(last);
            }
        }

        static bool Inside(float v) => Mathf.Abs(v) <= WorldShape.HalfSize + 0.01f;

        static bool CellBuilt(List<float> xs, List<float> zs, int ix, int iz)
        {
            var cx = (xs[ix] + xs[ix + 1]) * 0.5f;
            var cz = (zs[iz] + zs[iz + 1]) * 0.5f;
            return (Inside(cx) && Inside(cz)) || cx * openDirection.x + cz * openDirection.y > FarMinProjection;
        }

        static bool Needed(List<float> xs, List<float> zs, int ix, int iz)
        {
            for (var dx = -1; dx <= 0; dx++)
            for (var dz = -1; dz <= 0; dz++)
            {
                var cx = ix + dx;
                var cz = iz + dz;
                if (cx >= 0 && cz >= 0 && cx < xs.Count - 1 && cz < zs.Count - 1 && CellBuilt(xs, zs, cx, cz))
                    return true;
            }
            return false;
        }

        static void AddTri(MeshBuilder builder, WorldShape shape, Vector3 a, Vector3 b, Vector3 c, Color32 color)
        {
            var centroid = (a + b + c) / 3f;
            var biome = shape.BiomeAt(centroid.x, centroid.z);
            builder.AddTriangle(a, b, c, (int)biome, color, Vector3.up);
        }
    }
}
