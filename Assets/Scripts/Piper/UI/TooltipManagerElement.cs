using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public class TooltipManagerElement : VisualElement
    {
        private TooltipProvider previousTooltipProvider;
        private bool builtForCurrentTarget;
        private List<VisualElement> previousOverrideTooltips;
        private InteractionMode previousMode;
        private readonly List<VisualElement> currentTooltips = new List<VisualElement>();

        public bool canTipThroughWalls = false;
        public float tipViewDistance = 5f;

        private InteractionModeSelector modeSelector;

        private ulong wasDirtyOnTick = 0;
        private ulong dirtyRefreshTime = 20;

        // Only gates the tag-request path (§6 of the tooltip refactor plan) - override-supplied
        // tooltips show immediately, since that content is core to what the mode is doing rather
        // than something the player is "discovering" by looking.
        private float dwellTimer = 0f;
        private const float tooltipDelaySeconds = 0.4f; // placeholder, tune in-editor

        public void MarkTooltipDirty()
        {
            wasDirtyOnTick = PiperMain.p.game.world.data.currentTick;
        }

        public TooltipManagerElement()
        {
            modeSelector = Object.FindFirstObjectByType<InteractionModeSelector>();
        }

        public void update()
        {
            InteractionMode currentMode = modeSelector.CurrentMode;

            if (currentMode != previousMode)
            {
                ClearAll();
                previousTooltipProvider = null;
                previousOverrideTooltips = null;
                builtForCurrentTarget = false;
                dwellTimer = 0f;
                previousMode = currentMode;
            }

            if (currentMode != null && currentMode.TryOverrideTooltips(out List<VisualElement> overrideTooltips))
            {
                overrideModeUpdate(overrideTooltips);
            }
            else
            {
                tagRequestUpdate(currentMode);
            }
        }

        private void overrideModeUpdate(List<VisualElement> tooltips)
        {
            previousTooltipProvider = null;
            builtForCurrentTarget = false;

            if (!ReferenceEquals(tooltips, previousOverrideTooltips))
            {
                ClearAll();
                foreach (var t in tooltips)
                {
                    Add(t);
                    currentTooltips.Add(t);
                }
                previousOverrideTooltips = tooltips;
            }

            foreach (var t in currentTooltips)
                t.visible = t.childCount != 0;
        }

        private void tagRequestUpdate(InteractionMode currentMode)
        {
            previousOverrideTooltips = null;

            TooltipProvider currentTooltipProvider = getTooltipProviderUnderCrosshair();
            TooltipRequestTags requestedTags = currentMode != null ? currentMode.RequestedTooltipTags : TooltipRequestTags.Normal;

            bool targetChanged = currentTooltipProvider != previousTooltipProvider;
            bool playerMoving = PiperMain.p.game.playerObject.IsWalking;

            if (targetChanged || playerMoving) dwellTimer = 0f;
            else dwellTimer += Time.deltaTime;

            if (targetChanged)
            {
                if (currentTooltips.Count > 0) ClearAll();
                builtForCurrentTarget = false;
            }

            bool shouldShow = currentTooltipProvider != null && dwellTimer >= tooltipDelaySeconds;

            if (shouldShow && !builtForCurrentTarget)
            {
                foreach (var t in currentTooltipProvider.createTooltips(requestedTags))
                {
                    Add(t);
                    currentTooltips.Add(t);
                }
                builtForCurrentTarget = true;
            }

            if (currentTooltips.Count > 0)
            {
                ulong currentTick = PiperMain.p.game.world.data.currentTick;
                if (wasDirtyOnTick > 0 && currentTick > wasDirtyOnTick + dirtyRefreshTime)
                {
                    wasDirtyOnTick = 0;
                    ClearAll();
                    foreach (var t in currentTooltipProvider.createTooltips(requestedTags))
                    {
                        Add(t);
                        currentTooltips.Add(t);
                    }
                }
                else
                {
                    currentTooltipProvider.updateTooltips(requestedTags);
                }

                foreach (var t in currentTooltips)
                    t.visible = t.childCount != 0;
            }

            previousTooltipProvider = currentTooltipProvider;
        }

        private void ClearAll()
        {
            currentTooltips.Clear();
            Clear();
        }

        private TooltipProvider getTooltipProviderUnderCrosshair()
        {
            PlayerRaycaster raycaster = PiperMain.p.game.raycaster;
            if (raycaster.raycastHitsCount == 0 || raycaster.raycastHits[0].distance > tipViewDistance)
                return null;

            return raycaster.raycastHits[0].collider.GetComponent<TooltipProvider>();
        }

        public void ClearTooltip()
        {
            ClearAll();
        }
    }
}
