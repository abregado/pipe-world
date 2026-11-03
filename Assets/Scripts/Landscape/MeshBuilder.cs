using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PipeWorld.Landscape
{
    public struct Tri
    {
        public Vector3 a, b, c;
        public float shade;
        public byte emissive;
        // Per-vertex weights written to UV0.x when the builder tracks them (grass sway).
        public Vector3 weights;
    }

    // Every triangle gets its own three vertices so normals are flat.
    public sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Color32> colors = new List<Color32>();
        readonly List<int>[] indices;
        readonly List<Vector2> weights;

        public MeshBuilder(int submeshCount = 1, bool trackWeights = false)
        {
            if (trackWeights)
                weights = new List<Vector2>();
            indices = new List<int>[submeshCount];
            for (var i = 0; i < submeshCount; i++)
                indices[i] = new List<int>();
        }

        public int TriangleCount => vertices.Count / 3;

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, int submesh, Color32 color, Vector3? facing = null, Vector3 vertexWeights = default)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f)
                return;
            if (facing.HasValue && Vector3.Dot(n, facing.Value) < 0f)
            {
                (b, c) = (c, b);
                (vertexWeights.y, vertexWeights.z) = (vertexWeights.z, vertexWeights.y);
                n = -n;
            }
            n.Normalize();

            var first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            if (weights != null)
            {
                weights.Add(new Vector2(vertexWeights.x, 0f));
                weights.Add(new Vector2(vertexWeights.y, 0f));
                weights.Add(new Vector2(vertexWeights.z, 0f));
            }
            for (var i = 0; i < 3; i++)
            {
                normals.Add(n);
                colors.Add(color);
            }
            var list = indices[submesh];
            list.Add(first);
            list.Add(first + 1);
            list.Add(first + 2);
        }

        public void Append(Tri[] template, Matrix4x4 matrix, Color32 color, int submesh = 0)
        {
            foreach (var t in template)
            {
                var shaded = new Color32(
                    (byte)Mathf.Min(255f, color.r * t.shade),
                    (byte)Mathf.Min(255f, color.g * t.shade),
                    (byte)Mathf.Min(255f, color.b * t.shade),
                    t.emissive);
                AddTriangle(matrix.MultiplyPoint3x4(t.a), matrix.MultiplyPoint3x4(t.b), matrix.MultiplyPoint3x4(t.c), submesh, shaded, null, t.weights);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            if (weights != null)
                mesh.SetUVs(0, weights);
            mesh.subMeshCount = indices.Length;
            for (var i = 0; i < indices.Length; i++)
                mesh.SetTriangles(indices[i], i, false);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
