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

        // The vertical distance from the floor to the ground at the building this prop belongs to.
        public static float DropFor(World world, uint building, float floorY)
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
            Rebuild(DropFor(PiperMain.p.game.world, p->building, p->position.y));
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
