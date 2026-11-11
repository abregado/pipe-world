using UnityEngine;

namespace PipeWorld.Piper
{
    public unsafe class CollectableGenericBehaviour : PropBehaviour, IInteractable
    {
        public CollectableGenericBehaviour() : base(PropPrototypeId.CollectableGeneric) { }

        public override void resetVisualisation(bool isNewlyPlacedByPlayer)
        {
            base.resetVisualisation(isNewlyPlacedByPlayer);
            Prop* p = prop;
            if (p != null)
                SetBaseTint(InventoryItemPrototypes.instance[p->data.collectable.givenItem].color);
        }

        public void GetPrimaryVerb(out string text, out bool actionEnabled, out string errorMessage)
        {
            Prop* p = prop;
            text = p == null ? "" : $"Take {InventoryItemPrototypes.instance[p->data.collectable.givenItem].name}";
            actionEnabled = p != null && BuildingInventory.inRange(PiperMain.p.game.world, p->position);
            errorMessage = actionEnabled ? "" : BuildingInventory.OutOfRangeMessage;
        }

        public void Interact(InteractionMode interactionMode)
        {
            Prop* p = prop;
            if (p == null || !BuildingInventory.inRange(PiperMain.p.game.world, p->position))
                return;
            PiperMain.p.game.inputActionManager.add(new CollectCollectableData() { prop = new PropNetworkIdentifier(this) });
        }

        public bool IsInteractable(InteractionMode interactionMode) => prop != null;

        public CrosshairStyle GetCrosshairStyle() => CrosshairStyle.Collecting;
    }
}
