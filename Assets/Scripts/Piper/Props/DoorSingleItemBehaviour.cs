using UnityEngine;

namespace PipeWorld.Piper
{
    public unsafe class DoorSingleItemBehaviour : PropBehaviour, IInteractable
    {
        public Transform panel;
        public float slideDistance = 1.3f;
        public float slideSpeed = 4f;

        private Vector3 closedLocalPosition;
        private float openAmount;

        public DoorSingleItemBehaviour() : base(PropPrototypeId.DoorSingleItem) { }

        public override void resetVisualisation(bool isNewlyPlacedByPlayer)
        {
            base.resetVisualisation(isNewlyPlacedByPlayer);
            closedLocalPosition = panel.localPosition;
            Prop* p = prop;
            openAmount = p != null && p->data.door.doorOpen ? 1f : 0f;
            ApplyPanel();
        }

        public override void updateVisualisation()
        {
            Prop* p = prop;
            if (p == null)
                return;

            openAmount = Mathf.MoveTowards(openAmount, p->data.door.doorOpen ? 1f : 0f, slideSpeed * Time.deltaTime / slideDistance);
            ApplyPanel();
        }

        private void ApplyPanel()
        {
            panel.localPosition = closedLocalPosition + Vector3.right * (slideDistance * Mathf.SmoothStep(0f, 1f, openAmount));
        }

        public void GetPrimaryVerb(out string text, out bool actionEnabled, out string errorMessage)
        {
            Prop* p = prop;
            text = p != null && p->data.door.doorOpen ? "Close" : "Open";
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
