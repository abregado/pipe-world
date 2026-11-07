using UnityEngine;

namespace PipeWorld.Piper
{
    // The terminal in a building: using it starts the building editor.
    public unsafe class TerminalBehaviour : PropBehaviour, IInteractable
    {
        public TerminalBehaviour() : base(PropPrototypeId.Terminal) { }

        public void GetPrimaryVerb(out string text, out bool actionEnabled, out string errorMessage)
        {
            text = "Edit building";
            actionEnabled = true;
            errorMessage = "";
        }

        public void Interact(InteractionMode interactionMode)
        {
            Prop* p = prop;
            if (p != null)
                PiperMain.p.game.interactionModeSelector.EnterBuildingEdit(p->building);
        }

        // Right click (a short hold) starts the prop placer without the flying editor.
        public float secondaryInteractionTime() => 0.2f;

        public string GetSecondaryVerb() => "Place furniture";

        public void InteractSecondary(InteractionMode interactionMode)
        {
            if (prop != null)
                PiperMain.p.game.interactionModeSelector.EnterFurnishing();
        }

        public bool IsInteractable(InteractionMode interactionMode) => prop != null;

        public CrosshairStyle GetCrosshairStyle() => CrosshairStyle.Interact;
    }
}
