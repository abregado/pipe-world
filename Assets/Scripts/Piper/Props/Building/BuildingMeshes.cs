using System.Collections.Generic;
using PipeWorld.Landscape;
using UnityEngine;

namespace PipeWorld.Piper
{
    // Flat-shaded geometry for buildings, built with the landscape's MeshBuilder: vertex colour is albedo, vertex alpha is glow.
    public static class BuildingMeshes
    {
        public static readonly Color32 FloorColor = new Color32(98, 104, 116, 0);
        public static readonly Color32 CeilingColor = new Color32(84, 90, 102, 0);
        public static readonly Color32 WallColor = new Color32(150, 156, 168, 0);
        public static readonly Color32 PartitionColor = new Color32(132, 150, 176, 0);
        public static readonly Color32 ScaffoldColor = new Color32(176, 138, 76, 0);
        public static readonly Color32 TrimColor = new Color32(70, 74, 82, 0);

        // A box whose long axis runs along `along` (horizontal), centred on `center`.
        // size = (length along, height, thickness across).
        public static void AddBox(MeshBuilder b, Vector3 center, Vector3 along, Vector3 size, Color32 color)
        {
            Vector3 ax = along.normalized;
            Vector3 up = Vector3.up;
            Vector3 side = Vector3.Cross(up, ax);
            Vector3 hx = ax * (size.x * 0.5f), hy = up * (size.y * 0.5f), hz = side * (size.z * 0.5f);

            Vector3 P(int sx, int sy, int sz) => center + hx * sx + hy * sy + hz * sz;
            void Face(Vector3 a, Vector3 bb, Vector3 c, Vector3 d, Vector3 normal)
            {
                b.AddTriangle(a, bb, c, 0, color, normal);
                b.AddTriangle(a, c, d, 0, color, normal);
            }

            Face(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), ax);
            Face(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), -ax);
            Face(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), up);
            Face(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), -up);
            Face(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), side);
            Face(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), -side);
        }

        // Triangulates a room outline; the points are in footprint order around the room.
        public static void AddPolygon(MeshBuilder b, List<Vector3> ring, bool facingUp, Color32 color)
        {
            Vector3 facing = facingUp ? Vector3.up : Vector3.down;
            if (ring.Count == 3)
            {
                b.AddTriangle(ring[0], ring[1], ring[2], 0, color, facing);
                return;
            }

            // A quad is split along the diagonal that stays inside it when the room has become concave.
            if (ring.Count == 4)
            {
                if (SameSide(ring[0], ring[2], ring[1], ring[3]))
                {
                    b.AddTriangle(ring[1], ring[2], ring[3], 0, color, facing);
                    b.AddTriangle(ring[1], ring[3], ring[0], 0, color, facing);
                }
                else
                {
                    b.AddTriangle(ring[0], ring[1], ring[2], 0, color, facing);
                    b.AddTriangle(ring[0], ring[2], ring[3], 0, color, facing);
                }
            }
        }

        // True when p and q are on the same side of the line through a and b, so that line is not a valid diagonal.
        private static bool SameSide(Vector3 a, Vector3 b, Vector3 p, Vector3 q)
        {
            float Side(Vector3 v) => (b.x - a.x) * (v.z - a.z) - (b.z - a.z) * (v.x - a.x);
            return Side(p) * Side(q) > 0f;
        }
    }
}
