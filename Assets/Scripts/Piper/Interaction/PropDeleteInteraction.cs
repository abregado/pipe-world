using UnityEngine;

namespace PipeWorld.Piper
{
    // Deletes props. Aim at a prop and click: a building prop goes through DeleteBuildingObject (refused when the building needs it, like its
    // terminal or last exterior door), an outdoor prop through DeleteProp. Right click leaves the mode.
    public unsafe class PropDeleteInteraction : InteractionMode
    {
        private const float Reach = 8f;
        private static readonly Color Removable = new Color(1f, 0.4f, 0.35f);
        private static readonly Color Kept = new Color(0.7f, 0.7f, 0.72f);

        private BuildingGizmos gizmos;

        public override string GetUIID() => "prop-delete";

        public override string GetEnglishName() => "Delete props";

        public override void ActivateMode()
        {
            base.ActivateMode();
            gizmos ??= new BuildingGizmos("Prop deleter");
            gameHud.SetTooltipValues(false, 0f);
        }

        public override void DeactivateMode()
        {
            gizmos?.Clear();
            gameHud.ChangeScrollWheelPrompt("");
            base.DeactivateMode();
        }

        public override bool ModeUpdate()
        {
            PiperGame game = PiperMain.p.game;
            if (InputFixed.GetMouseButtonDown(1))
            {
                game.interactionModeSelector.SkipToMode(0);
                return false;
            }

            gizmos.Begin();
            gameHud.ChangeRightClickPrompt("Leave");
            gameHud.ChangeScrollWheelPrompt("");

            PropBehaviour hover = null;
            PlayerRaycaster raycaster = game.raycaster;
            if (raycaster.raycastHitsCount > 0 && raycaster.raycastHits[0].distance <= Reach)
                hover = raycaster.raycastHits[0].collider.GetComponentInParent<PropBehaviour>();

            Prop* prop = hover != null ? game.world.tryGet(hover.propHandle) : null;
            if (prop == null)
            {
                gameHud.ChangeLeftClickPrompt("Aim at a prop", false, "Aim at a prop.");
                gizmos.End();
                return false;
            }

            // A building prop is tried on a copy of its building first, so the reason it cannot go is shown before the click.
            string problem = null;
            uint building = prop->building;
            uint id = prop->slabHeader._playerIndexAndGeneration;
            if (building != 0)
            {
                BuildingModel model = BuildingStore.load(game.world, building);
                problem = model == null ? "That building no longer exists." : model.Clone().deleteObject(id);
            }

            Bounds bounds = BoundsOf(hover);
            gizmos.Box(bounds.center, Vector3.right, bounds.size, problem == null ? Removable : Kept);
            gameHud.ChangeLeftClickPrompt("Delete " + PropPrototypes.instance[prop->prototypeId].name, problem == null, problem ?? "");

            if (problem == null && InputFixed.GetMouseButtonDown(0))
            {
                if (building != 0)
                    game.inputActionManager.add(new DeleteBuildingObjectData { building = building, obj = id });
                else
                    game.inputActionManager.add(new DeletePropData { prop = new PropNetworkIdentifier(prop) });
            }

            gizmos.End();
            return false;
        }

        private static Bounds BoundsOf(PropBehaviour prop)
        {
            Renderer[] renderers = prop.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(prop.transform.position, Vector3.one * 0.5f);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
