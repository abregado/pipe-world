using PipeWorld.Landscape;
using PipeWorld.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace PipeWorld.Player
{
    // A small lantern held at the lower right of the view. It hangs from a hinge that follows the camera but always hangs
    // under gravity, and swings as a damped pendulum driven by the hand's acceleration and the walking pace.
    public class PlayerLantern : MonoBehaviour
    {
        // Hinge position in the camera pivot's space: right, down and forward of the eye.
        public Vector3 handOffset = new Vector3(0.36f, -0.16f, 0.72f);
        // Pendulum length in metres; 0.3 m swings about once a second.
        public float length = 0.3f;
        public float damping = 2.5f;
        public float maxSwingDegrees = 35f;
        // Extra swing per metre per second of walking speed, at the stride rate.
        public float strideSwing = 0.6f;
        public float strideRate = 7f;

        public Color lightColor = new Color(1f, 0.72f, 0.42f);
        public float intensity = 2.2f;
        public float range = 9f;
        // Share of the intensity kept by day, as for the torches.
        [Range(0f, 1f)] public float dayIntensity = 0.15f;

        static readonly Color32 Iron = new Color32(48, 46, 50, 0);
        static readonly Color32 Glow = new Color32(255, 190, 110, 0);

        Transform pivot;
        Transform hinge;
        Light lanternLight;
        DayNightCycle cycle;
        Vector3 lastPosition;
        Vector3 lastVelocity;
        bool hasHistory;
        // Swing about the hinge's x (forward and back) and z (side to side), in radians.
        Vector2 angle;
        Vector2 angularVelocity;
        float stridePhase;

        void Start()
        {
            pivot = GetComponent<PlayerController>().cameraPivot;
            cycle = FindAnyObjectByType<DayNightCycle>();
            var world = FindAnyObjectByType<DemoWorld>();
            if (pivot == null || world == null)
            {
                enabled = false;
                return;
            }

            hinge = new GameObject("Lantern Hinge").transform;
            hinge.SetParent(pivot, false);
            hinge.localPosition = handOffset;
            Build(world);
        }

        void Build(DemoWorld world)
        {
            var library = world.Library;
            var rng = new System.Random(7);

            var frame = new MeshBuilder();
            frame.Append(library.FlatBlock(rng), Matrix4x4.TRS(new Vector3(0f, -0.025f, 0f), Quaternion.identity, new Vector3(0.1f, 0.015f, 0.015f)), Iron);
            frame.Append(library.FlatBlock(rng), Matrix4x4.TRS(new Vector3(0f, -0.075f, 0f), Quaternion.identity, new Vector3(0.13f, 0.04f, 0.13f)), Iron);
            for (var i = 0; i < 4; i++)
            {
                var corner = new Vector3(i < 2 ? -0.05f : 0.05f, -0.205f, i % 2 == 0 ? -0.05f : 0.05f);
                frame.Append(library.FlatBlock(rng), Matrix4x4.TRS(corner, Quaternion.identity, new Vector3(0.012f, 0.135f, 0.012f)), Iron);
            }
            frame.Append(library.FlatBlock(rng), Matrix4x4.TRS(new Vector3(0f, -0.235f, 0f), Quaternion.identity, new Vector3(0.12f, 0.03f, 0.12f)), Iron);
            // The light is inside, so none of the lantern casts shadows.
            AddRenderer("Lantern Frame", frame.ToMesh("Lantern Frame"), world.PropMaterial);

            var glass = new MeshBuilder();
            var template = (Tri[])library.Crystal(rng).Clone();
            for (var i = 0; i < template.Length; i++)
                template[i].emissive = 255;
            glass.Append(template, Matrix4x4.TRS(new Vector3(0f, -0.2f, 0f), Quaternion.identity, new Vector3(0.07f, 0.12f, 0.07f)), Glow);
            AddRenderer("Lantern Flame", glass.ToMesh("Lantern Flame"), world.flameMaterial);

            var lightObject = new GameObject("Lantern Light");
            lightObject.transform.SetParent(hinge, false);
            lightObject.transform.localPosition = new Vector3(0f, -0.14f, 0f);
            lanternLight = lightObject.AddComponent<Light>();
            lanternLight.type = LightType.Point;
            lanternLight.color = lightColor;
            lanternLight.intensity = intensity;
            lanternLight.range = range;
            lanternLight.shadows = LightShadows.Soft;
            lanternLight.shadowStrength = 0.9f;

            var grade = lightObject.AddComponent<GradeLight>();
            grade.source = lanternLight;
            grade.tint = lightColor;
            grade.strength = 0.7f;
            grade.warmLut = 1f;
            grade.glow = 0.03f;
            grade.radiusScale = 1.2f;
        }

        void AddRenderer(string objectName, Mesh mesh, Material material)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(hinge, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        void LateUpdate()
        {
            var dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f)
                return;

            var position = hinge.position;
            var velocity = (position - lastPosition) / dt;
            // A teleport or the first frame would read as a huge acceleration.
            if (!hasHistory || (position - lastPosition).sqrMagnitude > 4f)
            {
                lastPosition = position;
                lastVelocity = Vector3.zero;
                hasHistory = true;
                return;
            }
            var acceleration = Vector3.ClampMagnitude((velocity - lastVelocity) / dt, 30f);
            lastPosition = position;
            lastVelocity = velocity;

            // Everything is in the player's yaw frame, so the lantern hangs straight down whatever the camera's pitch.
            var yawRotation = Quaternion.Euler(0f, pivot.eulerAngles.y, 0f);
            var local = Quaternion.Inverse(yawRotation) * acceleration;
            var speed = new Vector2(velocity.x, velocity.z).magnitude;
            stridePhase += dt * strideRate * Mathf.Clamp01(speed / 2f);
            var stride = Mathf.Sin(stridePhase) * strideSwing * Mathf.Min(speed, 9f);

            var stiffness = -Physics.gravity.y / length;
            // Accelerating forward swings the lantern back (+x); accelerating right swings it left (-z).
            var drive = new Vector2(local.z / length + stride, -local.x / length);
            var angularAcceleration = -stiffness * angle - damping * angularVelocity + drive;
            angularVelocity += angularAcceleration * dt;
            angle += angularVelocity * dt;
            var limit = maxSwingDegrees * Mathf.Deg2Rad;
            angle = new Vector2(Mathf.Clamp(angle.x, -limit, limit), Mathf.Clamp(angle.y, -limit, limit));

            hinge.rotation = yawRotation * Quaternion.Euler(angle.x * Mathf.Rad2Deg, 0f, angle.y * Mathf.Rad2Deg);

            var daylight = cycle != null ? Mathf.Lerp(dayIntensity, 1f, cycle.NightFactor) : 1f;
            var flicker = 0.92f + 0.08f * Mathf.PerlinNoise(Time.time * 6f, 3.7f);
            lanternLight.intensity = intensity * daylight * flicker;
        }
    }
}
