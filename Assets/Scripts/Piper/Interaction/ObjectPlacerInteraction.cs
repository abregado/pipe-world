using System;
using UnityEngine;

namespace PipeWorld.Piper
{
    // Places props on the faces of buildings. The mouse wheel chooses the prop; right click removes the building prop under the crosshair.
    // Each prop knows which surfaces it goes on (tables and chairs on floors, lamps on ceilings, paintings on walls, doors and archways
    // on inner walls, exterior doors on outer walls) and the placement is checked on a copy of the building before it is sent.
    public unsafe class ObjectPlacerInteraction : InteractionMode
    {
        private const float Reach = 8f;
        private static readonly Color Good = new Color(0.5f, 0.9f, 1f);
        private static readonly Color Bad = new Color(1f, 0.4f, 0.35f);

        private BuildingGizmos gizmos;
        private int selected;
        private readonly ScrollStepper wheelStepper = new ScrollStepper();
        private BuildingPropDef[] choices = BuildingProps.placeable;
        private bool inside;
        private int nextInsideCheck;

        public BuildingPropDef selectedDef => choices[Mathf.Clamp(selected, 0, choices.Length - 1)];
        public bool playerInside => inside;

        public override string GetUIID() => "object-placer";

        public override string GetEnglishName() => "Place props";

        public override void ActivateMode()
        {
            base.ActivateMode();
            gizmos ??= new BuildingGizmos("Prop placer");
            gameHud.SetTooltipValues(false, 0f);
            RefreshChoices(true);
        }

        // From outdoors only the props that go on an outer wall are offered; from inside, all but the outdoor-only ones. The choice
        // stays on the same prop when the list changes under it.
        private void RefreshChoices(bool force)
        {
            if (!force && Time.frameCount < nextInsideCheck)
                return;
            nextInsideCheck = Time.frameCount + 15;

            bool nowInside = BuildingTravel.IsLocalPlayerInsideAny(PiperMain.p.game);
            if (!force && nowInside == inside)
                return;

            PropPrototypeId keep = choices.Length > 0 ? selectedDef.id : PropPrototypeId.None;
            inside = nowInside;
            choices = BuildingProps.placeableFor(inside);
            int index = System.Array.FindIndex(choices, d => d.id == keep);
            selected = index < 0 ? 0 : index;
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

            if (InputFixed.GetKeyDown(KeyCode.O))
            {
                game.interactionModeSelector.SkipToMode(0);
                return false;
            }

            if (InputFixed.GetMouseButtonDown(1))
            {
                game.interactionModeSelector.SkipToMode(0);
                return false;
            }

            RefreshChoices(false);
            int count = choices.Length;
            int steps = wheelStepper.Step(InputFixed.mouseScrollDelta.y);
            if (steps != 0)
                selected = ((selected - steps) % count + count) % count;

            BuildingPropDef def = selectedDef;
            gizmos.Begin();

            string problem = "Aim at a building.";
            string removeLabel = "";
            Prop* removeTarget = null;
            PlaceBuildingPropData placement = default;
            bool hasPlacement = false;

            PlayerRaycaster raycaster = game.raycaster;
            RaycastHit? surface = null;
            for (int i = 0; i < raycaster.raycastHitsCount; i++)
            {
                RaycastHit hit = raycaster.raycastHits[i];
                if (hit.distance > Reach)
                    break;

                // Props are not surfaces: the placement looks through them to the building behind. The first prop under the crosshair is the removal target.
                PropBehaviour prop = hit.collider.GetComponentInParent<PropBehaviour>();
                if (prop != null)
                {
                    if (i == 0)
                    {
                        Prop* p = game.world.tryGet(prop.propHandle);
                        if (p != null && p->building != 0)
                        {
                            removeTarget = p;
                            removeLabel = "Remove " + PropPrototypes.instance[p->prototypeId].name;
                        }
                    }
                    continue;
                }
                surface = hit;
                break;
            }

            if (surface.HasValue)
            {
                RaycastHit hit = surface.Value;
                BuildingView view = BuildingTargeting.ViewOf(hit.collider);
                if (view != null)
                {
                    BuildingModel model = BuildingStore.load(game.world, view.buildingId);
                    if (model != null)
                    {
                        Func<uint> ids = BuildingModel.trialIds();
                        model.newId = ids;
                        model.newPropId = ids;
                        problem = Target(model, def, hit, out placement);
                        hasPlacement = placement.building != 0;
                        if (hasPlacement)
                        {
                            BuildingModel trial = model.Clone();
                            string result = trial.placeProp(def.id, placement.face, placement.faceRef, placement.u, placement.v);
                            problem = problem ?? result;
                            DrawHologram(model, def, placement, problem == null ? Good : Bad);
                        }
                    }
                }
            }
            gizmos.End();

            gameHud.ChangeLeftClickPrompt("Place " + def.name, problem == null, problem ?? "");
            gameHud.ChangeRightClickPrompt("Leave");
            gameHud.ChangeScrollWheelPrompt((inside ? "Inside: " : "Outside: ") + def.name + (removeLabel == "" ? "" : "  X: " + removeLabel));

            if (hasPlacement && problem == null && InputFixed.GetMouseButtonDown(0))
                game.inputActionManager.add(placement);
            else if (removeTarget != null && InputFixed.GetKeyDown(KeyCode.X))
                game.inputActionManager.add(new DeleteBuildingObjectData { building = removeTarget->building, obj = removeTarget->slabHeader._playerIndexAndGeneration });

            return false;
        }

        // Finds the face and the 0-1 coordinates under the crosshair. A null result means the placement can be tried; otherwise it is the reason it cannot.
        internal static string Target(BuildingModel model, BuildingPropDef def, RaycastHit hit, out PlaceBuildingPropData placement)
        {
            placement = default;
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, model.yaw, 0f)) * (hit.point - model.position);
            Vector3 normal = Quaternion.Inverse(Quaternion.Euler(0f, model.yaw, 0f)) * hit.normal;

            if (def.surface == PropSurface.Wall)
            {
                if (Mathf.Abs(normal.y) > 0.7f || local.y < 0f)
                    return "Aim at a wall.";

                BEdge e = model.nearestEdge(local.x, local.z);
                if (e == null)
                    return "There is no wall here.";

                BVertex a = model.vertex(e.a), b = model.vertex(e.b);
                BuildingGeometry.closestPointOnSeg(local.x, local.z, a.x, a.z, b.x, b.z, out _, out _, out double t);
                placement = new PlaceBuildingPropData
                {
                    building = model.id,
                    proto = def.id,
                    face = model.sideOf(e, local.x, local.z),
                    faceRef = e.id,
                    u = (float)t,
                    v = BuildingProps.snapHeight(def, local.y, model.height),
                };
                return null;
            }

            bool floor = def.surface == PropSurface.Floor;
            if (floor ? !(normal.y > 0.7f && local.y < 0.3f) : !(normal.y < -0.7f && local.y > model.height - 0.3f))
                return floor ? "Aim at the floor." : "Aim at the ceiling.";

            float x = Mathf.Round(local.x * 10f) / 10f, z = Mathf.Round(local.z * 10f) / 10f;
            BArea area = model.areaAt(x, z);
            if (area == null)
                return "Place it inside a room.";

            var (u, v) = model.areaUv(area, x, z);
            placement = new PlaceBuildingPropData
            {
                building = model.id,
                proto = def.id,
                face = floor ? BuildingFace.Floor : BuildingFace.Ceiling,
                faceRef = area.id,
                u = u,
                v = v,
            };
            return null;
        }

        private void DrawHologram(BuildingModel model, BuildingPropDef def, PlaceBuildingPropData placement, Color color) =>
            DrawHologram(gizmos, model, def, placement.face, placement.faceRef, placement.u, placement.v, color);

        internal static void DrawHologram(BuildingGizmos gizmos, BuildingModel model, BuildingPropDef def, BuildingFace face, uint faceRef, float u, float v, Color color)
        {
            BObject o = model.makeProp(def, face, faceRef, u, v, 0);
            Matrix4x4 toWorld = BuildingTargeting.ToWorld(model);
            Vector3 position = model.objectLocalPosition(o);

            if (def.surface == PropSurface.Wall)
            {
                BEdge e = model.edge(o.faceRef);
                BVertex a = model.vertex(e.a), b = model.vertex(e.b);
                Vector3 dir = new Vector3(b.x - a.x, 0f, b.z - a.z).normalized;
                // The stairs reach the ground, so the hologram is the whole flight: from the floor level down to the ground, out from the wall.
                if (def.id == PropPrototypeId.ExteriorStairs)
                {
                    float drop = Mathf.Max(0.1f, model.position.y - model.groundY);
                    float run = ExteriorStairsBehaviour.RunFor(drop);
                    Vector3 stairCentre = position + model.sideNormal(e, o.face) * (BuildingConstants.WallThickness * 0.5f + run * 0.5f) + Vector3.down * (drop * 0.5f);
                    gizmos.Box(toWorld.MultiplyPoint3x4(stairCentre), toWorld.MultiplyVector(dir), new Vector3(def.width, drop, run), color);
                    return;
                }

                float thickness = def.teleports ? 0.44f : def.opening ? 0.24f : 0.2f;
                Vector3 centre = position + Vector3.up * (def.anchorBottom ? def.height * 0.5f : 0f);
                if (!def.opening && !def.teleports)
                    centre += model.sideNormal(e, o.face) * (BuildingConstants.WallThickness * 0.5f + 0.02f);
                gizmos.Box(toWorld.MultiplyPoint3x4(centre), toWorld.MultiplyVector(dir), new Vector3(def.width, def.height, thickness), color);
                return;
            }

            float sign = def.surface == PropSurface.Ceiling ? -1f : 1f;
            Vector3 c = position + Vector3.up * (sign * def.previewHeight * 0.5f);
            gizmos.Box(toWorld.MultiplyPoint3x4(c), toWorld.MultiplyVector(Vector3.right), new Vector3(def.radius * 2f, def.previewHeight, def.radius * 2f), color);
        }
    }
}
