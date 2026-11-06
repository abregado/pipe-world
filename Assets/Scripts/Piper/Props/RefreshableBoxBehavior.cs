using UnityEngine;

namespace PipeWorld.Piper
{
    public unsafe class RefreshableBoxBehavior : PropBehaviour, IInteractable
    {
        public Transform fillBar;
        public float fillBarFullWidth = 0.7f;

        public RefreshableBoxBehavior() : base(PropPrototypeId.RefreshableBox) { }

        public override void resetVisualisation(bool isNewlyPlacedByPlayer)
        {
            base.resetVisualisation(isNewlyPlacedByPlayer);
            ApplyFill();
        }

        public override void updateVisualisation() => ApplyFill();

        private void ApplyFill()
        {
            Prop* p = prop;
            float fraction = p == null ? 0f : (float)p->data.refreshable.amountStored / Mathf.Max(1, p->data.refreshable.maxStored);
            fillBar.localScale = new Vector3(Mathf.Max(0.001f, fillBarFullWidth * fraction), fillBar.localScale.y, fillBar.localScale.z);
            fillBar.gameObject.SetActive(fraction > 0f);
        }

        public float interactionTime() => Constants.refreshableHoldSeconds;

        public void GetPrimaryVerb(out string text, out bool actionEnabled, out string errorMessage)
        {
            Prop* p = prop;
            int stored = p == null ? 0 : p->data.refreshable.amountStored;
            text = stored > 0 ? $"Take {stored} {InventoryItemPrototypes.instance[p->data.refreshable.givenItem].name}" : "Take";
            actionEnabled = stored > 0;
            errorMessage = stored > 0 ? "" : "Empty";
        }

        public void Interact(InteractionMode interactionMode)
        {
            PiperMain.p.game.inputActionManager.add(new TakeFromRefreshableData() { prop = new PropNetworkIdentifier(this) });
        }

        public bool IsInteractable(InteractionMode interactionMode)
        {
            Prop* p = prop;
            return p != null && p->data.refreshable.amountStored > 0;
        }
    }
}
