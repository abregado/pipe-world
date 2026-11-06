using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace PipeWorld.Landscape
{
    public class DemoWorld : MonoBehaviour
    {
        public int seed = 1337;
        public float terrainCellSize = 4f;
        // Indexed by Biome.
        public Material[] terrainMaterials;
        public Material scatterMaterial;
        public Material grassMaterial;
        public Material flameMaterial;

        const float ScatterDayDistance = 420f;
        const float ScatterNightDistance = 220f;
        const float GrassDayDistance = 95f;
        const float GrassNightDistance = 60f;

        public WorldShape Shape { get; private set; }
        public ShapeLibrary Library { get; private set; }
        // The scatter material with the distance dissolve off, for props that must stay solid at any range.
        public Material PropMaterial { get; private set; }

        void Awake()
        {
            var timer = Stopwatch.StartNew();
            Shape = new WorldShape(seed);
            var library = new ShapeLibrary(seed);
            Library = library;

            var terrainMesh = TerrainMeshBuilder.Build(Shape, terrainCellSize);
            CreateObject("Terrain", terrainMesh, terrainMaterials, true, ShadowCastingMode.On);

            var cliffs = CliffBuilder.Build(Shape, library, seed + 1);
            var cliffTriangles = 0;
            for (var i = 0; i < cliffs.Length; i++)
            {
                cliffTriangles += cliffs[i].TriangleCount;
                CreateObject($"Cliffs {(Biome)i}", cliffs[i].ToMesh($"Cliffs {(Biome)i}"), new[] { terrainMaterials[i] }, true, ShadowCastingMode.On);
            }

            var scatterTriangles = 0;
            var culling = gameObject.AddComponent<ScatterCulling>();
            culling.RegisterFade("_PW_ScatterDrawDistance", ScatterDayDistance, ScatterNightDistance);
            culling.RegisterFade("_PW_GrassDrawDistance", GrassDayDistance, GrassNightDistance);
            foreach (var entry in ScatterBuilder.Build(Shape, library, seed + 2))
            {
                scatterTriangles += entry.Value.TriangleCount;
                var name = $"Scatter {entry.Key.biome} {entry.Key.tx},{entry.Key.tz}";
                culling.Register(CreateObject(name, entry.Value.ToMesh(name), new[] { scatterMaterial }, false, ShadowCastingMode.On), ScatterDayDistance, ScatterNightDistance, 110f);
            }

            var grassTriangles = 0;
            foreach (var entry in GrassBuilder.Build(Shape, library, seed + 3))
            {
                grassTriangles += entry.Value.TriangleCount;
                var name = $"Grass {entry.Key.tx},{entry.Key.tz}";
                culling.Register(CreateObject(name, entry.Value.ToMesh(name), new[] { grassMaterial }, false, ShadowCastingMode.Off), GrassDayDistance, GrassNightDistance, 0f);
            }

            // Torches and beacons are seen from across the crater, so their parts must not dissolve with the scatter.
            PropMaterial = new Material(scatterMaterial) { name = "Scatter (no fade)" };
            PropMaterial.SetFloat("_DistanceFade", 0f);
            TorchBuilder.BuildAll(transform, Shape, library, PropMaterial, flameMaterial, terrainMaterials, seed + 4);

            foreach (Transform child in transform)
                if (child.name == "Monolith" || child.name == "Campfire")
                    Debug.Log($"[DemoWorld] {child.name.ToLowerInvariant()} at ({child.position.x:F0}, {child.position.y:F1}, {child.position.z:F0})");
            foreach (var plateau in Shape.Plateaus)
                Debug.Log($"[DemoWorld] plateau at ({plateau.center.x:F0}, {plateau.center.y:F0}) radius {plateau.radius:F1} height {plateau.height:F1} top {plateau.baseHeight + plateau.height:F1} ramp {plateau.rampDeg:F0} deg");
            foreach (var mesa in Shape.Mesas)
                Debug.Log($"[DemoWorld] mesa at ({mesa.center.x:F0}, {mesa.center.y:F0}) radius {mesa.radius:F1} ground {Shape.Height(mesa.center.x, mesa.center.y):F1}");

            Debug.Log($"[DemoWorld] built in {timer.ElapsedMilliseconds} ms: terrain {terrainMesh.triangles.Length / 3} tris, cliffs {cliffTriangles} tris, scatter {scatterTriangles} tris, grass {grassTriangles} tris");
        }

        MeshRenderer CreateObject(string objectName, Mesh mesh, Material[] materials, bool collider, ShadowCastingMode shadows)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = materials;
            meshRenderer.shadowCastingMode = shadows;
            if (collider)
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return meshRenderer;
        }
    }
}
