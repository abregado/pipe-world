
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public class SuperTool: InteractionMode {

        public InteractionMode[] controlledModes;

        public InteractionMode lastHighlightMode;

        public override string GetUIID() {
            return "super-mode";
        }

        public override string GetEnglishName() {
            return "Super interaction mode";
        }

        public override bool ModeUpdate() {
            bool found = false;
            foreach (InteractionMode mode in controlledModes)
            {
                    if (!mode.ObjectOfInterestUnderCursor()) continue;
                found = true;
                if (lastHighlightMode!=null && lastHighlightMode != mode)
                {
                    lastHighlightMode.UpdateHighlight();
                }
                lastHighlightMode = mode;
                if (mode.ModeUpdate())
                {
                    mode.cameFromSuperTool = true;
                    FindFirstObjectByType<InteractionModeSelector>().SkipToMode(mode);
                    return true;
                };
            }

            if (!found){
                if (lastHighlightMode !=null)
                {
                    lastHighlightMode.UpdateHighlight();
                    lastHighlightMode = null;
                }
                gameHud.ChangeLeftClickPrompt("");
                gameHud.ChangeRightClickPrompt("");
                gameHud.ChangeScrollWheelPrompt("");
            }
            return false;

        }

        // Only the last mode to own the click prompts supplies tooltips, so an object never shows several modes' panels.
        public override bool TryOverrideTooltips(out List<VisualElement> tooltips) {
            if (lastHighlightMode != null) return lastHighlightMode.TryOverrideTooltips(out tooltips);
            tooltips = null;
            return false;
        }

        // Normal, not None, so basic prop info still shows when no mode claims the cursor.
        public override TooltipRequestTags RequestedTooltipTags =>
            lastHighlightMode?.RequestedTooltipTags ?? TooltipRequestTags.Normal;
    }
}
