using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public class InteractionModeSelector : MonoBehaviour
    {
        public List<InteractionMode> allModes;
        public bool interactionActive = true;
        public FurniturePicker furniturePicker { get; private set; }

        // The hotbar, in order. Outside there are two tools; inside only the first, since a new building cannot be placed from a room.
        // The first slot is the default mode, which uses what is under the crosshair.
        private static readonly (string name, string icon)[] OutsideToolbar =
        {
            ("Interact", "pointing"),
            ("New building", "house"),
        };

        private static readonly (string name, string icon)[] InsideToolbar =
        {
            ("Interact", "pointing"),
        };

        private const int InsideCheckInterval = 10;

        private int currentModeIndex = -1;
        private bool inside;
        private int nextInsideCheck;
        private readonly ScrollStepper wheelStepper = new ScrollStepper();

        public InteractionMode CurrentMode => currentModeIndex == -1 ? null : allModes[currentModeIndex];

        private (string name, string icon)[] Toolbar => inside ? InsideToolbar : OutsideToolbar;

        public bool PlayerInside => inside;

        public void setupUi()
        {
            VisualElement hudRoot = FindFirstObjectByType<GameHud>().Root;

            foreach (InteractionMode mode in allModes)
                mode.SetupMode(hudRoot);

            ScreenUIHolder holder = FindFirstObjectByType<ScreenUIHolder>();
            furniturePicker = new FurniturePicker(holder.transform, holder.panelSettings, EnterPlaceFurniture);
        }

        public void doUpdate()
        {
            if (currentModeIndex == -1)
            {
                SkipToMode(0);
                return;
            }

            if (!interactionActive)
                return;

            InteractionMode current = allModes[currentModeIndex];
            if (UpdateInside(current))
                current = allModes[currentModeIndex];

            // The number keys pick a hotbar slot in every mode but the editor (where they pick its tools). The wheel steps through
            // the hotbar in the default mode; the other modes use the wheel themselves.
            if (current is not BuildingEditInteraction)
            {
                int slots = Toolbar.Length;
                if (!inside && InputFixed.GetKeyDown(KeyCode.B) && current is not BuildingPlacerInteraction)
                {
                    SkipToMode(ModeForSlot(1));
                    return;
                }

                for (int i = 0; i < slots; i++)
                {
                    if (InputFixed.GetKeyDown(KeyCode.Alpha1 + i) && ToolbarSlot(current) != i)
                    {
                        SkipToMode(ModeForSlot(i));
                        return;
                    }
                }

                int steps = wheelStepper.Step(InputFixed.mouseScrollDelta.y);
                if (steps != 0 && slots > 1 && ToolbarSlot(current) == 0)
                {
                    int slot = ((ToolbarSlot(current) - steps) % slots + slots) % slots;
                    SkipToMode(ModeForSlot(slot));
                    return;
                }
            }

            PiperMain.p.game.hud.SetCrosshairStyle(CrosshairStyle.Default);
            current.ModeUpdate();
        }

        // Re-checks whether the player is in a room. Going in changes the hotbar and ends the building placer. Returns true if the mode changed.
        private bool UpdateInside(InteractionMode current)
        {
            if (Time.frameCount < nextInsideCheck)
                return false;
            nextInsideCheck = Time.frameCount + InsideCheckInterval;

            bool now = BuildingTravel.IsLocalPlayerInsideAny(PiperMain.p.game);
            if (now == inside)
                return false;

            inside = now;
            if (inside && current is BuildingPlacerInteraction)
            {
                SkipToMode(0);
                return true;
            }

            RefreshToolbar();
            return false;
        }

        // The hotbar slot a mode occupies. Furniture modes are entered from a terminal and have no slot.
        private int ToolbarSlot(InteractionMode mode) => mode is BuildingPlacerInteraction ? 1
            : mode is ObjectPlacerInteraction || mode is PropMoveInteraction || mode is PropDeleteInteraction ? -1
            : 0;

        private int ModeForSlot(int slot) => slot == 1 ? allModes.FindIndex(m => m is BuildingPlacerInteraction) : 0;

        // Shows the hotbar, or the building tools while editing.
        public void RefreshToolbar()
        {
            GameHud hud = PiperMain.p.game?.hud;
            if (hud == null || currentModeIndex == -1)
                return;

            InteractionMode current = allModes[currentModeIndex];
            if (current is BuildingEditInteraction edit)
            {
                var tools = new List<(string name, string icon)>();
                for (int i = 0; i < BuildingEditInteraction.toolNames.Length; i++)
                    tools.Add((BuildingEditInteraction.toolNames[i], BuildingEditInteraction.toolIcons[i]));
                hud.SetToolbar(tools, edit.toolIndex);
            }
            else
                hud.SetToolbar(Toolbar, ToolbarSlot(current));
        }

        // Releases the active mode's prompts and highlight, e.g. while a menu covers the screen.
        // The edit mode stays active under a menu: leaving it would move the player.
        public void Suspend()
        {
            if (currentModeIndex == -1 || allModes[currentModeIndex] is BuildingEditInteraction)
                return;

            allModes[currentModeIndex].DeactivateMode();
            currentModeIndex = -1;
        }

        public void SkipToMode(int mode)
        {
            if (currentModeIndex != -1)
                allModes[currentModeIndex].DeactivateMode();

            allModes[mode].ActivateMode();
            currentModeIndex = mode % allModes.Count;
            RefreshToolbar();
        }

        public void SkipToMode(InteractionMode mode)
        {
            for (int i = 0; i < allModes.Count; i++)
            {
                if (allModes[i] == mode)
                {
                    SkipToMode(i);
                    return;
                }
            }
        }

        public void EnterBuildingEdit(uint buildingId)
        {
            int index = allModes.FindIndex(m => m is BuildingEditInteraction);
            Transform player = PiperMain.p.game.playerObject.transform;
            ((BuildingEditInteraction)allModes[index]).Begin(buildingId, player.position, player.eulerAngles.y);
            SkipToMode(index);
        }

        // The terminal's Place Furniture button: lists what can be placed from where the player stands.
        public void OpenFurniturePicker()
        {
            furniturePicker.Open(BuildingProps.placeableFor(BuildingTravel.IsLocalPlayerInsideAny(PiperMain.p.game)));
        }

        public void EnterPlaceFurniture(BuildingPropDef def)
        {
            int index = allModes.FindIndex(m => m is ObjectPlacerInteraction);
            ((ObjectPlacerInteraction)allModes[index]).Begin(def);
            SkipToMode(index);
        }

        public void EnterMoveFurniture() => SkipToMode(allModes.FindIndex(m => m is PropMoveInteraction));

        public void EnterDeleteFurniture() => SkipToMode(allModes.FindIndex(m => m is PropDeleteInteraction));

        public void LeaveBuildingEdit()
        {
            if (currentModeIndex != -1 && allModes[currentModeIndex] is BuildingEditInteraction)
                SkipToMode(0);
        }

        public void ResetMode()
        {
            if (currentModeIndex == -1)
                return;

            InteractionMode current = allModes[currentModeIndex];
            current.DeactivateMode();
            current.ActivateMode();
        }
    }
}
