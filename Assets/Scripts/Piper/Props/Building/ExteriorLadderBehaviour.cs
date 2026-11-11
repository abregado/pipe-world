using PipeWorld.Landscape;
using UnityEngine;

namespace PipeWorld.Piper
{
    // A ladder up the outside of an outer wall, from the ground to a little above the roof. It is rebuilt to fit the drop to the ground and
    // the building's height each time the view is bound to a prop. Using it from the ground climbs to the roof above it, and from the roof
    // climbs back down.
    public unsafe class ExteriorLadderBehaviour : PropBehaviour, IInteractable
    {
        private const float Width = 0.5f;
        private const float StandOff = 0.14f;
        private const float AboveRoof = 1f;
        private static readonly Color32 Rail = new Color32(110, 80, 56, 0);
        private static readonly Color32 Rung = new Color32(150, 108, 72, 0);

        private Mesh mesh;

        public ExteriorLadderBehaviour() : base(PropPrototypeId.ExteriorLadder) { }

        public override void resetVisualisation(bool isNewlyPlacedByPlayer)
        {
            base.resetVisualisation(isNewlyPlacedByPlayer);

            Prop* p = prop;
            if (p == null)
                return;

            World world = PiperMain.p.game.world;
            float floorY = p->position.y;
            float drop = ExteriorDoorBehaviour.DropToGround(p->position, transform.forward, floorY, ExteriorDoorBehaviour.FallbackDrop(world, p->building, floorY));
            Rebuild(drop, BuildingTravel.BuildingHeight(world, p->building) + BuildingConstants.RoofThickness + AboveRoof);
        }

        // `below` is how far the ladder reaches under the floor level and `above` how far over it.
        private void Rebuild(float below, float above)
        {
            foreach (BoxCollider old in GetComponents<BoxCollider>())
                Destroy(old);
            if (mesh != null)
                Destroy(mesh);

            float total = below + above;
            float centreY = (above - below) * 0.5f;
            float z = BuildingConstants.WallThickness * 0.5f + StandOff;
            var b = new MeshBuilder();

            foreach (float x in new[] { -Width * 0.5f, Width * 0.5f })
                BuildingMeshes.AddBox(b, new Vector3(x, centreY, z), Vector3.right, new Vector3(0.06f, total, 0.06f), Rail);
            for (float y = -below + 0.3f; y < above - 0.05f; y += 0.3f)
                BuildingMeshes.AddBox(b, new Vector3(0f, y, z), Vector3.right, new Vector3(Width, 0.04f, 0.04f), Rung);
            // Brackets hold the rails off the wall.
            for (float y = -below + 0.6f; y < above - 0.05f; y += 1.5f)
                BuildingMeshes.AddBox(b, new Vector3(0f, y, z * 0.5f), Vector3.right, new Vector3(Width + 0.06f, 0.05f, z), Rail);

            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, centreY, z);
            box.size = new Vector3(Width + 0.1f, total, 0.14f);

            mesh = b.ToMesh("Exterior ladder");
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
        }

        public void GetPrimaryVerb(out string text, out bool actionEnabled, out string errorMessage)
        {
            Prop* p = prop;
            PiperGame game = PiperMain.p.game;
            BuildingModel m = p != null ? BuildingStore.load(game.world, p->building) : null;
            text = m != null && BuildingTravel.IsOnRoof(m, game.playerObject.transform.position) ? "Climb down" : "Climb to the roof";
            actionEnabled = true;
            errorMessage = "";
        }

        public void Interact(InteractionMode interactionMode)
        {
            Prop* p = prop;
            if (p != null)
                BuildingTravel.UseExteriorLadder(PiperMain.p.game, p->building, p->slabHeader._playerIndexAndGeneration);
        }

        public bool IsInteractable(InteractionMode interactionMode) => prop != null;

        public CrosshairStyle GetCrosshairStyle() => CrosshairStyle.Interact;
    }
}
