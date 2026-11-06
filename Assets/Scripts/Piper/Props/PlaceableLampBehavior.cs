using UnityEngine;

namespace PipeWorld.Piper
{
    public unsafe class PlaceableLampBehavior : PropBehaviour, IInteractable
    {
        public Light lampLight;
        public GameObject bulbOn;
        public GameObject bulbOff;

        public PlaceableLampBehavior() : base(PropPrototypeId.PlaceableLamp) { }

        public override void resetVisualisation(bool isNewlyPlacedByPlayer)
        {
            base.resetVisualisation(isNewlyPlacedByPlayer);
            ApplyState();
        }

        public override void updateVisualisation() => ApplyState();

        private void ApplyState()
        {
            Prop* p = prop;
            bool on = p != null && p->data.lamp.on;
            lampLight.enabled = on;
            bulbOn.SetActive(on);
            bulbOff.SetActive(!on);
        }

        public void GetPrimaryVerb(out string text, out bool actionEnabled, out string errorMessage)
        {
            Prop* p = prop;
            text = p != null && p->data.lamp.on ? "Switch off" : "Switch on";
            actionEnabled = true;
            errorMessage = "";
        }

        public void Interact(InteractionMode interactionMode)
        {
            PiperMain.p.game.inputActionManager.add(new ToggleOpenableData() { prop = new PropNetworkIdentifier(this) });
        }

        public bool IsInteractable(InteractionMode interactionMode) => prop != null;
    }
}
