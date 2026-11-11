using UnityEngine;

namespace PipeWorld.Piper
{
    public enum TerminalAction : byte { EditBuilding, PlaceFurniture, MoveFurniture, DeleteFurniture }

    // One button on a face of the terminal, activated by left click with the FingerInteraction. It lights up while aimed at.
    public class TerminalButton : MonoBehaviour, IInteractable, IGetHighlighted
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");

        public TerminalAction action;

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock block;

        public static string Label(TerminalAction action) => action switch
        {
            TerminalAction.EditBuilding => "Edit building",
            TerminalAction.PlaceFurniture => "Place furniture",
            TerminalAction.MoveFurniture => "Move furniture",
            _ => "Delete furniture",
        };

        private TerminalBehaviour terminal => GetComponentInParent<TerminalBehaviour>();

        public void GetPrimaryVerb(out string text, out bool actionEnabled, out string errorMessage)
        {
            text = Label(action);
            actionEnabled = true;
            errorMessage = "";
        }

        public void Interact(InteractionMode interactionMode)
        {
            TerminalBehaviour t = terminal;
            if (t == null || t.BuildingId == 0)
                return;

            InteractionModeSelector selector = PiperMain.p.game.interactionModeSelector;
            switch (action)
            {
                case TerminalAction.EditBuilding:
                    selector.EnterBuildingEdit(t.BuildingId);
                    break;
                case TerminalAction.PlaceFurniture:
                    selector.OpenFurniturePicker();
                    break;
                case TerminalAction.MoveFurniture:
                    selector.EnterMoveFurniture();
                    break;
                default:
                    selector.EnterDeleteFurniture();
                    break;
            }
        }

        public bool IsInteractable(InteractionMode interactionMode) => terminal != null;

        public CrosshairStyle GetCrosshairStyle() => CrosshairStyle.Interact;

        public GameObject GetGameObject() => gameObject;

        public void HighlightOn() => SetTint(1.45f);

        public void HighlightOff() => SetTint(1f);

        private void SetTint(float brightness)
        {
            meshRenderer ??= GetComponent<MeshRenderer>();
            block ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(block);
            block.SetColor(TintId, new Color(brightness, brightness, brightness, 1f));
            meshRenderer.SetPropertyBlock(block);
        }
    }
}
