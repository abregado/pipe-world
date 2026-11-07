using System.Collections.Generic;
using System.Linq;
using PipeWorld.Player;
using UnityEngine;

namespace PipeWorld.Piper
{
    // Edits a building's volumes while the player flies around it. Entered from the terminal; leaving returns the player to where they stood.
    // Targets are always on the building's floor plane, found from the crosshair ray.
    public class BuildingEditInteraction : InteractionMode
    {
        public enum Tool { Manipulate, Extrude, CreateTri, Slice, Delete, Heights, WallStyle }

        private static readonly string[] ToolNames = { "Manipulate", "Extrude", "Create Triangle", "Slice", "Delete Room", "Floor and Ceiling Height", "Wall Style" };
        private static readonly string[] ToolIcons = { "move", "expand", "triangle-target", "sword-slice", "trash-can", "elevator", "brick-wall" };
        private static readonly KeyCode[] ToolKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7 };

        public static string[] toolNames => ToolNames;
        public static string[] toolIcons => ToolIcons;

        private static readonly Color Idle = new Color(0.9f, 0.9f, 0.95f);
        private static readonly Color Marker = new Color(1f, 0.85f, 0.25f);
        private static readonly Color Hover = new Color(1f, 0.55f, 0.1f);
        private static readonly Color Good = new Color(0.4f, 1f, 0.5f);
        private static readonly Color Bad = new Color(1f, 0.4f, 0.35f);

        private enum MarkerKind { None, Edge, Vertex, Floor, Ceiling }

        private BuildingGizmos gizmos;
        private uint buildingId;
        private Vector3 returnPosition;
        private float returnYaw;
        private float returnPitch;

        // Entering pulls the camera back from the player, who is left standing as a stand-in avatar; leaving closes the eyes, puts the
        // player back and opens them again.
        private enum Phase { Entering, Active, Exiting }
        private const float EnterDuration = 1.4f;
        private const float EyeDuration = 0.6f;
        private const float PullBack = 7f;
        private const float PullUp = 3.5f;

        private Phase phase = Phase.Active;
        private float phaseTime;
        private Vector3 startEye, endEye;
        private Quaternion startRotation;
        private OtherPlayerBehaviour avatar;

        // The terminal the player used, and where they stood relative to it. Edits can move the terminal (a wall pulled, the floor
        // raised), and the stand-in and the place the player returns to move with it so they stay in front of it.
        private uint terminalId;
        private Vector3 terminalOffset;
        private Vector3 pullFlat = Vector3.forward;
        private Tool tool;

        private BuildingModel model;
        private Matrix4x4 toWorld;
        private Matrix4x4 toLocal;
        private float planeY;

        private bool dragging;
        private MarkerKind dragKind;
        private uint dragId;
        private Vector3 dragOrigin;
        private Vector3 dragAxis;
        private float dragOffset;
        private Vector2 dragPoint;

        private readonly ScrollStepper wheelStepper = new ScrollStepper();
        private string toast = "";
        private float toastUntil;

        public Tool currentTool => tool;
        public int toolIndex => (int)tool;

        public void SelectTool(int index)
        {
            SetTool((Tool)Mathf.Clamp(index, 0, ToolNames.Length - 1));
        }
        public uint editingBuildingId => buildingId;

        public override string GetUIID() => "building-edit";

        public override string GetEnglishName() => "Edit building: " + ToolNames[(int)tool];

        // Called by the terminal before the selector switches to this mode.
        public void Begin(uint building, Vector3 playerPosition, float playerYaw)
        {
            buildingId = building;
            returnPosition = playerPosition;
            returnYaw = playerYaw;
            tool = Tool.Manipulate;

            terminalId = 0;
            BuildingModel at = BuildingStore.load(PiperMain.p.game.world, building);
            BObject terminal = at?.objects.Find(o => o.proto == PropPrototypeId.Terminal);
            if (terminal != null)
            {
                terminalId = terminal.id;
                terminalOffset = playerPosition - TerminalWorld(at, terminal);
            }
        }

        public override void ActivateMode()
        {
            base.ActivateMode();
            gizmos ??= new BuildingGizmos("Building edit");
            gameHud.SetTooltipValues(false, 0f);
            dragging = false;

            PiperGame game = PiperMain.p.game;
            var player = game.playerObject;
            returnPitch = player.Pitch;
            player.SetFlying(true);
            game.buildingSyncer.editingBuilding = buildingId;

            // A stand-in where the player stood, and a camera that backs away from it keeping the feet centred.
            avatar = Instantiate(Resources.Load<OtherPlayerBehaviour>("OtherPlayer"), game.liveListObject.transform);
            avatar.name = "Player (editing)";
            avatar.SetTarget(returnPosition, new Vector3(returnPitch, returnYaw, 0f), false, "You");
            avatar.transform.rotation = Quaternion.Euler(0f, returnYaw, 0f);

            pullFlat = Quaternion.Euler(0f, returnYaw, 0f) * Vector3.forward;
            startEye = player.EyePosition;
            startRotation = player.EyeRotation;
            endEye = returnPosition - pullFlat * PullBack + Vector3.up * PullUp;
            phase = Phase.Entering;
            phaseTime = 0f;
            player.frameDriver = DriveEntering;
        }

        private void DriveEntering(float dt)
        {
            phaseTime += dt;
            float t = Mathf.Clamp01(phaseTime / EnterDuration);
            float s = t * t * (3f - 2f * t);
            Vector3 eye = Vector3.Lerp(startEye, endEye, s);
            Quaternion toFeet = Quaternion.LookRotation(returnPosition - eye);
            PiperMain.p.game.playerObject.SetEyePose(eye, Quaternion.Slerp(startRotation, toFeet, s));

            if (t >= 1f)
            {
                PiperMain.p.game.playerObject.frameDriver = null;
                phase = Phase.Active;
            }
        }

        private static Vector3 TerminalWorld(BuildingModel m, BObject terminal) =>
            m.position + Quaternion.Euler(0f, m.yaw, 0f) * m.objectLocalPosition(terminal);

        private void FollowTerminal()
        {
            BObject terminal = terminalId == 0 ? null : model.objectById(terminalId);
            if (terminal == null)
                return;

            Vector3 want = TerminalWorld(model, terminal) + terminalOffset;
            if ((want - returnPosition).sqrMagnitude < 1e-6f)
                return;

            returnPosition = want;
            avatar?.SetTarget(want, new Vector3(returnPitch, returnYaw, 0f), false, "You");
            endEye = returnPosition - pullFlat * PullBack + Vector3.up * PullUp;
        }

        // Closes the eyes, leaves the mode while they are shut, and opens them on the player's own view.
        private void RequestExit()
        {
            if (phase == Phase.Exiting)
                return;

            PiperGame game = PiperMain.p.game;
            phase = Phase.Exiting;
            dragging = false;
            gizmos?.Clear();
            game.playerObject.frameDriver = _ => { };
            gameHud.ChangeLeftClickPrompt("");
            gameHud.ChangeRightClickPrompt("");

            EyeCloseElement eyes = gameHud.eyes;
            eyes.Tween(1f, EyeDuration, EyeCloseElement.EaseInQuad, () =>
            {
                game.interactionModeSelector.SkipToMode(0);
                eyes.Tween(0f, EyeDuration, EyeCloseElement.EaseOutQuad);
            });
        }

        public override void DeactivateMode()
        {
            PiperGame game = PiperMain.p.game;
            gizmos?.Clear();
            dragging = false;
            if (avatar != null)
            {
                avatar.gameObject.SetActive(false);
                Destroy(avatar.gameObject);
                avatar = null;
            }
            phase = Phase.Active;
            if (game != null && game.playerObject != null)
            {
                game.playerObject.frameDriver = null;
                game.playerObject.SetFlying(false);
                game.playerObject.Teleport(returnPosition, returnYaw, returnPitch);
                game.buildingSyncer.editingBuilding = 0;
            }
            gameHud.ChangeScrollWheelPrompt("");
            base.DeactivateMode();
        }

        private void SetTool(Tool next)
        {
            tool = next;
            dragging = false;
            gameHud.ChangeModeText(GetEnglishName());
            PiperMain.p.game.interactionModeSelector.RefreshToolbar();
        }

        private void Reject(string reason)
        {
            toast = reason;
            toastUntil = Time.realtimeSinceStartup + 3f;
        }

        public override bool ModeUpdate()
        {
            PiperGame game = PiperMain.p.game;
            model = BuildingStore.load(game.world, buildingId, BuildingModel.trialIds());
            if (model == null)
            {
                game.interactionModeSelector.SkipToMode(0);
                return false;
            }

            FollowTerminal();

            // The camera is moving: only the footprint is drawn, and no input is taken until the player has the view back.
            if (phase != Phase.Active)
            {
                toWorld = BuildingTargeting.ToWorld(model);
                gizmos.Begin();
                DrawFootprint();
                gizmos.End();
                gameHud.ChangeLeftClickPrompt("");
                gameHud.ChangeRightClickPrompt("");
                return false;
            }

            if (InputFixed.GetKeyDown(KeyCode.Tab))
            {
                RequestExit();
                return false;
            }

            for (int i = 0; i < ToolKeys.Length; i++)
            {
                if (InputFixed.GetKeyDown(ToolKeys[i]) && i != (int)tool)
                    SetTool((Tool)i);
            }

            int steps = wheelStepper.Step(InputFixed.mouseScrollDelta.y);
            if (!dragging && steps != 0)
                SetTool((Tool)((((int)tool - steps) % ToolNames.Length + ToolNames.Length) % ToolNames.Length));

            // Right click with nothing in progress leaves the editor; while dragging it cancels the drag (handled by the tool).
            bool wasDragging = dragging;
            if (!wasDragging && InputFixed.GetMouseButtonDown(1))
            {
                RequestExit();
                return false;
            }

            toWorld = BuildingTargeting.ToWorld(model);
            toLocal = toWorld.inverse;
            planeY = model.position.y;
            Ray ray = BuildingTargeting.ViewRay(game);

            gizmos.Begin();
            DrawFootprint();

            string hint = "";
            switch (tool)
            {
                case Tool.Manipulate:
                case Tool.Extrude:
                case Tool.CreateTri:
                    hint = UpdateDragTool(game, ray);
                    break;
                case Tool.Slice:
                    hint = UpdateSlice(game, ray);
                    break;
                case Tool.Delete:
                    hint = UpdateDelete(game, ray);
                    break;
                case Tool.Heights:
                    hint = UpdateHeights(game, ray);
                    break;
                case Tool.WallStyle:
                    hint = UpdateWallStyle(game, ray);
                    break;
            }
            gizmos.End();

            bool toastActive = Time.realtimeSinceStartup < toastUntil;
            string error = toastActive ? toast : "";
            if (!toastActive && BuildingActions.lastRejection != null && Time.realtimeSinceStartup - BuildingActions.lastRejectionTime < 3f)
                error = BuildingActions.lastRejection;

            gameHud.ChangeLeftClickPrompt(hint, error == "", error);
            gameHud.ChangeRightClickPrompt(dragging ? "Cancel" : "Exit");
            gameHud.ChangeScrollWheelPrompt("Tool: " + ToolNames[(int)tool]);
            return false;
        }

        // ---- Drawing ----

        private Vector3 World(double x, double z, float lift = 0.03f) => toWorld.MultiplyPoint3x4(new Vector3((float)x, lift, (float)z));

        private void DrawFootprint()
        {
            foreach (BEdge e in model.edges)
            {
                BVertex a = model.vertex(e.a), b = model.vertex(e.b);
                gizmos.Line(World(a.x, a.z), World(b.x, b.z), Idle, 0.07f);
            }
        }

        private void DrawEdgeLine(BVertex a, BVertex b, Vector3 offsetWorld, Color color)
        {
            gizmos.Line(World(a.x, a.z, 0.06f) + offsetWorld, World(b.x, b.z, 0.06f) + offsetWorld, color, 0.09f);
        }

        private float HitRadius(Ray ray, Vector3 point)
        {
            BuildingTargeting.DistanceToRay(ray, point, out float along);
            return Mathf.Max(0.3f, 0.035f * along);
        }

        private bool Hit(Ray ray, Vector3 point)
        {
            float distance = BuildingTargeting.DistanceToRay(ray, point, out float along);
            return along > 0f && distance < HitRadius(ray, point);
        }

        // ---- Manipulate / Extrude / Create Tri ----

        private string UpdateDragTool(PiperGame game, Ray ray)
        {
            if (dragging)
                return UpdateDrag(game, ray);

            MarkerKind kind = MarkerKind.None;
            uint hoverId = 0;
            float best = float.MaxValue;

            void Consider(MarkerKind k, uint id, Vector3 p)
            {
                gizmos.Marker(p, Marker, 0.3f);
                BuildingTargeting.DistanceToRay(ray, p, out float along);
                float d = BuildingTargeting.DistanceToRay(ray, p, out _);
                if (along > 0f && d < HitRadius(ray, p) && d < best)
                {
                    best = d;
                    kind = k;
                    hoverId = id;
                }
            }

            if (tool == Tool.Manipulate)
            {
                foreach (BVertex v in model.triangleOnlyVertices())
                    Consider(MarkerKind.Vertex, v.id, World(v.x, v.z, 0.1f));
                foreach (BEdge e in model.edges)
                {
                    if (model.edgeBelongsToTriangle(e.id)) continue;
                    var m = model.edgeMidpoint(e);
                    Consider(MarkerKind.Edge, e.id, World(m.x, m.z, 0.1f));
                }
            }
            else
            {
                foreach (BEdge e in model.edges)
                {
                    if (model.areasForEdge(e.id).Count != 1) continue;
                    var m = model.edgeMidpoint(e);
                    Consider(MarkerKind.Edge, e.id, World(m.x, m.z, 0.1f));
                }
            }

            if (kind == MarkerKind.None)
                return tool == Tool.Manipulate ? "Aim at a marker" : "Aim at an outer wall marker";

            if (kind == MarkerKind.Edge) { BEdge e = model.edge(hoverId); var m = model.edgeMidpoint(e); gizmos.Marker(World(m.x, m.z, 0.1f), Hover, 0.42f); }
            else { BVertex v = model.vertex(hoverId); gizmos.Marker(World(v.x, v.z, 0.1f), Hover, 0.42f); }

            if (InputFixed.GetMouseButtonDown(0))
                BeginDrag(kind, hoverId);

            return tool == Tool.Manipulate ? (kind == MarkerKind.Vertex ? "Drag corner" : "Drag wall")
                : tool == Tool.Extrude ? "Drag to extrude" : "Drag to add a triangle";
        }

        private void BeginDrag(MarkerKind kind, uint id)
        {
            dragging = true;
            dragKind = kind;
            dragId = id;
            dragOffset = 0f;
            dragPoint = default;

            if (kind == MarkerKind.Edge)
            {
                BEdge e = model.edge(id);
                var m = model.edgeMidpoint(e);
                dragOrigin = World(m.x, m.z, 0f);

                var n = tool == Tool.Extrude ? model.edgeNormal(e, model.areasForEdge(id)[0]) : model.edgeNormal(e);
                dragAxis = toWorld.MultiplyVector(new Vector3((float)n.x, 0f, (float)n.z)).normalized;
            }
        }

        private string UpdateDrag(PiperGame game, Ray ray)
        {
            if (InputFixed.GetMouseButtonDown(1))
            {
                dragging = false;
                return "";
            }

            bool axisDrag = dragKind == MarkerKind.Edge && tool != Tool.CreateTri;
            string error;
            if (axisDrag)
            {
                dragOffset = BuildingTargeting.ClosestOnLine(ray, dragOrigin, dragAxis);
                if (tool == Tool.Extrude)
                    dragOffset = Mathf.Max(0f, dragOffset);
                dragOffset = BuildingTargeting.Round(dragOffset);
                error = Trial();
                DrawAxisPreview(error);
            }
            else
            {
                if (BuildingTargeting.RayPlane(ray, planeY, out Vector3 point))
                {
                    Vector3 local = toLocal.MultiplyPoint3x4(point);
                    dragPoint = new Vector2(BuildingTargeting.Round(local.x), BuildingTargeting.Round(local.z));
                }
                error = Trial();
                DrawPointPreview(error);
            }

            if (InputFixed.GetMouseButtonUp(0) || !InputFixed.GetMouseButton(0))
            {
                dragging = false;
                if (error != null)
                    Reject(error);
                else
                    Send(game);
                return "";
            }

            return error ?? "Release to apply";
        }

        // The same operation the action will run, on a copy. A null result means the edit is valid.
        private string Trial()
        {
            BuildingModel work = model.Clone();
            if (tool == Tool.Manipulate)
                return dragKind == MarkerKind.Edge ? work.moveEdge(dragId, dragOffset) : work.moveVertex(dragId, dragPoint.x, dragPoint.y);
            if (tool == Tool.Extrude)
                return work.extrude(dragId, dragOffset);
            return work.createTri(dragId, dragPoint.x, dragPoint.y);
        }

        private void Send(PiperGame game)
        {
            InputActionManager actions = game.inputActionManager;
            if (tool == Tool.Manipulate && dragKind == MarkerKind.Edge)
                actions.add(new MoveBuildingEdgeData { building = buildingId, edge = dragId, offset = dragOffset });
            else if (tool == Tool.Manipulate)
                actions.add(new MoveBuildingVertexData { building = buildingId, vertex = dragId, x = dragPoint.x, z = dragPoint.y });
            else if (tool == Tool.Extrude)
                actions.add(new ExtrudeBuildingEdgeData { building = buildingId, edge = dragId, offset = dragOffset });
            else
                actions.add(new CreateBuildingTriData { building = buildingId, edge = dragId, x = dragPoint.x, z = dragPoint.y });
        }

        private void DrawAxisPreview(string error)
        {
            Color color = error == null ? Good : Bad;
            Vector3 shift = dragAxis * dragOffset;
            BEdge dragged = model.edge(dragId);

            if (tool == Tool.Extrude)
            {
                BVertex a = model.vertex(dragged.a), b = model.vertex(dragged.b);
                Vector3 pa = World(a.x, a.z, 0.06f), pb = World(b.x, b.z, 0.06f);
                gizmos.Loop(new[] { pa, pb, pb + shift, pa + shift }, color, 0.09f);
                return;
            }

            foreach (uint eid in model.collinearChain(dragId))
            {
                BEdge e = model.edge(eid);
                DrawEdgeLine(model.vertex(e.a), model.vertex(e.b), shift, color);
            }
        }

        private void DrawPointPreview(string error)
        {
            Color color = error == null ? Good : Bad;
            Vector3 p = World(dragPoint.x, dragPoint.y, 0.06f);
            gizmos.Marker(p, color, 0.4f);

            if (dragKind == MarkerKind.Vertex)
            {
                foreach (BEdge e in model.edges.Where(e => e.a == dragId || e.b == dragId))
                {
                    BVertex other = model.vertex(e.a == dragId ? e.b : e.a);
                    gizmos.Line(World(other.x, other.z, 0.06f), p, color, 0.09f);
                }
                return;
            }

            BEdge edge = model.edge(dragId);
            BVertex va = model.vertex(edge.a), vb = model.vertex(edge.b);
            gizmos.Line(World(va.x, va.z, 0.06f), p, color, 0.09f);
            gizmos.Line(World(vb.x, vb.z, 0.06f), p, color, 0.09f);
        }

        // ---- Heights ----

        private Vector3 FootprintCentre()
        {
            float x = 0f, z = 0f;
            foreach (BVertex v in model.vertices) { x += v.x; z += v.z; }
            return new Vector3(x / model.vertices.Count, 0f, z / model.vertices.Count);
        }

        private float dragFloorY;
        private float dragHeight;

        private string UpdateHeights(PiperGame game, Ray ray)
        {
            Vector3 centre = FootprintCentre();
            Vector3 floorPoint = toWorld.MultiplyPoint3x4(centre);
            Vector3 ceilingPoint = toWorld.MultiplyPoint3x4(centre + Vector3.up * model.height);
            gizmos.Line(floorPoint, ceilingPoint, Idle, 0.05f);
            gizmos.Marker(floorPoint, Marker, 0.4f);
            gizmos.Marker(ceilingPoint, Marker, 0.4f);

            if (!dragging)
            {
                MarkerKind hover = MarkerKind.None;
                if (Hit(ray, ceilingPoint)) hover = MarkerKind.Ceiling;
                else if (Hit(ray, floorPoint)) hover = MarkerKind.Floor;

                if (hover == MarkerKind.None)
                    return "Aim at the floor or ceiling marker";

                gizmos.Marker(hover == MarkerKind.Floor ? floorPoint : ceilingPoint, Hover, 0.55f);
                if (InputFixed.GetMouseButtonDown(0))
                {
                    dragging = true;
                    dragKind = hover;
                    dragOrigin = hover == MarkerKind.Floor ? floorPoint : ceilingPoint;
                    dragFloorY = model.position.y;
                    dragHeight = model.height;
                }
                return hover == MarkerKind.Floor ? "Drag floor height" : "Drag ceiling height";
            }

            if (InputFixed.GetMouseButtonDown(1))
            {
                dragging = false;
                return "";
            }

            float offset = BuildingTargeting.ClosestOnLine(ray, dragOrigin, Vector3.up);
            offset = Mathf.Round(offset / 0.25f) * 0.25f;
            if (dragKind == MarkerKind.Floor)
            {
                dragFloorY = Mathf.Clamp(model.position.y + offset, model.groundY, model.groundY + BuildingConstants.MaxRaise);
                dragHeight = model.height;
            }
            else
            {
                dragFloorY = model.position.y;
                dragHeight = Mathf.Clamp(model.height + offset, BuildingConstants.MinHeight, BuildingConstants.MaxHeight);
            }

            string error = model.Clone().setHeights(dragFloorY, dragHeight);
            Color color = error == null ? Good : Bad;
            float lift = dragFloorY - model.position.y;
            foreach (BEdge e in model.edges)
            {
                BVertex a = model.vertex(e.a), b = model.vertex(e.b);
                Vector3 pa = World(a.x, a.z, lift), pb = World(b.x, b.z, lift);
                gizmos.Line(pa, pb, color, 0.07f);
                gizmos.Line(pa + Vector3.up * dragHeight, pb + Vector3.up * dragHeight, color, 0.07f);
            }
            gizmos.Line(floorPoint + Vector3.up * lift, floorPoint + Vector3.up * (lift + dragHeight), color, 0.05f);

            if (InputFixed.GetMouseButtonUp(0) || !InputFixed.GetMouseButton(0))
            {
                dragging = false;
                if (error != null)
                    Reject(error);
                else if (Mathf.Abs(dragFloorY - model.position.y) > 0.001f || Mathf.Abs(dragHeight - model.height) > 0.001f)
                    game.inputActionManager.add(new SetBuildingHeightsData { building = buildingId, floorY = dragFloorY, height = dragHeight });
                return "";
            }

            return error ?? (dragKind == MarkerKind.Floor ? "Floor " : "Ceiling ") + "release to apply  (floor +" + (dragFloorY - model.groundY).ToString("0.00") + " m, ceiling " + dragHeight.ToString("0.00") + " m)";
        }

        // ---- Wall style ----

        // A marker on every wall between two rooms, coloured by its style. Open walls have no geometry, so the marker is what is aimed at.
        private string UpdateWallStyle(PiperGame game, Ray ray)
        {
            BEdge hover = null;
            float best = float.MaxValue;
            foreach (BEdge e in model.edges)
            {
                if (!model.isEdgeInside(e.id))
                    continue;

                var m = model.edgeMidpoint(e);
                Vector3 p = World(m.x, m.z, 0.9f);
                Color color = e.type == WallStyle.Open ? new Color(0.65f, 0.65f, 0.7f) : e.type == WallStyle.Glass ? new Color(0.5f, 0.85f, 1f) : Marker;
                gizmos.Marker(p, color, 0.34f);

                float d = BuildingTargeting.DistanceToRay(ray, p, out float along);
                if (along > 0f && d < HitRadius(ray, p) && d < best)
                {
                    best = d;
                    hover = e;
                }
            }

            if (hover == null)
                return "Aim at a marker on a wall between two rooms";

            var mid = model.edgeMidpoint(hover);
            gizmos.Marker(World(mid.x, mid.z, 0.9f), Hover, 0.5f);
            BVertex a = model.vertex(hover.a), b = model.vertex(hover.b);
            gizmos.Line(World(a.x, a.z, 0.12f), World(b.x, b.z, 0.12f), Hover, 0.12f);

            byte next = WallStyle.next(hover.type);
            if (InputFixed.GetMouseButtonDown(0))
                game.inputActionManager.add(new SetBuildingWallStyleData { building = buildingId, edge = hover.id, style = next });
            return WallStyle.name(hover.type) + " wall: click for " + WallStyle.name(next).ToLower();
        }

        // ---- Slice ----

        private BArea AreaUnderCrosshair(Ray ray, out Vector3 local)
        {
            local = default;
            if (!BuildingTargeting.RayPlane(ray, planeY, out Vector3 point))
                return null;
            local = toLocal.MultiplyPoint3x4(point);
            return model.areaAt(local.x, local.z);
        }

        private string UpdateSlice(PiperGame game, Ray ray)
        {
            BArea area = AreaUnderCrosshair(ray, out Vector3 local);
            if (area == null)
                return "Aim at a room";

            SliceInfo info = model.computeSlice(area, local.x, local.z);
            if (info == null)
                return "Nowhere to slice here";

            BuildingModel trial = model.Clone();
            string error = trial.slice(area.id, local.x, local.z);
            Color color = error == null ? Good : Bad;
            // The cut may cross several rooms: a line through every crossing, with a marker on each wall it splits.
            for (int k = 0; k < info.points.Count; k++)
            {
                gizmos.Marker(World(info.points[k].x, info.points[k].z, 0.08f), color, 0.3f);
                if (k > 0)
                    gizmos.Line(World(info.points[k - 1].x, info.points[k - 1].z, 0.08f), World(info.points[k].x, info.points[k].z, 0.08f), color, 0.1f);
            }

            if (InputFixed.GetMouseButtonDown(0))
            {
                if (error != null)
                    Reject(error);
                else
                    game.inputActionManager.add(new SliceBuildingAreaData { building = buildingId, area = area.id, x = local.x, z = local.z });
            }
            return error ?? "Slice room";
        }

        // ---- Delete ----

        private string UpdateDelete(PiperGame game, Ray ray)
        {
            BArea area = AreaUnderCrosshair(ray, out _);
            if (area == null)
                return "Aim at a room";

            BuildingModel trial = model.Clone();
            string error = trial.deleteArea(area.id);
            Color color = error == null ? Bad : Idle;
            gizmos.Loop(model.areaVertices(area).Select(v => World(v.x, v.z, 0.1f)).ToList(), color, 0.12f);

            if (InputFixed.GetMouseButtonDown(0))
            {
                if (error != null)
                    Reject(error);
                else
                    game.inputActionManager.add(new DeleteBuildingAreaData { building = buildingId, area = area.id });
            }
            return error ?? "Delete room";
        }
    }
}
