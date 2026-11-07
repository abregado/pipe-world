using System;
using UnityEngine;

namespace PipeWorld.Piper
{
    // Moves props. The first click selects the prop under the crosshair; nothing moves until a second click chooses where it goes.
    // Right click with a prop selected goes back to choosing a prop, and right click with none leaves the mode.
    // A building prop moves along the faces of its own building and must satisfy the same rules as when it was placed; an outdoor prop
    // moves to the ground or floor under the crosshair.
    public unsafe class PropMoveInteraction : InteractionMode
    {
        private enum Stage { Select, Place }

        private const float Reach = 8f;
        private static readonly Color Chosen = new Color(1f, 0.85f, 0.25f);
        private static readonly Color Hover = new Color(1f, 0.55f, 0.1f);
        private static readonly Color Good = new Color(0.5f, 0.9f, 1f);
        private static readonly Color Bad = new Color(1f, 0.4f, 0.35f);

        private BuildingGizmos gizmos;
        private Stage stage;

        // The selected prop. A building prop is found again by its building and id, an outdoor one by its prototype and position.
        private uint selectedBuilding;
        private uint selectedObject;
        private PropNetworkIdentifier selectedOutdoor;
        private PropPrototypeId selectedProto;
        private Vector3 boundsOffset;
        private Vector3 boundsSize;

        public override string GetUIID() => "prop-move";

        public override string GetEnglishName() => "Move props";

        public override void ActivateMode()
        {
            base.ActivateMode();
            gizmos ??= new BuildingGizmos("Prop mover");
            gameHud.SetTooltipValues(false, 0f);
            stage = Stage.Select;
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
            if (stage == Stage.Select && InputFixed.GetMouseButtonDown(1))
            {
                game.interactionModeSelector.SkipToMode(0);
                return false;
            }

            gizmos.Begin();
            if (stage == Stage.Select)
                UpdateSelect(game);
            else
                UpdatePlace(game);
            gizmos.End();
            gameHud.ChangeScrollWheelPrompt("");
            return false;
        }

        // ---- Select ----

        private void UpdateSelect(PiperGame game)
        {
            PropBehaviour hover = null;
            PlayerRaycaster raycaster = game.raycaster;
            if (raycaster.raycastHitsCount > 0 && raycaster.raycastHits[0].distance <= Reach)
                hover = raycaster.raycastHits[0].collider.GetComponentInParent<PropBehaviour>();

            Prop* prop = hover != null ? game.world.tryGet(hover.propHandle) : null;
            if (prop == null)
            {
                gameHud.ChangeLeftClickPrompt("Aim at a prop", false, "Aim at a prop.");
                gameHud.ChangeRightClickPrompt("Leave");
                return;
            }

            Bounds bounds = BoundsOf(hover);
            gizmos.Box(bounds.center, Vector3.right, bounds.size, Hover);
            gameHud.ChangeLeftClickPrompt("Move " + PropPrototypes.instance[prop->prototypeId].name);
            gameHud.ChangeRightClickPrompt("Leave");

            if (InputFixed.GetMouseButtonDown(0))
            {
                selectedProto = prop->prototypeId;
                selectedBuilding = prop->building;
                selectedObject = prop->slabHeader._playerIndexAndGeneration;
                selectedOutdoor = new PropNetworkIdentifier(prop);
                boundsOffset = bounds.center - (Vector3)prop->position;
                boundsSize = bounds.size;
                stage = Stage.Place;
            }
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

        // ---- Place ----

        private void UpdatePlace(PiperGame game)
        {
            if (InputFixed.GetMouseButtonDown(1))
            {
                stage = Stage.Select;
                return;
            }

            gameHud.ChangeRightClickPrompt("Back");

            // The first surface under the crosshair that is not a prop.
            PlayerRaycaster raycaster = game.raycaster;
            RaycastHit? surface = null;
            for (int i = 0; i < raycaster.raycastHitsCount; i++)
            {
                RaycastHit hit = raycaster.raycastHits[i];
                if (hit.distance > Reach)
                    break;
                if (hit.collider.GetComponentInParent<PropBehaviour>() != null)
                    continue;
                surface = hit;
                break;
            }

            if (selectedBuilding != 0)
                { PlaceOnBuilding(game, surface); return; }
            PlaceOutdoors(game, surface);
        }

        private void PlaceOnBuilding(PiperGame game, RaycastHit? surface)
        {
            BuildingModel model = BuildingStore.load(game.world, selectedBuilding);
            BObject current = model?.objectById(selectedObject);
            if (current == null)
            {
                stage = Stage.Select;
                return;
            }

            // The prop being moved stays marked where it is.
            ObjectPlacerInteraction.DrawHologram(gizmos, model, current.def, current.face, current.faceRef, current.u, current.v, Chosen);

            string problem = "Aim at a surface of this building.";
            if (surface.HasValue)
            {
                RaycastHit hit = surface.Value;
                BuildingView view = BuildingTargeting.ViewOf(hit.collider);
                if (view == null || view.buildingId != selectedBuilding)
                    problem = "Move it within the same building.";
                else
                {
                    Func<uint> ids = BuildingModel.trialIds();
                    model.newId = ids;
                    model.newPropId = ids;
                    problem = ObjectPlacerInteraction.Target(model, current.def, hit, out PlaceBuildingPropData placement);
                    if (placement.building != 0)
                    {
                        problem = problem ?? model.Clone().moveProp(selectedObject, placement.face, placement.faceRef, placement.u, placement.v);
                        ObjectPlacerInteraction.DrawHologram(gizmos, model, current.def, placement.face, placement.faceRef, placement.u, placement.v, problem == null ? Good : Bad);

                        if (problem == null && InputFixed.GetMouseButtonDown(0))
                        {
                            game.inputActionManager.add(new MoveBuildingPropData
                            {
                                building = selectedBuilding,
                                obj = selectedObject,
                                face = placement.face,
                                faceRef = placement.faceRef,
                                u = placement.u,
                                v = placement.v,
                            });
                            stage = Stage.Select;
                        }
                    }
                }
            }

            gameHud.ChangeLeftClickPrompt("Move here", problem == null, problem ?? "");
        }

        private void PlaceOutdoors(PiperGame game, RaycastHit? surface)
        {
            Prop* prop = selectedOutdoor.tryGet(game.world);
            if (prop == null)
            {
                stage = Stage.Select;
                return;
            }

            gizmos.Box((Vector3)prop->position + boundsOffset, Vector3.right, boundsSize, Chosen);

            string problem = "Aim at the ground.";
            if (surface.HasValue)
            {
                RaycastHit hit = surface.Value;
                problem = hit.normal.y < 0.5f ? "Needs level ground." : null;
                Vector3 position = hit.point;
                gizmos.Box(position + boundsOffset, Vector3.right, boundsSize, problem == null ? Good : Bad);

                if (problem == null && InputFixed.GetMouseButtonDown(0))
                {
                    game.inputActionManager.add(new MovePropData { prop = selectedOutdoor, newPosition = position });
                    stage = Stage.Select;
                }
            }

            gameHud.ChangeLeftClickPrompt("Move here", problem == null, problem ?? "");
            return;
        }
    }
}
