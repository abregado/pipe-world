using PipeWorld.World;
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
        static readonly Color32 White = new Color32(255, 255, 255, 0);

        struct Spec
        {
            public float scale;
            public Color lightColor;
            public Color32 flameColor;
            public float intensity;
            public float range;
            public float glow;
            public float warmLut;
            public float gradeRadiusScale;
            public float shadowDistance;
        }

        static Spec Torch() => new Spec
        {
            scale = 1f,
            lightColor = new Color(1f, 0.62f, 0.3f),
            flameColor = new Color32(255, 150, 50, 0),
            // A short range keeps each torch's pool to itself: overlapping pools fill in each other's shadows.
            intensity = 9f,
            range = 12f,
            glow = 0.07f,
            warmLut = 1f,
            gradeRadiusScale = 1.25f,
            shadowDistance = 90f,
        };

        // Beacons are lit in the colour of the biome they stand in: warm on tundra and rock, cold cyan on ice.
        static Spec Beacon(Biome biome)
        {
            var spec = new Spec
            {
                scale = 5f,
                intensity = 70f,
                range = 60f,
                glow = 0.14f,
                gradeRadiusScale = 0.9f,
                shadowDistance = 220f,
            };
            switch (biome)
            {
                case Biome.Ice:
                    spec.lightColor = new Color(0.35f, 0.8f, 1f);
                    spec.flameColor = new Color32(110, 210, 255, 0);
                    spec.warmLut = 0f;
                    break;
                case Biome.Rock:
                    spec.lightColor = new Color(1f, 0.42f, 0.2f);
                    spec.flameColor = new Color32(255, 105, 40, 0);
                    spec.warmLut = 1f;
                    break;
                default:
                    spec.lightColor = new Color(1f, 0.62f, 0.3f);
                    spec.flameColor = new Color32(255, 150, 50, 0);
                    spec.warmLut = 1f;
                    break;
            }
            return spec;
        }

        public static void BuildAll(Transform parent, WorldShape shape, ShapeLibrary library, Material material, Material flameMaterial, Material[] terrainMaterials, int seed)
        {
            var rng = new System.Random(seed);
            var context = new Context { parent = parent, shape = shape, library = library, material = material, flameMaterial = flameMaterial, terrainMaterials = terrainMaterials };

            Build(context, rng, new Vector2(4.5f, 6f), Torch(), 2);
            Build(context, rng, new Vector2(-5f, 8f), Torch(), 2);

            const int ringCount = 8;
            for (var i = 0; i < ringCount; i++)
            {
                var angle = (i + (float)rng.NextDouble() * 0.6f) / ringCount * Mathf.PI * 2f;
                var radius = Mathf.Lerp(12f, 25f, (float)rng.NextDouble());
                Build(context, rng, new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius), Torch(), 2);
            }

            BuildBeacons(context, rng);
            BuildCaveTorches(context, rng);
        }

        sealed class Context
        {
            public Transform parent;
            public WorldShape shape;
            public ShapeLibrary library;
            public Material material;
            public Material flameMaterial;
            public Material[] terrainMaterials;
        }

        // One beacon on the start floor, then two on opposite sides of every terrace.
        static void BuildBeacons(Context c, System.Random rng)
        {
            BuildBeacon(c, rng, new Vector2(-14f, 35f));

            for (var k = 1; k < c.shape.Rings.Length; k++)
            {
                var inner = c.shape.Rings[k - 1].radius + 30f;
                var outer = c.shape.Rings[k].radius - 30f;
                var firstAngle = (float)rng.NextDouble() * Mathf.PI * 2f;
                for (var side = 0; side < 2; side++)
                {
                    for (var attempt = 0; attempt < 40; attempt++)
                    {
                        var angle = firstAngle + side * Mathf.PI + (float)(rng.NextDouble() - 0.5) * 0.6f;
                        var radius = Mathf.Lerp(inner, outer, (float)rng.NextDouble());
                        var x = Mathf.Cos(angle) * radius;
                        var z = Mathf.Sin(angle) * radius;
                        if (!c.shape.IsClear(x, z, 12f) || c.shape.WallDistance(x, z) < 25f)
                            continue;

                        BuildBeacon(c, rng, new Vector2(x, z));
                        break;
                    }
                }
            }
        }

        static void BuildBeacon(Context c, System.Random rng, Vector2 xz)
        {
            Build(c, rng, xz, Beacon(c.shape.BiomeAt(xz.x, xz.y)), 2);
        }

        // Each cave gets one torch near its back wall, centred, with no rocks: the cave walls cast the shadows.
        static void BuildCaveTorches(Context c, System.Random rng)
        {
            foreach (var cave in c.shape.Caves)
            {
                var ring = c.shape.Rings[cave.ring];
                var theta = cave.thetaDeg * Mathf.Deg2Rad;
                var backFace = c.shape.RingRadiusBase(ring, theta) + cave.depth - 5.5f;
                var radius = backFace - 1.8f;
                Build(c, rng, new Vector2(Mathf.Cos(theta) * radius, Mathf.Sin(theta) * radius), Torch(), 0);
            }
        }

        static void Build(Context c, System.Random rng, Vector2 xz, Spec spec, int rockCount)
        {
            var s = spec.scale;
            var ground = c.shape.Height(xz.x, xz.y);
            var root = new GameObject(s > 1f ? "Beacon" : "Torch");
            root.transform.SetParent(c.parent, false);
            root.transform.position = new Vector3(xz.x, ground, xz.y);

            var body = new MeshBuilder();
            body.Append(c.library.FlatBlock(rng), Matrix4x4.TRS(new Vector3(0f, -0.1f * s, 0f), Quaternion.Euler(0f, Random(rng), 0f), new Vector3(0.18f * s, (PostHeight + 0.1f) * s, 0.18f * s)), Wood);
            AddRenderer(root.transform, "Body", body.ToMesh("Torch Body"), c.material, ShadowCastingMode.On);
            // The brazier sits just below the light, so as a caster it would throw a dark disc metres wide around its own torch.
            var brazier = new MeshBuilder();
            brazier.Append(c.library.FlatBlock(rng), Matrix4x4.TRS(new Vector3(0f, (PostHeight - 0.1f) * s, 0f), Quaternion.Euler(0f, Random(rng), 0f), new Vector3(0.5f * s, 0.28f * s, 0.5f * s)), Iron);
            AddRenderer(root.transform, "Brazier", brazier.ToMesh("Torch Brazier"), c.material, ShadowCastingMode.Off);

            var flameBuilder = new MeshBuilder();
            flameBuilder.Append(Glowing(c.library.Crystal(rng)), Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, Random(rng), 0f), new Vector3(0.3f * s, 0.7f * s, 0.3f * s)), spec.flameColor);
            var flame = AddRenderer(root.transform, "Flame", flameBuilder.ToMesh("Torch Flame"), c.flameMaterial, ShadowCastingMode.Off);
            flame.transform.localPosition = new Vector3(0f, (PostHeight + 0.05f) * s, 0f);

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 0.22f * s;
            capsule.height = (PostHeight + 0.2f) * s;
            capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, LightHeight * s, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = spec.lightColor;
            light.intensity = spec.intensity;
            light.range = spec.range;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.95f;

            var grade = lightObject.AddComponent<GradeLight>();
            grade.source = light;
            grade.tint = spec.lightColor;
            grade.warmLut = spec.warmLut;
            grade.glow = spec.glow;
            grade.radiusScale = spec.gradeRadiusScale;

            var flicker = root.AddComponent<TorchFlicker>();
            flicker.torchLight = light;
            flicker.flame = flame.transform;
            flicker.seed = (float)rng.NextDouble() * 100f;
            flicker.baseIntensity = spec.intensity;
            flicker.baseRange = spec.range;
            flicker.shadowDistance = spec.shadowDistance;

            AddShadowRocks(c, root.transform, rng, xz, ground, spec, rockCount);
        }

        // Large rocks beside the light, in the biome's terrain material, so the light has something to cast shadows from.
        static void AddShadowRocks(Context c, Transform parent, System.Random rng, Vector2 xz, float ground, Spec spec, int count)
        {
            var biome = c.shape.BiomeAt(xz.x, xz.y);
            var sizeScale = spec.scale > 1f ? 2.4f : 1f;

            for (var i = 0; i < count; i++)
            {
                var angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                var distance = Mathf.Lerp(2.4f, 3.8f, (float)rng.NextDouble()) * spec.scale;
                var x = xz.x + Mathf.Cos(angle) * distance;
                var z = xz.y + Mathf.Sin(angle) * distance;
                var width = Mathf.Lerp(1.6f, 2.6f, (float)rng.NextDouble()) * sizeScale;
                var height = Mathf.Lerp(1.6f, 2.8f, (float)rng.NextDouble()) * sizeScale;
                var y = c.shape.Height(x, z) - height * 0.3f;

                var go = new GameObject("Shadow Rock");
                go.transform.SetParent(parent, true);
                go.transform.SetPositionAndRotation(new Vector3(x, y, z),
                    Quaternion.Euler(Mathf.Lerp(-10f, 10f, (float)rng.NextDouble()), Random(rng), Mathf.Lerp(-10f, 10f, (float)rng.NextDouble())));
                go.transform.localScale = new Vector3(width, height, width * Mathf.Lerp(0.8f, 1.2f, (float)rng.NextDouble()));

                var builder = new MeshBuilder();
                builder.Append(c.library.Rock(rng), Matrix4x4.identity, White);
                var mesh = builder.ToMesh("Shadow Rock");
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = c.terrainMaterials[(int)biome];
                renderer.shadowCastingMode = ShadowCastingMode.On;
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = true;
            }
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
        // Point-light shadows cost six depth passes each, so only lights near the camera cast them.
        public float shadowDistance = 90f;
        public LightShadows shadowType = LightShadows.Soft;
        // By day the torches would otherwise light the ground about as much as the low sun, and fill in its shadows.
        [Range(0f, 1f)] public float dayIntensity = 0.2f;

        DayNightCycle cycle;

        void Start() => cycle = FindAnyObjectByType<DayNightCycle>();

        void Update()
        {
            var slow = Mathf.PerlinNoise(Time.time * 2.3f + seed, seed);
            var fast = Mathf.PerlinNoise(Time.time * 9f + seed, seed + 31f);
            var flutter = 0.8f + 0.3f * slow + 0.15f * fast;

            var daylight = cycle != null ? Mathf.Lerp(dayIntensity, 1f, cycle.NightFactor) : 1f;
            torchLight.intensity = baseIntensity * flutter * daylight;
            torchLight.range = baseRange * (0.94f + 0.06f * flutter);
            flame.localScale = new Vector3(0.9f + 0.2f * fast, 0.8f + 0.5f * slow, 0.9f + 0.2f * (1f - fast));

            var cam = Camera.main;
            if (cam != null)
            {
                var near = (cam.transform.position - transform.position).sqrMagnitude < shadowDistance * shadowDistance;
                torchLight.shadows = near ? shadowType : LightShadows.None;
            }
        }
    }
}
