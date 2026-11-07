using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public class InteractionModeSelector : MonoBehaviour
    {
        public List<InteractionMode> allModes;
        public bool interactionActive = true;

        // The modes the mouse wheel steps through, in toolbar order. The first slot is the default mode, which uses what is under the crosshair.
        private static readonly (string name, string icon)[] ToolbarItems =
        {
            ("Interact", "pointing"),
            ("Place building", "house"),
            ("Place props", "wooden-chair"),
            ("Move props", "move"),
            ("Delete props", "trash-can"),
        };

        private int currentModeIndex = -1;
        private readonly ScrollStepper wheelStepper = new ScrollStepper();

        public InteractionMode CurrentMode => currentModeIndex == -1 ? null : allModes[currentModeIndex];

        public void setupUi()
        {
            VisualElement hudRoot = FindFirstObjectByType<GameHud>().Root;

            foreach (InteractionMode mode in allModes)
                mode.SetupMode(hudRoot);
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
            if (current is not BuildingEditInteraction)
            {
                if (InputFixed.GetKeyDown(KeyCode.B) && current is not BuildingPlacerInteraction)
                {
                    SkipToMode(allModes.FindIndex(m => m is BuildingPlacerInteraction));
                    return;
                }
                if (InputFixed.GetKeyDown(KeyCode.O) && current is not ObjectPlacerInteraction)
                {
                    SkipToMode(allModes.FindIndex(m => m is ObjectPlacerInteraction));
                    return;
                }
            }

            // The number keys pick a toolbar slot in every mode but the editor (where they pick its tools).
            // The wheel steps through the toolbar in the default mode; the placement modes use the wheel themselves.
            if (current is not BuildingEditInteraction)
            {
                for (int i = 0; i < ToolbarItems.Length; i++)
                {
                    if (InputFixed.GetKeyDown(KeyCode.Alpha1 + i) && ToolbarSlot(current) != i)
                    {
                        SkipToMode(ModeForSlot(i));
                        return;
                    }
                }

                int steps = wheelStepper.Step(InputFixed.mouseScrollDelta.y);
                if (steps != 0 && ToolbarSlot(current) == 0)
                {
                    int slot = ((ToolbarSlot(current) - steps) % ToolbarItems.Length + ToolbarItems.Length) % ToolbarItems.Length;
                    SkipToMode(ModeForSlot(slot));
                    return;
                }
            }

            PiperMain.p.game.hud.SetCrosshairStyle(CrosshairStyle.Default);
            current.ModeUpdate();
        }

        private int ToolbarSlot(InteractionMode mode) => mode is BuildingPlacerInteraction ? 1 : mode is ObjectPlacerInteraction ? 2 : mode is PropMoveInteraction ? 3 : mode is PropDeleteInteraction ? 4 : 0;

        private int ModeForSlot(int slot) => slot == 1 ? allModes.FindIndex(m => m is BuildingPlacerInteraction)
            : slot == 2 ? allModes.FindIndex(m => m is ObjectPlacerInteraction)
            : slot == 3 ? allModes.FindIndex(m => m is PropMoveInteraction)
            : slot == 4 ? allModes.FindIndex(m => m is PropDeleteInteraction)
            : 0;

        // Shows the modes the wheel steps through, or the building tools while editing.
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
                hud.SetToolbar(ToolbarItems, ToolbarSlot(current));
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

        // The terminal's second button: furnishing on its own, without the flying editor.
        public void EnterFurnishing()
        {
            SkipToMode(allModes.FindIndex(m => m is ObjectPlacerInteraction));
        }

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
