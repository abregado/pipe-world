using UnityEngine;

namespace PipeWorld.Piper
{
    // Places new buildings in three clicks: the first sets the origin on the ground, the second the floor height above it, the third the
    // ceiling height. The mouse wheel turns the hologram throughout; right click goes back a step.
    public unsafe class BuildingPlacerInteraction : InteractionMode
    {
        private enum Stage { Origin, Floor, Height }

        private const float Reach = 30f;
        private const float RotateStep = 15f;
        private const float DegreesPerNotch = 30f;
        private readonly ScrollStepper rotateStepper = new ScrollStepper();
        private const float SnapHeight = 1f;
        private const float HeightStep = 0.25f;

        private static readonly Color Good = new Color(0.4f, 1f, 0.5f);
        private static readonly Color Bad = new Color(1f, 0.4f, 0.35f);
        private static readonly Color Faint = new Color(0.7f, 0.85f, 1f);

        private BuildingGizmos gizmos;
        private float yaw;
        private float rawYaw;
        private Stage stage;
        private Vector3 origin;
        private float groundY;
        private float floorY;
        private float height = BuildingConstants.CeilingHeight;

        public override string GetUIID() => "building-placer";

        public override string GetEnglishName() => "Place building";

        public override void ActivateMode()
        {
            base.ActivateMode();
            gizmos ??= new BuildingGizmos("Building placer");
            gameHud.SetTooltipValues(false, 0f);
            stage = Stage.Origin;
            height = BuildingConstants.CeilingHeight;
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

            if (InputFixed.GetKeyDown(KeyCode.B))
            {
                game.interactionModeSelector.SkipToMode(0);
                return false;
            }

            if (InputFixed.GetMouseButtonDown(1))
            {
                if (stage == Stage.Origin)
                {
                    game.interactionModeSelector.SkipToMode(0);
                    return false;
                }
                stage = stage - 1;
            }

            // The wheel turns the hologram smoothly; Shift snaps it to 15 degree steps measured from world X.
            bool snap = InputFixed.shift;
            float wheel = InputFixed.mouseScrollDelta.y;
            if (snap)
            {
                // Each scroll past the threshold is a whole 15 degree step.
                int steps = rotateStepper.Step(wheel);
                rawYaw = Mathf.Repeat(Mathf.Round(rawYaw / RotateStep) * RotateStep + steps * RotateStep, 360f);
                yaw = rawYaw;
            }
            else
            {
                rotateStepper.Reset();
                rawYaw = Mathf.Repeat(rawYaw + wheel * DegreesPerNotch, 360f);
                yaw = rawYaw;
            }

            gizmos.Begin();
            string problem = null;
            string leftLabel;
            Ray ray = BuildingTargeting.ViewRay(game);

            if (stage == Stage.Origin)
            {
                leftLabel = "Set position";
                problem = "Aim at the ground.";
                PlayerRaycaster raycaster = game.raycaster;
                if (raycaster.raycastHitsCount > 0 && raycaster.raycastHits[0].distance <= Reach)
                {
                    RaycastHit hit = raycaster.raycastHits[0];
                    Vector3 position = new Vector3(BuildingTargeting.Round(hit.point.x), BuildingTargeting.Round(hit.point.y), BuildingTargeting.Round(hit.point.z));
                    problem = hit.normal.y < 0.7f ? "Needs level ground."
                        : BuildingTargeting.ViewOf(hit.collider) != null ? "Not on a building."
                        : TooCloseToAnother(game.world, position) ? "Too close to another building."
                        : null;

                    DrawBuilding(position, position.y, BuildingConstants.CeilingHeight, problem == null ? Good : Bad);
                    if (problem == null && InputFixed.GetMouseButtonDown(0))
                    {
                        origin = position;
                        groundY = position.y;
                        floorY = groundY;
                        stage = Stage.Floor;
                    }
                }
            }
            else if (stage == Stage.Floor)
            {
                leftLabel = "Set floor height";
                float raise = BuildingTargeting.ClosestOnLine(ray, new Vector3(origin.x, groundY, origin.z), Vector3.up);
                // Shift snaps the floor to whole units of world Y rather than of the raise.
                floorY = snap ? Mathf.Round((groundY + raise) / SnapHeight) * SnapHeight : groundY + Mathf.Round(raise / HeightStep) * HeightStep;
                floorY = Mathf.Clamp(floorY, groundY, groundY + BuildingConstants.MaxRaise);
                DrawBuilding(origin, floorY, height, Good);
                gizmos.Line(new Vector3(origin.x, groundY, origin.z), new Vector3(origin.x, floorY, origin.z), Faint, 0.05f);

                if (InputFixed.GetMouseButtonDown(0))
                    stage = Stage.Height;
            }
            else
            {
                leftLabel = "Set ceiling height";
                float rise = BuildingTargeting.ClosestOnLine(ray, new Vector3(origin.x, floorY, origin.z), Vector3.up);
                height = Mathf.Clamp(Mathf.Round(rise / HeightStep) * HeightStep, BuildingConstants.MinHeight, BuildingConstants.MaxHeight);
                DrawBuilding(origin, floorY, height, Good);
                gizmos.Line(new Vector3(origin.x, groundY, origin.z), new Vector3(origin.x, floorY + height, origin.z), Faint, 0.05f);

                if (InputFixed.GetMouseButtonDown(0))
                {
                    game.inputActionManager.add(new PlaceBuildingData
                    {
                        position = new Vector3(origin.x, floorY, origin.z),
                        yaw = yaw,
                        height = height,
                        groundY = groundY,
                    });
                    game.interactionModeSelector.SkipToMode(0);
                    gizmos.End();
                    return false;
                }
            }
            gizmos.End();

            string detail = stage == Stage.Origin ? "" : stage == Stage.Floor ? "  floor +" + (floorY - groundY).ToString("0.00") + " m" : "  ceiling " + height.ToString("0.00") + " m";
            gameHud.ChangeLeftClickPrompt(leftLabel + detail, problem == null, problem ?? "");
            gameHud.ChangeRightClickPrompt(stage == Stage.Origin ? "Cancel" : "Back");
            gameHud.ChangeScrollWheelPrompt("Rotate  Shift: snap");
            return false;
        }

        // The building hologram: the 3x3 footprint at the chosen floor height, up to the ceiling, plus the scaffold under a raised floor.
        private void DrawBuilding(Vector3 position, float floor, float ceilingHeight, Color color)
        {
            float edge = BuildingConstants.StartHalfSize * 2f;
            Vector3 along = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            Vector3 center = new Vector3(position.x, floor + ceilingHeight * 0.5f, position.z);
            gizmos.Box(center, along, new Vector3(edge, ceilingHeight, edge), color);

            float raised = floor - groundYFor(position, floor);
            if (raised > 0.05f)
                gizmos.Box(new Vector3(position.x, floor - raised * 0.5f, position.z), along, new Vector3(edge, raised, edge), Faint);
        }

        private float groundYFor(Vector3 position, float floor) => stage == Stage.Origin ? floor : groundY;

        private static bool TooCloseToAnother(World world, Vector3 position)
        {
            for (int i = 0; i < world.buildings.slabs.size; i++)
            {
                Building* b = world.buildings.slabs.get(i);
                if (b->slabHeader.allocated && Vector3.Distance(b->position, position) < BuildingConstants.MinBuildingSpacing)
                    return true;
            }
            return false;
        }
    }
}
