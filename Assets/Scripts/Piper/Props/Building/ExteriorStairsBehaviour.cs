using PipeWorld.Landscape;
using UnityEngine;

namespace PipeWorld.Piper
{
    // Steps from the floor level of an outer wall down to the ground. The drop depends on how far the building's floor is raised, so the
    // steps are built to fit each time the view is bound to a prop: they cover the whole drop at a gentle slope, and every step has a
    // box collider so the player can walk up them.
    public unsafe class ExteriorStairsBehaviour : PropBehaviour
    {
        public const float Width = 1.5f;
        private const float StepRun = 0.32f;
        private const float MaxRise = 0.22f;
        private const float MinDrop = 0.1f;

        private Mesh mesh;

        public ExteriorStairsBehaviour() : base(PropPrototypeId.ExteriorStairs) { }

        // The vertical distance from the floor down to the real ground in front of the wall. The ground is measured where the flight
        // will end (a second pass uses the length found by the first), ignoring buildings and props, so stairs on a slope reach the
        // terrain. `fallback` is used where nothing is hit, such as before the terrain's colliders exist.
        public static float DropToGround(Vector3 wallPoint, Vector3 outward, float floorY, float fallback)
        {
            outward.y = 0f;
            outward.Normalize();
            float drop = Mathf.Max(MinDrop, GroundDrop(wallPoint + outward * 0.4f, floorY, fallback));
            for (int pass = 0; pass < 2; pass++)
                drop = Mathf.Max(MinDrop, GroundDrop(wallPoint + outward * (0.08f + RunFor(drop)), floorY, fallback));
            return drop;
        }

        private static float GroundDrop(Vector3 point, float floorY, float fallback)
        {
            float best = float.MaxValue;
            float ground = float.NaN;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(point.x, floorY + 1f, point.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<BuildingView>() != null || hit.collider.GetComponentInParent<PropBehaviour>() != null)
                    continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    ground = hit.point.y;
                }
            }
            return float.IsNaN(ground) ? fallback : floorY - ground;
        }

        // The building's recorded ground level, for when the real ground cannot be measured.
        public static float FallbackDrop(World world, uint building, float floorY)
        {
            for (int i = 0; i < world.buildings.slabs.size; i++)
            {
                Building* b = world.buildings.slabs.get(i);
                if (b->slabHeader.allocated && b->slabHeader._playerIndexAndGeneration == building)
                    return Mathf.Max(MinDrop, floorY - b->groundY);
            }
            return 1f;
        }

        public static int StepCount(float drop) => Mathf.Max(1, Mathf.CeilToInt(drop / MaxRise));

        public static float RunFor(float drop) => StepCount(drop) * StepRun;

        public override void resetVisualisation(bool isNewlyPlacedByPlayer)
        {
            base.resetVisualisation(isNewlyPlacedByPlayer);

            Prop* p = prop;
            if (p == null)
                return;
            Vector3 wallPoint = p->position;
            float floorY = wallPoint.y;
            Rebuild(DropToGround(wallPoint, transform.forward, floorY, FallbackDrop(PiperMain.p.game.world, p->building, floorY)));
        }

        private void Rebuild(float drop)
        {
            foreach (BoxCollider old in GetComponents<BoxCollider>())
                Destroy(old);
            if (mesh != null)
                Destroy(mesh);

            int steps = StepCount(drop);
            float rise = drop / steps;
            var builder = new MeshBuilder();

            // The first step is level with the floor; each is a solid block from the ground up, so the whole flight is one slope of steps.
            for (int i = 0; i < steps; i++)
            {
                float top = -rise * i;
                float height = drop + top;
                Vector3 center = new Vector3(0f, top - height * 0.5f, 0.08f + StepRun * (i + 0.5f));
                BuildingMeshes.AddBox(builder, center, Vector3.right, new Vector3(Width, height, StepRun), i % 2 == 0 ? StepColor : StepColorDark);

                var box = gameObject.AddComponent<BoxCollider>();
                box.center = center;
                box.size = new Vector3(Width, height, StepRun);
            }

            mesh = builder.ToMesh("Exterior stairs");
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private static readonly Color32 StepColor = new Color32(150, 108, 72, 0);
        private static readonly Color32 StepColorDark = new Color32(120, 86, 58, 0);

        private void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
        }
    }
}
