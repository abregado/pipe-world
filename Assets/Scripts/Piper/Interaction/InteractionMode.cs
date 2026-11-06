using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public abstract class InteractionMode : MonoBehaviour
    {
        public float interactDistance = 5f;

        protected VisualElement uiRoot;
        protected GameHud gameHud;
        public bool cameFromSuperTool;

        protected GameObject _highlighted;

        public virtual string GetUIID() => "default-mode";

        public virtual string GetEnglishName() => "Default Interaction mode";

        public string englishModeName => GetEnglishName();

        public virtual void SetupMode(VisualElement root)
        {
            uiRoot = root;
            gameHud = FindFirstObjectByType<GameHud>();
            enabled = false;
        }

        public virtual void ActivateMode()
        {
            gameHud.ChangeModeText(englishModeName);
            enabled = true;
            gameHud.SetTooltipValues(false, interactDistance);
        }

        public virtual void DeactivateMode()
        {
            gameHud.ChangeModeText("");
            gameHud.ChangeLeftClickPrompt("");
            gameHud.ChangeRightClickPrompt("");
            gameHud.ClearCrosshairProgress();
            enabled = false;
            cameFromSuperTool = false;
        }

        public virtual void UpdateHighlight() { }

        public virtual bool ModeUpdate() => false;

        public virtual bool ObjectOfInterestUnderCursor() => false;

        // true = this mode fully owns tooltip display this frame (tooltips may legitimately be empty);
        // false = defer to the tag-request path against whatever is under the cursor.
        public virtual bool TryOverrideTooltips(out List<VisualElement> tooltips)
        {
            tooltips = null;
            return false;
        }

        public virtual TooltipRequestTags RequestedTooltipTags => TooltipRequestTags.Normal;

        protected void ChangeHighlight(GameObject newHighlight = null)
        {
            if (_highlighted != null)
            {
                _highlighted.GetComponent<IGetHighlighted>()?.HighlightOff();
                IInteractable interactable = _highlighted.GetComponent<IInteractable>();
                if (interactable != null && interactable.interactionTime() > 0f)
                    interactable.ResetVisuals();
            }

            _highlighted = newHighlight;
            if (_highlighted != null)
                _highlighted.GetComponent<IGetHighlighted>()?.HighlightOn();
        }
    }

    public interface IGetHighlighted
    {
        void HighlightOn();
        void HighlightOff();
        GameObject GetGameObject();
    }
}
