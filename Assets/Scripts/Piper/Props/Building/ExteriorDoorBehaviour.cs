using UnityEngine;

namespace PipeWorld.Piper
{
    // The door in an outer wall. The wall stays solid; using the door moves the player to the other side.
    public unsafe class ExteriorDoorBehaviour : PropBehaviour, IInteractable
    {
        public ExteriorDoorBehaviour() : base(PropPrototypeId.ExteriorDoor) { }

        public void GetPrimaryVerb(out string text, out bool actionEnabled, out string errorMessage)
        {
            Prop* p = prop;
            text = p != null && BuildingTravel.IsLocalPlayerInside(PiperMain.p.game, p->building) ? "Go outside" : "Go inside";
            actionEnabled = true;
            errorMessage = "";
        }

        public void Interact(InteractionMode interactionMode)
        {
            Prop* p = prop;
            if (p != null)
                BuildingTravel.UseDoor(PiperMain.p.game, p->building, p->slabHeader._playerIndexAndGeneration);
        }

        public bool IsInteractable(InteractionMode interactionMode) => prop != null;

        public CrosshairStyle GetCrosshairStyle() => CrosshairStyle.Interact;
    }
}
