using System;
using UnityEngine;

namespace PipeWorld.Piper
{
    // Places one chosen prop on the faces of buildings. The prop is chosen before the mode starts (from the terminal's furniture list);
    // the mouse wheel turns floor and ceiling props, and right click leaves. Each prop knows which surfaces it goes on (tables and chairs
    // on floors, lamps on ceilings, paintings on walls, doors and archways on inner walls, exterior doors on outer walls) and the
    // placement is checked on a copy of the building before it is sent.
    public unsafe class ObjectPlacerInteraction : InteractionMode
    {
        private const float Reach = 8f;
        public const float RotateStep = 15f;
        private static readonly Color Good = new Color(0.5f, 0.9f, 1f);
        private static readonly Color Bad = new Color(1f, 0.4f, 0.35f);

        private BuildingGizmos gizmos;
        private BuildingPropDef selectedDef;
        private float yaw;
        private readonly ScrollStepper rotateStepper = new ScrollStepper();

        public override string GetUIID() => "object-placer";

        public override string GetEnglishName() => selectedDef == null ? "Place furniture" : "Place " + selectedDef.name;

        // Chooses the prop to place; the next ActivateMode starts placing it.
        public void Begin(BuildingPropDef def)
        {
            selectedDef = def;
            yaw = 0f;
            rotateStepper.Reset();
        }

        public override void ActivateMode()
        {
            base.ActivateMode();
            gizmos ??= new BuildingGizmos("Prop placer");
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

            if (selectedDef == null || InputFixed.GetMouseButtonDown(1))
            {
                game.interactionModeSelector.SkipToMode(0);
                return false;
            }

            BuildingPropDef def = selectedDef;
            bool rotatable = def.surface != PropSurface.Wall;
            int steps = rotateStepper.Step(InputFixed.mouseScrollDelta.y);
            if (rotatable && steps != 0)
                yaw = Mathf.Repeat(yaw + steps * RotateStep, 360f);
            gizmos.Begin();

            string problem = "Aim at a building.";
            PlaceBuildingPropData placement = default;
            bool hasPlacement = false;

            PlayerRaycaster raycaster = game.raycaster;
            RaycastHit? surface = null;
            for (int i = 0; i < raycaster.raycastHitsCount; i++)
            {
                RaycastHit hit = raycaster.raycastHits[i];
                if (hit.distance > Reach)
                    break;

                // Props are not surfaces: the placement looks through them to the building behind.
                if (hit.collider.GetComponentInParent<PropBehaviour>() != null)
                    continue;
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
                        placement.yaw = yaw;
                        hasPlacement = placement.building != 0;
                        if (hasPlacement)
                        {
                            BuildingModel trial = model.Clone();
                            string result = trial.placeProp(def.id, placement.face, placement.faceRef, placement.u, placement.v, yaw);
                            problem = problem ?? result;
                            DrawHologram(model, def, placement, problem == null ? Good : Bad);
                        }
                    }
                }
            }
            gizmos.End();

            gameHud.ChangeLeftClickPrompt("Place " + def.name, problem == null, problem ?? "");
            gameHud.ChangeRightClickPrompt("Leave");
            gameHud.ChangeScrollWheelPrompt(rotatable ? "Rotate" : "");

            if (hasPlacement && problem == null && InputFixed.GetMouseButtonDown(0))
                game.inputActionManager.add(placement);

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
            DrawHologram(gizmos, model, def, placement.face, placement.faceRef, placement.u, placement.v, color, placement.yaw);

        internal static void DrawHologram(BuildingGizmos gizmos, BuildingModel model, BuildingPropDef def, BuildingFace face, uint faceRef, float u, float v, Color color, float yaw = 0f)
        {
            BObject o = model.makeProp(def, face, faceRef, u, v, 0, yaw);
            Matrix4x4 toWorld = BuildingTargeting.ToWorld(model);
            Vector3 position = model.objectLocalPosition(o);

            if (def.surface == PropSurface.Wall)
            {
                BEdge e = model.edge(o.faceRef);
                BVertex a = model.vertex(e.a), b = model.vertex(e.b);
                Vector3 dir = new Vector3(b.x - a.x, 0f, b.z - a.z).normalized;
                float thickness = def.depth > 0f ? def.depth : def.opening ? 0.24f : 0.2f;
                Vector3 centre = position + Vector3.up * (def.anchorBottom ? def.height * 0.5f : 0f);
                if (!def.opening && def.depth <= 0f)
                    centre += model.sideNormal(e, o.face) * (BuildingConstants.WallThickness * 0.5f + 0.02f);
                gizmos.Box(toWorld.MultiplyPoint3x4(centre), toWorld.MultiplyVector(dir), new Vector3(def.width, def.height, thickness), color);
                return;
            }

            float sign = def.surface == PropSurface.Ceiling ? -1f : 1f;
            Vector3 c = position + Vector3.up * (sign * def.previewHeight * 0.5f);
            Quaternion turn = Quaternion.Euler(0f, o.yaw, 0f);
            gizmos.Box(toWorld.MultiplyPoint3x4(c), toWorld.MultiplyVector(turn * Vector3.right), new Vector3(def.radius * 2f, def.previewHeight, def.radius * 2f), color);
            // The line from the middle to the front shows which way the prop faces.
            Vector3 front = turn * Vector3.forward * def.radius;
            gizmos.Line(toWorld.MultiplyPoint3x4(c), toWorld.MultiplyPoint3x4(c + front), color, 0.05f);
        }
    }
}
