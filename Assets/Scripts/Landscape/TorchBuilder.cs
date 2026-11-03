using UnityEngine;
using UnityEngine.Rendering;

namespace PipeWorld.Landscape
{
    public static class TorchBuilder
    {
        const float PostHeight = 1.9f;
        const float LightHeight = 2.35f;

        static readonly Color32 Wood = new Color32(94, 66, 46, 0);
        static readonly Color32 Iron = new Color32(54, 54, 60, 0);
        static readonly Color32 Flame = new Color32(255, 150, 50, 0);

        public static void BuildAll(Transform parent, WorldShape shape, ShapeLibrary library, Material material, int seed)
        {
            var rng = new System.Random(seed);

            // Two beside the spawn point cast real shadows; the rest are unshadowed.
            Build(parent, shape, library, material, rng, new Vector2(4.5f, 6f), true);
            Build(parent, shape, library, material, rng, new Vector2(-5f, 8f), true);

            const int ringCount = 8;
            for (var i = 0; i < ringCount; i++)
            {
                var angle = (i + (float)rng.NextDouble() * 0.6f) / ringCount * Mathf.PI * 2f;
                var radius = Mathf.Lerp(12f, 25f, (float)rng.NextDouble());
                Build(parent, shape, library, material, rng, new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius), false);
            }
        }

        static void Build(Transform parent, WorldShape shape, ShapeLibrary library, Material material, System.Random rng, Vector2 xz, bool shadows)
        {
            var ground = shape.Height(xz.x, xz.y);
            var root = new GameObject("Torch");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(xz.x, ground, xz.y);

            var body = new MeshBuilder();
            body.Append(library.FlatBlock(rng), Matrix4x4.TRS(new Vector3(0f, -0.1f, 0f), Quaternion.Euler(0f, Random(rng), 0f), new Vector3(0.18f, PostHeight + 0.1f, 0.18f)), Wood);
            body.Append(library.FlatBlock(rng), Matrix4x4.TRS(new Vector3(0f, PostHeight - 0.1f, 0f), Quaternion.Euler(0f, Random(rng), 0f), new Vector3(0.5f, 0.28f, 0.5f)), Iron);
            AddRenderer(root.transform, "Body", body.ToMesh("Torch Body"), material, ShadowCastingMode.On);

            var flameBuilder = new MeshBuilder();
            flameBuilder.Append(Glowing(library.Crystal(rng)), Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, Random(rng), 0f), new Vector3(0.3f, 0.7f, 0.3f)), Flame);
            var flame = AddRenderer(root.transform, "Flame", flameBuilder.ToMesh("Torch Flame"), material, ShadowCastingMode.Off);
            flame.transform.localPosition = new Vector3(0f, PostHeight + 0.05f, 0f);

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 0.22f;
            capsule.height = PostHeight + 0.2f;
            capsule.center = new Vector3(0f, (PostHeight + 0.2f) * 0.5f, 0f);

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, LightHeight, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.3f);
            light.intensity = 6f;
            light.range = 24f;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = 0.9f;

            var flicker = root.AddComponent<TorchFlicker>();
            flicker.torchLight = light;
            flicker.flame = flame.transform;
            flicker.seed = (float)rng.NextDouble() * 100f;
        }

        static float Random(System.Random rng) => (float)rng.NextDouble() * 360f;

        // The scatter shader glows by vertex alpha, so the whole flame is lit from within.
        static Tri[] Glowing(Tri[] template)
        {
            var copy = (Tri[])template.Clone();
            for (var i = 0; i < copy.Length; i++)
                copy[i].emissive = 255;
            return copy;
        }

        static MeshRenderer AddRenderer(Transform parent, string name, Mesh mesh, Material material, ShadowCastingMode shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
            return renderer;
        }
    }

    public class TorchFlicker : MonoBehaviour
    {
        public Light torchLight;
        public Transform flame;
        public float seed;
        public float baseIntensity = 6f;
        public float baseRange = 24f;

        void Update()
        {
            var slow = Mathf.PerlinNoise(Time.time * 2.3f + seed, seed);
            var fast = Mathf.PerlinNoise(Time.time * 9f + seed, seed + 31f);
            var flutter = 0.8f + 0.3f * slow + 0.15f * fast;

            torchLight.intensity = baseIntensity * flutter;
            torchLight.range = baseRange * (0.94f + 0.06f * flutter);
            flame.localScale = new Vector3(0.9f + 0.2f * fast, 0.8f + 0.5f * slow, 0.9f + 0.2f * (1f - fast));
        }
    }
}
