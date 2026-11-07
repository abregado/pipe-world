using System.Collections.Generic;
using System.Linq;
using PipeWorld.Landscape;
using UnityEngine;

namespace PipeWorld.Piper
{
    // The scene object for one building: a shell mesh with collision (floors, walls with openings), a scaffold under the floors, and a
    // ceiling that can be hidden. Props are ordinary world props, shown by the prop syncer.
    public class BuildingView : MonoBehaviour
    {
        public uint buildingId;
        public ulong contentHash;

        private GameObject shell;
        private GameObject ceiling;
        private GameObject scaffold;
        private Material material;

        public static BuildingView Create(uint id, Transform parent, Material material)
        {
            var go = new GameObject("Building " + EntityId.counter(id));
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BuildingView>();
            view.buildingId = id;
            view.material = material;
            return view;
        }

        public void SetCeilingVisible(bool visible)
        {
            if (ceiling != null && ceiling.activeSelf != visible)
                ceiling.SetActive(visible);
        }

        public void Rebuild(BuildingModel m)
        {
            transform.position = m.position;
            transform.rotation = Quaternion.Euler(0f, m.yaw, 0f);

            bool ceilingVisible = ceiling == null || ceiling.activeSelf;
            if (shell != null) Destroy(shell);
            if (ceiling != null) Destroy(ceiling);
            if (scaffold != null) Destroy(scaffold);

            float h = m.height;
            float scaffoldDepth = Mathf.Max(0f, m.position.y - m.groundY) + BuildingConstants.FoundationDepth;
            var shellBuilder = new MeshBuilder();
            var ceilingBuilder = new MeshBuilder();
            var scaffoldBuilder = new MeshBuilder();
            var blockerBuilder = new MeshBuilder();

            foreach (BArea area in m.areas)
            {
                var ring = m.areaVertices(area).Select(v => new Vector3(v.x, 0f, v.z)).ToList();
                BuildingMeshes.AddPolygon(shellBuilder, ring, true, BuildingMeshes.FloorColor);

                var top = ring.Select(p => new Vector3(p.x, h, p.z)).ToList();
                BuildingMeshes.AddPolygon(ceilingBuilder, top, false, BuildingMeshes.CeilingColor);
                BuildingMeshes.AddPolygon(ceilingBuilder, top, true, BuildingMeshes.CeilingColor);

                AddScaffold(scaffoldBuilder, ring, scaffoldDepth);
                AddBlocker(blockerBuilder, ring, scaffoldDepth);
            }

            foreach (BEdge e in m.edges)
                AddWall(shellBuilder, m, e);

            shell = MakeChild("Shell", shellBuilder.ToMesh("Building shell"), true);
            ceiling = MakeChild("Ceiling", ceilingBuilder.ToMesh("Building ceiling"), true);
            ceiling.SetActive(ceilingVisible);

            scaffold = MakeChild("Scaffold", scaffoldBuilder.ToMesh("Building scaffold"), false);
            var blocker = new GameObject("Scaffold blocker");
            blocker.transform.SetParent(scaffold.transform, false);
            blocker.AddComponent<MeshCollider>().sharedMesh = blockerBuilder.ToMesh("Scaffold blocker");
        }

        private GameObject MakeChild(string name, Mesh mesh, bool collide)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collide)
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        // Posts at the corners and rails around the edge, from the ground up to just under the floor.
        private static void AddScaffold(MeshBuilder b, List<Vector3> ring, float depth)
        {
            const float Post = 0.12f;
            Color32 color = BuildingMeshes.ScaffoldColor;
            float railTop = -0.1f, railBottom = -depth + 0.1f;

            for (int i = 0; i < ring.Count; i++)
            {
                Vector3 a = ring[i], c = ring[(i + 1) % ring.Count];
                BuildingMeshes.AddBox(b, new Vector3(a.x, -depth * 0.5f, a.z), Vector3.right, new Vector3(Post, depth, Post), color);

                Vector3 dir = c - a;
                float length = dir.magnitude;
                if (length < 0.01f)
                    continue;
                dir /= length;

                var rails = new List<float> { railTop, railBottom };
                if (depth > 1.6f)
                    rails.Add(-depth * 0.5f);
                foreach (float y in rails)
                    BuildingMeshes.AddBox(b, new Vector3((a.x + c.x) * 0.5f, y, (a.z + c.z) * 0.5f), dir, new Vector3(length, 0.08f, 0.08f), color);
            }
        }

        // The solid volume under a floor, so the player cannot walk into the space the scaffold holds up.
        private static void AddBlocker(MeshBuilder b, List<Vector3> ring, float depth)
        {
            var bottom = ring.Select(p => new Vector3(p.x, -depth, p.z)).ToList();
            var topRing = ring.Select(p => new Vector3(p.x, 0f, p.z)).ToList();
            BuildingMeshes.AddPolygon(b, bottom, false, BuildingMeshes.ScaffoldColor);
            BuildingMeshes.AddPolygon(b, bottom, true, BuildingMeshes.ScaffoldColor);
            BuildingMeshes.AddPolygon(b, topRing, true, BuildingMeshes.ScaffoldColor);
            BuildingMeshes.AddPolygon(b, topRing, false, BuildingMeshes.ScaffoldColor);

            for (int i = 0; i < ring.Count; i++)
            {
                int j = (i + 1) % ring.Count;
                Vector3 a0 = bottom[i], a1 = bottom[j], b0 = topRing[i], b1 = topRing[j];
                b.AddTriangle(a0, a1, b1, 0, BuildingMeshes.ScaffoldColor);
                b.AddTriangle(a0, b1, b0, 0, BuildingMeshes.ScaffoldColor);
                b.AddTriangle(a0, b1, a1, 0, BuildingMeshes.ScaffoldColor);
                b.AddTriangle(a0, b0, b1, 0, BuildingMeshes.ScaffoldColor);
            }
        }

        // A wall is boxes either side of any openings (doors and archways), with a lintel above each opening.
        private static void AddWall(MeshBuilder b, BuildingModel m, BEdge e)
        {
            float H = m.height;
            const float Depth = BuildingConstants.FoundationDepth;

            BVertex va = m.vertex(e.a), vb = m.vertex(e.b);
            Vector3 a = new Vector3(va.x, 0f, va.z), bpos = new Vector3(vb.x, 0f, vb.z);
            float len = Vector3.Distance(a, bpos);
            Vector3 dir = (bpos - a) / len;
            Color32 color = e.type == 0 ? BuildingMeshes.PartitionColor : BuildingMeshes.WallColor;

            var openings = m.objects.Where(o => o.faceRef == e.id && o.def.surface == PropSurface.Wall && o.def.opening)
                .Select(o => (centre: o.u * len, half: o.def.width * 0.5f, height: o.def.height))
                .OrderBy(o => o.centre).ToList();

            float cursor = 0f;
            foreach (var o in openings)
            {
                Segment(b, a, dir, cursor, o.centre - o.half, len, color, -Depth, H);
                Segment(b, a, dir, o.centre - o.half, o.centre + o.half, len, color, o.height, H);
                cursor = o.centre + o.half;
            }
            Segment(b, a, dir, cursor, len, len, color, -Depth, H);
        }

        private static void Segment(MeshBuilder b, Vector3 origin, Vector3 dir, float s0, float s1, float len, Color32 color, float y0, float y1)
        {
            if (s1 - s0 < 0.001f)
                return;

            // The ends that meet a corner are lengthened by half a wall thickness so neighbouring walls close the corner.
            float e0 = s0 <= 0.0001f ? BuildingConstants.WallThickness * 0.5f : 0f;
            float e1 = s1 >= len - 0.0001f ? BuildingConstants.WallThickness * 0.5f : 0f;
            float length = s1 - s0 + e0 + e1;
            Vector3 center = origin + dir * ((s0 + s1) * 0.5f + (e1 - e0) * 0.5f) + Vector3.up * ((y0 + y1) * 0.5f);
            BuildingMeshes.AddBox(b, center, dir, new Vector3(length, y1 - y0, BuildingConstants.WallThickness), color);
        }
    }
}
