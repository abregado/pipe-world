using UnityEngine;

namespace PipeWorld.Piper
{
    // Places new buildings, which are always 4x4x4 m, in two clicks: the first sets the origin on the ground, the second the floor height
    // above it. The hologram marks the side that will have the door. The mouse wheel turns the hologram throughout; right click goes back a step.
    public unsafe class BuildingPlacerInteraction : InteractionMode
    {
        private enum Stage { Origin, Floor }

        private const float Reach = 30f;
        private const float RotateStep = 15f;
        private const float DegreesPerNotch = 5f;
        private readonly ScrollStepper rotateStepper = new ScrollStepper();
        private const float SnapHeight = 1f;
        private const float HeightStep = 0.25f;
        private const float DoorWidth = 1.5f;
        private const float DoorHeight = 2.1f;

        private static readonly Color Good = new Color(0.4f, 1f, 0.5f);
        private static readonly Color Bad = new Color(1f, 0.4f, 0.35f);
        private static readonly Color Faint = new Color(0.7f, 0.85f, 1f);
        private static readonly Color DoorColor = new Color(1f, 0.75f, 0.2f);

        private BuildingGizmos gizmos;
        private float yaw;
        private float rawYaw;
        private Stage stage;
        private Vector3 origin;
        private float groundY;
        private float floorY;

        public override string GetUIID() => "building-placer";

        public override string GetEnglishName() => "Place building";

        public override void ActivateMode()
        {
            base.ActivateMode();
            gizmos ??= new BuildingGizmos("Building placer");
            gameHud.SetTooltipValues(false, 0f);
            stage = Stage.Origin;
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

                    DrawBuilding(position, position.y, problem == null ? Good : Bad);
                    if (problem == null && InputFixed.GetMouseButtonDown(0))
                    {
                        origin = position;
                        groundY = position.y;
                        floorY = groundY;
                        stage = Stage.Floor;
                    }
                }
            }
            else
            {
                leftLabel = "Place building";
                float raise = BuildingTargeting.ClosestOnLine(ray, new Vector3(origin.x, groundY, origin.z), Vector3.up);
                // Shift snaps the floor to whole units of world Y rather than of the raise.
                floorY = snap ? Mathf.Round((groundY + raise) / SnapHeight) * SnapHeight : groundY + Mathf.Round(raise / HeightStep) * HeightStep;
                floorY = Mathf.Clamp(floorY, groundY, groundY + BuildingConstants.MaxRaise);
                DrawBuilding(origin, floorY, Good);
                gizmos.Line(new Vector3(origin.x, groundY, origin.z), new Vector3(origin.x, floorY, origin.z), Faint, 0.05f);

                if (InputFixed.GetMouseButtonDown(0))
                {
                    game.inputActionManager.add(new PlaceBuildingData
                    {
                        position = new Vector3(origin.x, floorY, origin.z),
                        yaw = yaw,
                        height = BuildingConstants.CeilingHeight,
                        groundY = groundY,
                    });
                    game.interactionModeSelector.SkipToMode(0);
                    gizmos.End();
                    return false;
                }
            }
            gizmos.End();

            string detail = stage == Stage.Origin ? "" : "  floor +" + (floorY - groundY).ToString("0.00") + " m";
            gameHud.ChangeLeftClickPrompt(leftLabel + detail, problem == null, problem ?? "");
            gameHud.ChangeRightClickPrompt(stage == Stage.Origin ? "Cancel" : "Back");
            gameHud.ChangeScrollWheelPrompt("Rotate  Shift: snap");
            return false;
        }

        // The building hologram: the 4x4x4 footprint at the chosen floor height, the scaffold under a raised floor, and the door on the
        // south side (the one that faces local -Z), with an arrow out from it.
        private void DrawBuilding(Vector3 position, float floor, Color color)
        {
            float edge = BuildingConstants.StartHalfSize * 2f;
            float ceilingHeight = BuildingConstants.CeilingHeight;
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 along = turn * Vector3.right;
            Vector3 center = new Vector3(position.x, floor + ceilingHeight * 0.5f, position.z);
            gizmos.Box(center, along, new Vector3(edge, ceilingHeight, edge), color);

            float raised = floor - groundYFor(position, floor);
            if (raised > 0.05f)
                gizmos.Box(new Vector3(position.x, floor - raised * 0.5f, position.z), along, new Vector3(edge, raised, edge), Faint);

            Vector3 doorBase = new Vector3(position.x, floor, position.z) + turn * Vector3.back * BuildingConstants.StartHalfSize;
            Vector3 outward = turn * Vector3.back;
            gizmos.Box(doorBase + Vector3.up * (DoorHeight * 0.5f), along, new Vector3(DoorWidth, DoorHeight, 0.3f), DoorColor);
            gizmos.Line(doorBase + Vector3.up * 0.1f, doorBase + Vector3.up * 0.1f + outward * 1.5f, DoorColor, 0.07f);
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
