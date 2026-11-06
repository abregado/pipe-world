using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public class InteractionModeSelector : MonoBehaviour
    {
        public List<InteractionMode> allModes;
        public bool interactionActive = true;

        private int currentModeIndex = -1;

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

            PiperMain.p.game.hud.SetCrosshairStyle(CrosshairStyle.Default);
            allModes[currentModeIndex].ModeUpdate();
        }

        // Releases the active mode's prompts and highlight, e.g. while a menu covers the screen.
        public void Suspend()
        {
            if (currentModeIndex == -1)
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
