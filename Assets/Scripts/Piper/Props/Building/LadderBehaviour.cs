using PipeWorld.Landscape;
using UnityEngine;

namespace PipeWorld.Piper
{
    // A ladder hanging from a room's ceiling with a hatch on the roof above it. It is one prop with two parts: using the ladder from
    // inside climbs to the roof, using the hatch from the roof comes back down. The ladder is rebuilt to reach the floor whatever the
    // building's ceiling height is.
    public unsafe class LadderBehaviour : PropBehaviour, IInteractable
    {
        private static readonly Color32 Wood = new Color32(150, 108, 72, 0);
        private static readonly Color32 DarkWood = new Color32(110, 80, 56, 0);
        private static readonly Color32 Metal = new Color32(70, 74, 82, 0);

        private Mesh mesh;

        public LadderBehaviour() : base(PropPrototypeId.Ladder) { }

        public override void resetVisualisation(bool isNewlyPlacedByPlayer)
        {
            base.resetVisualisation(isNewlyPlacedByPlayer);

            Prop* p = prop;
            if (p == null)
                return;

            World world = PiperMain.p.game.world;
            float height = 3f;
            for (int i = 0; i < world.buildings.slabs.size; i++)
            {
                Building* b = world.buildings.slabs.get(i);
                if (b->slabHeader.allocated && b->slabHeader._playerIndexAndGeneration == p->building)
                    height = b->height;
            }
            Rebuild(height);
        }

        private void Rebuild(float height)
        {
            foreach (BoxCollider old in GetComponents<BoxCollider>())
                Destroy(old);
            if (mesh != null)
                Destroy(mesh);

            var b = new MeshBuilder();

            // Below the ceiling: two rails and a rung every 0.3 m, down to the floor.
            foreach (float x in new[] { -0.2f, 0.2f })
                BuildingMeshes.AddBox(b, new Vector3(x, -height * 0.5f, 0f), Vector3.right, new Vector3(0.06f, height, 0.06f), DarkWood);
            for (float y = -0.3f; y > -height + 0.1f; y -= 0.3f)
                BuildingMeshes.AddBox(b, new Vector3(0f, y, 0f), Vector3.right, new Vector3(0.4f, 0.04f, 0.04f), Wood);

            // Above it, on the roof: a closed hatch with a frame and a ring to pull.
            float roof = BuildingConstants.RoofThickness;
            BuildingMeshes.AddBox(b, new Vector3(0f, roof + 0.04f, 0f), Vector3.right, new Vector3(1f, 0.08f, 1f), Metal);
            BuildingMeshes.AddBox(b, new Vector3(0f, roof + 0.1f, 0f), Vector3.right, new Vector3(0.84f, 0.04f, 0.84f), Wood);
            BuildingMeshes.AddBox(b, new Vector3(0f, roof + 0.14f, 0f), Vector3.right, new Vector3(0.2f, 0.04f, 0.06f), Metal);

            var ladder = gameObject.AddComponent<BoxCollider>();
            ladder.center = new Vector3(0f, -height * 0.5f, 0f);
            ladder.size = new Vector3(0.5f, height, 0.12f);
            var hatch = gameObject.AddComponent<BoxCollider>();
            hatch.center = new Vector3(0f, roof + 0.06f, 0f);
            hatch.size = new Vector3(1f, 0.12f, 1f);

            mesh = b.ToMesh("Ladder");
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
        }

        public void GetPrimaryVerb(out string text, out bool actionEnabled, out string errorMessage)
        {
            text = IsPlayerOnRoof() ? "Open hatch" : "Climb up";
            actionEnabled = true;
            errorMessage = "";
        }

        private bool IsPlayerOnRoof()
        {
            Prop* p = prop;
            if (p == null)
                return false;
            PiperGame game = PiperMain.p.game;
            BuildingModel m = BuildingStore.load(game.world, p->building);
            return m != null && BuildingTravel.IsOnRoof(m, game.playerObject.transform.position);
        }

        public void Interact(InteractionMode interactionMode)
        {
            Prop* p = prop;
            if (p != null)
                BuildingTravel.UseLadder(PiperMain.p.game, p->building, p->slabHeader._playerIndexAndGeneration);
        }

        public bool IsInteractable(InteractionMode interactionMode) => prop != null;

        public CrosshairStyle GetCrosshairStyle() => CrosshairStyle.Interact;
    }
}
