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
        static readonly Color32 Stone = new Color32(110, 104, 100, 0);
        static readonly Color32 White = new Color32(255, 255, 255, 0);

        enum Kind
        {
            Torch,
            Beacon,
            Campfire,
            Monolith,
        }

        struct Spec
        {
            public Kind kind;
            public float scale;
            public Color lightColor;
            public Color32 flameColor;
            public float intensity;
            public float range;
            public float glow;
            public float warmLut;
            public float gradeRadiusScale;
            public float shadowDistance;
            // Height of the light above the ground.
            public float lightHeight;
            // Shadow rocks stand 2.4 to 3.8 m times rockDistance away, and are rockSize times the torch size.
            public float rockDistance;
            public float rockSize;
            // How far the light drifts from its rest position, in metres, so its shadows move a little.
            public float sway;
        }

        static Spec Torch() => new Spec
        {
            kind = Kind.Torch,
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
            lightHeight = LightHeight,
            rockDistance = 1f,
            rockSize = 1f,
            sway = 0.06f,
        };

        // The light sits just above the logs, so nearby rocks throw long shadows.
        static Spec Campfire() => new Spec
        {
            kind = Kind.Campfire,
            scale = 1f,
            lightColor = new Color(1f, 0.55f, 0.25f),
            flameColor = new Color32(255, 130, 40, 0),
            intensity = 7f,
            range = 11f,
            glow = 0.08f,
            warmLut = 1f,
            gradeRadiusScale = 1.3f,
            shadowDistance = 90f,
            lightHeight = 0.8f,
            rockDistance = 1f,
            rockSize = 1f,
            sway = 0.1f,
        };

        // A tall glowing crystal with its light inside, halfway up. It pulses slowly instead of flickering.
        static Spec Monolith() => new Spec
        {
            kind = Kind.Monolith,
            scale = 1f,
            lightColor = new Color(0.35f, 0.8f, 1f),
            flameColor = new Color32(120, 215, 255, 0),
            intensity = 45f,
            range = 45f,
            glow = 0.16f,
            warmLut = 0f,
            gradeRadiusScale = 1f,
            shadowDistance = 220f,
            lightHeight = 4.5f,
            rockDistance = 3f,
            rockSize = 1.8f,
            sway = 0f,
        };

        // Beacons are lit in the colour of the biome they stand in; on ice a crystal monolith stands instead.
        static Spec Beacon(Biome biome)
        {
            if (biome == Biome.Ice)
                return Monolith();

            var spec = new Spec
            {
                kind = Kind.Beacon,
                scale = 5f,
                intensity = 70f,
                range = 60f,
                glow = 0.14f,
                gradeRadiusScale = 0.9f,
                shadowDistance = 220f,
                lightHeight = LightHeight * 5f,
                rockDistance = 5f,
                rockSize = 2.4f,
                sway = 0.2f,
                warmLut = 1f,
            };
            if (biome == Biome.Rock)
            {
                spec.lightColor = new Color(1f, 0.42f, 0.2f);
                spec.flameColor = new Color32(255, 105, 40, 0);
            }
            else
            {
                spec.lightColor = new Color(1f, 0.62f, 0.3f);
                spec.flameColor = new Color32(255, 150, 50, 0);
            }
            return spec;
        }

        public static void BuildAll(Transform parent, WorldShape shape, ShapeLibrary library, Material material, Material flameMaterial, Material[] terrainMaterials, int seed)
        {
            var rng = new System.Random(seed);
            var context = new Context { parent = parent, shape = shape, library = library, material = material, flameMaterial = flameMaterial, terrainMaterials = terrainMaterials };

            Build(context, rng, new Vector2(4.5f, 6f), Torch(), 2);
            Build(context, rng, new Vector2(-5f, 8f), Torch(), 2);

            // Every other torch around the start is a campfire.
            const int ringCount = 8;
            for (var i = 0; i < ringCount; i++)
            {
                var angle = (i + (float)rng.NextDouble() * 0.6f) / ringCount * Mathf.PI * 2f;
                var radius = Mathf.Lerp(12f, 25f, (float)rng.NextDouble());
                Build(context, rng, new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius), i % 2 == 1 ? Campfire() : Torch(), 2);
            }

            BuildBeacons(context, rng);
            BuildCaveTorches(context, rng);
            BuildPlateauLights(context, rng);
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
                        if (!c.shape.IsClear(x, z, 12f) || c.shape.WallDistance(x, z) < 25f || !c.shape.InPlayArea(x, z, 25f))
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

        // A campfire on top of each plateau, or a monolith on ice, a little off centre.
        static void BuildPlateauLights(Context c, System.Random rng)
        {
            foreach (var plateau in c.shape.Plateaus)
            {
                var angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * plateau.radius * 0.3f;
                var xz = plateau.center + offset;
                var spec = c.shape.BiomeAt(xz.x, xz.y) == Biome.Ice ? Monolith() : Campfire();
                Build(c, rng, xz, spec, 2);
            }
        }

        static void Build(Context c, System.Random rng, Vector2 xz, Spec spec, int rockCount)
        {
            var ground = c.shape.Height(xz.x, xz.y);
            var root = new GameObject(spec.kind.ToString());
            root.transform.SetParent(c.parent, false);
            root.transform.position = new Vector3(xz.x, ground, xz.y);

            Transform flame;
            switch (spec.kind)
            {
                case Kind.Campfire: flame = BuildCampfire(c, rng, root, spec); break;
                case Kind.Monolith: flame = BuildMonolith(c, rng, root, spec); break;
                default: flame = BuildPost(c, rng, root, spec); break;
            }

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, spec.lightHeight, 0f);
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
            flicker.flame = flame;
            flicker.animateFlame = spec.kind != Kind.Monolith;
            flicker.pulse = spec.kind == Kind.Monolith;
            flicker.swayAmount = spec.sway;
            flicker.seed = (float)rng.NextDouble() * 100f;
            flicker.baseIntensity = spec.intensity;
            flicker.baseRange = spec.range;
            flicker.shadowDistance = spec.shadowDistance;

            AddShadowRocks(c, root.transform, rng, xz, spec, rockCount);
        }

        // A wooden post with an iron brazier and a flame on top. Beacons are the same at five times the size.
        static Transform BuildPost(Context c, System.Random rng, GameObject root, Spec spec)
        {
            var s = spec.scale;
            var body = new MeshBuilder();
            body.Append(c.library.FlatBlock(rng), Matrix4x4.TRS(new Vector3(0f, -0.1f * s, 0f), Quaternion.Euler(0f, Random(rng), 0f), new Vector3(0.18f * s, (PostHeight + 0.1f) * s, 0.18f * s)), Wood);
            AddRenderer(root.transform, "Body", body.ToMesh("Torch Body"), c.material, ShadowCastingMode.On);
            // The brazier sits just below the light, so as a caster it would throw a dark disc metres wide around its own torch.
            var brazier = new MeshBuilder();
            brazier.Append(c.library.FlatBlock(rng), Matrix4x4.TRS(new Vector3(0f, (PostHeight - 0.1f) * s, 0f), Quaternion.Euler(0f, Random(rng), 0f), new Vector3(0.5f * s, 0.28f * s, 0.5f * s)), Iron);
            AddRenderer(root.transform, "Brazier", brazier.ToMesh("Torch Brazier"), c.material, ShadowCastingMode.Off);

            var flameBuilder = new MeshBuilder();
            flameBuilder.Append(Glowing(c.library.Crystal(rng), 255), Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, Random(rng), 0f), new Vector3(0.3f * s, 0.7f * s, 0.3f * s)), spec.flameColor);
            var flame = AddRenderer(root.transform, "Flame", flameBuilder.ToMesh("Torch Flame"), c.flameMaterial, ShadowCastingMode.Off);
            flame.transform.localPosition = new Vector3(0f, (PostHeight + 0.05f) * s, 0f);

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 0.22f * s;
            capsule.height = (PostHeight + 0.2f) * s;
            capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
            return flame.transform;
        }

        // A ring of stones and a stack of leaning logs. None of it casts shadows: it all sits around and below the light.
        static Transform BuildCampfire(Context c, System.Random rng, GameObject root, Spec spec)
        {
            var parts = new MeshBuilder();
            const int stones = 8;
            for (var i = 0; i < stones; i++)
            {
                var angle = (i + Range(rng, -0.2f, 0.2f)) / stones * Mathf.PI * 2f;
                var position = new Vector3(Mathf.Cos(angle) * 0.75f, -0.08f, Mathf.Sin(angle) * 0.75f);
                var size = Range(rng, 0.26f, 0.38f);
                parts.Append(c.library.Rock(rng), Matrix4x4.TRS(position, Quaternion.Euler(Range(rng, -15f, 15f), Random(rng), 0f), new Vector3(size, size * 0.8f, size)), Stone);
            }
            var yaw = Random(rng);
            for (var i = 0; i < 4; i++)
            {
                var rotation = Quaternion.Euler(0f, yaw + i * 90f + Range(rng, -15f, 15f), 0f) * Quaternion.Euler(28f, 0f, 0f);
                var position = rotation * new Vector3(0f, 0f, -0.32f) + new Vector3(0f, 0.05f, 0f);
                parts.Append(c.library.FlatBlock(rng), Matrix4x4.TRS(position, rotation * Quaternion.Euler(90f, 0f, 0f), new Vector3(0.13f, 0.75f, 0.13f)), Wood);
            }
            AddRenderer(root.transform, "Stones and Logs", parts.ToMesh("Campfire"), c.material, ShadowCastingMode.Off);

            var flameBuilder = new MeshBuilder();
            flameBuilder.Append(Glowing(c.library.Crystal(rng), 255), Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, Random(rng), 0f), new Vector3(0.45f, 0.85f, 0.45f)), spec.flameColor);
            for (var i = 0; i < 3; i++)
            {
                var angle = Random(rng) * Mathf.Deg2Rad;
                var position = new Vector3(Mathf.Cos(angle) * 0.18f, 0f, Mathf.Sin(angle) * 0.18f);
                flameBuilder.Append(Glowing(c.library.Crystal(rng), 255), Matrix4x4.TRS(position, Quaternion.Euler(Range(rng, -20f, 20f), Random(rng), Range(rng, -20f, 20f)), new Vector3(0.22f, Range(rng, 0.35f, 0.55f), 0.22f)), spec.flameColor);
            }
            var flame = AddRenderer(root.transform, "Flame", flameBuilder.ToMesh("Campfire Flame"), c.flameMaterial, ShadowCastingMode.Off);
            flame.transform.localPosition = new Vector3(0f, 0.08f, 0f);

            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(1.8f, 0.6f, 1.8f);
            box.center = new Vector3(0f, 0.3f, 0f);
            return flame.transform;
        }

        // A tall crystal on a stone plinth, ringed by smaller crystals. The light is inside it, so none of it casts shadows.
        static Transform BuildMonolith(Context c, System.Random rng, GameObject root, Spec spec)
        {
            var plinth = new MeshBuilder();
            plinth.Append(c.library.FlatBlock(rng), Matrix4x4.TRS(new Vector3(0f, -0.6f, 0f), Quaternion.Euler(0f, Random(rng), 0f), new Vector3(3.2f, 1.4f, 3.2f)), Stone);
            AddRenderer(root.transform, "Plinth", plinth.ToMesh("Monolith Plinth"), c.material, ShadowCastingMode.Off);

            var crystal = new MeshBuilder();
            crystal.Append(Glowing(c.library.Crystal(rng), 170), Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, Random(rng), 0f), new Vector3(1.3f, 9f, 1.3f)), spec.flameColor);
            for (var i = 0; i < 6; i++)
            {
                var angle = (i + Range(rng, -0.3f, 0.3f)) / 6f * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var lean = Quaternion.AngleAxis(Range(rng, 15f, 35f), Vector3.Cross(Vector3.up, outward));
                var height = Range(rng, 1.4f, 3.2f);
                crystal.Append(Glowing(c.library.Crystal(rng), 200), Matrix4x4.TRS(outward * Range(rng, 0.9f, 1.4f) + Vector3.down * 0.1f, lean * Quaternion.Euler(0f, Random(rng), 0f), new Vector3(0.5f, height, 0.5f)), spec.flameColor);
            }
            var obelisk = AddRenderer(root.transform, "Crystal", crystal.ToMesh("Monolith Crystal"), c.flameMaterial, ShadowCastingMode.Off);
            obelisk.transform.localPosition = new Vector3(0f, 0.75f, 0f);

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 1.6f;
            capsule.height = 9.5f;
            capsule.center = new Vector3(0f, 4.75f, 0f);
            return obelisk.transform;
        }

        // Large rocks beside the light, in the biome's terrain material, so the light has something to cast shadows from.
        static void AddShadowRocks(Context c, Transform parent, System.Random rng, Vector2 xz, Spec spec, int count)
        {
            var biome = c.shape.BiomeAt(xz.x, xz.y);

            for (var i = 0; i < count; i++)
            {
                var angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                var distance = Mathf.Lerp(2.4f, 3.8f, (float)rng.NextDouble()) * spec.rockDistance;
                var x = xz.x + Mathf.Cos(angle) * distance;
                var z = xz.y + Mathf.Sin(angle) * distance;
                var width = Mathf.Lerp(1.6f, 2.6f, (float)rng.NextDouble()) * spec.rockSize;
                var height = Mathf.Lerp(1.6f, 2.8f, (float)rng.NextDouble()) * spec.rockSize;
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

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        // The scatter shader glows by vertex alpha.
        static Tri[] Glowing(Tri[] template, byte amount)
        {
            var copy = (Tri[])template.Clone();
            for (var i = 0; i < copy.Length; i++)
                copy[i].emissive = amount;
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
        // Metres the light drifts from its rest position, so the shadows it casts move a little.
        public float swayAmount = 0.06f;
        // Flames flicker and change shape; monoliths pulse slowly and keep their shape.
        public bool animateFlame = true;
        public bool pulse;

        DayNightCycle cycle;
        Vector3 restPosition;

        void Start()
        {
            cycle = FindAnyObjectByType<DayNightCycle>();
            restPosition = torchLight.transform.localPosition;
        }

        void Update()
        {
            var slow = Mathf.PerlinNoise(Time.time * 2.3f + seed, seed);
            var fast = Mathf.PerlinNoise(Time.time * 9f + seed, seed + 31f);
            var flutter = pulse ? 0.85f + 0.15f * Mathf.Sin(Time.time * 1.1f + seed) : 0.8f + 0.3f * slow + 0.15f * fast;

            var daylight = cycle != null ? Mathf.Lerp(dayIntensity, 1f, cycle.NightFactor) : 1f;
            torchLight.intensity = baseIntensity * flutter * daylight;
            torchLight.range = baseRange * (0.94f + 0.06f * flutter);
            if (animateFlame && flame != null)
                flame.localScale = new Vector3(0.9f + 0.2f * fast, 0.8f + 0.5f * slow, 0.9f + 0.2f * (1f - fast));

            if (swayAmount > 0f)
            {
                var t = Time.time * 1.6f;
                var drift = new Vector3(
                    Mathf.PerlinNoise(t + seed, seed + 3f) - 0.5f,
                    (Mathf.PerlinNoise(t * 0.8f + seed, seed + 7f) - 0.5f) * 0.6f,
                    Mathf.PerlinNoise(t + seed + 13f, seed + 11f) - 0.5f);
                torchLight.transform.localPosition = restPosition + drift * (2f * swayAmount);
            }

            var cam = Camera.main;
            if (cam != null)
            {
                var near = (cam.transform.position - transform.position).sqrMagnitude < shadowDistance * shadowDistance;
                torchLight.shadows = near ? shadowType : LightShadows.None;
            }
        }
    }
}
