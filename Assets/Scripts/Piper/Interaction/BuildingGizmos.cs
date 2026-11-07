using System.Collections.Generic;
using UnityEngine;

namespace PipeWorld.Piper
{
    // Immediate-mode lines and markers for edit previews. Call Begin, draw, then End each tick; pooled objects are reused.
    public class BuildingGizmos
    {
        private readonly Transform root;
        private readonly Material baseMaterial;
        private readonly Dictionary<Color32, Material> materials = new Dictionary<Color32, Material>();
        private readonly List<LineRenderer> lines = new List<LineRenderer>();
        private readonly List<Transform> markers = new List<Transform>();
        private int lineUsed;
        private int markerUsed;

        public BuildingGizmos(string name)
        {
            root = new GameObject(name).transform;
            baseMaterial = Resources.Load<Material>("Materials/Gizmo");
            SUtil.ReleaseAssert(baseMaterial != null, "Resources/Materials/Gizmo.mat is missing");
        }

        public void Begin()
        {
            lineUsed = 0;
            markerUsed = 0;
        }

        public void End()
        {
            for (int i = lineUsed; i < lines.Count; i++)
                lines[i].gameObject.SetActive(false);
            for (int i = markerUsed; i < markers.Count; i++)
                markers[i].gameObject.SetActive(false);
        }

        public void Clear()
        {
            Begin();
            End();
        }

        public void Destroy()
        {
            if (root != null)
                Object.Destroy(root.gameObject);
        }

        private Material MaterialFor(Color color)
        {
            Color32 key = color;
            if (!materials.TryGetValue(key, out Material m))
            {
                m = new Material(baseMaterial);
                m.SetColor("_BaseColor", color);
                materials[key] = m;
            }
            return m;
        }

        public void Line(Vector3 a, Vector3 b, Color color, float width = 0.05f)
        {
            if (lineUsed == lines.Count)
            {
                var go = new GameObject("Line");
                go.transform.SetParent(root, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lines.Add(lr);
            }

            LineRenderer line = lines[lineUsed++];
            line.gameObject.SetActive(true);
            line.sharedMaterial = MaterialFor(color);
            line.startWidth = line.endWidth = width;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
        }

        public void Marker(Vector3 position, Color color, float size)
        {
            if (markerUsed == markers.Count)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(root, false);
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                markers.Add(go.transform);
            }

            Transform marker = markers[markerUsed++];
            marker.gameObject.SetActive(true);
            marker.position = position;
            marker.localScale = Vector3.one * size;
            marker.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(color);
        }

        public void Loop(IList<Vector3> points, Color color, float width = 0.05f)
        {
            for (int i = 0; i < points.Count; i++)
                Line(points[i], points[(i + 1) % points.Count], color, width);
        }

        // A wireframe box. `along` is the horizontal direction of the box's long axis.
        public void Box(Vector3 center, Vector3 along, Vector3 size, Color color, float width = 0.04f)
        {
            Vector3 ax = along.normalized, up = Vector3.up, side = Vector3.Cross(up, ax);
            Vector3 hx = ax * (size.x * 0.5f), hy = up * (size.y * 0.5f), hz = side * (size.z * 0.5f);
            Vector3[] c = new Vector3[8];
            for (int i = 0; i < 8; i++)
                c[i] = center + hx * ((i & 1) == 0 ? -1 : 1) + hy * ((i & 2) == 0 ? -1 : 1) + hz * ((i & 4) == 0 ? -1 : 1);

            foreach (var pair in new[] { (0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7) })
                Line(c[pair.Item1], c[pair.Item2], color, width);
        }
    }
}
