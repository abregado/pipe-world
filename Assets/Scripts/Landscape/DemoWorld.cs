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

        public WorldShape Shape { get; private set; }

        void Awake()
        {
            var timer = Stopwatch.StartNew();
            Shape = new WorldShape(seed);
            var library = new ShapeLibrary(seed);

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
            foreach (var entry in ScatterBuilder.Build(Shape, library, seed + 2))
            {
                scatterTriangles += entry.Value.TriangleCount;
                var name = $"Scatter {entry.Key.biome} {entry.Key.tx},{entry.Key.tz}";
                culling.Register(CreateObject(name, entry.Value.ToMesh(name), new[] { scatterMaterial }, false, ShadowCastingMode.On), 420f, 220f, 110f);
            }

            var grassTriangles = 0;
            foreach (var entry in GrassBuilder.Build(Shape, library, seed + 3))
            {
                grassTriangles += entry.Value.TriangleCount;
                var name = $"Grass {entry.Key.tx},{entry.Key.tz}";
                culling.Register(CreateObject(name, entry.Value.ToMesh(name), new[] { grassMaterial }, false, ShadowCastingMode.Off), 95f, 60f, 0f);
            }

            TorchBuilder.BuildAll(transform, Shape, library, scatterMaterial, seed + 4);

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
