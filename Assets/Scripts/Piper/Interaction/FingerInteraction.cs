using UnityEngine;

namespace PipeWorld.Piper
{
    public class FingerInteraction: InteractionMode {
        public LayerMask interactionLayerMask;
        public LayerMask blockingLayerMask;



        public override string GetUIID() {
            return "finger-mode";
        }

        public override string GetEnglishName() {
            return "Interaction mode";
        }

        protected void InteractWithHighlighted() {
            IInteractable interactable = _highlighted.GetComponent<IInteractable>();
            if (!interactable.IsInteractable(this)) return;
            interactable.Interact(this);
            ChangeHighlight();
        }



        protected void UpdateInteractionUI() {
            if (_highlighted != null && _highlighted.GetComponent<IInteractable>() != null && _highlighted.GetComponent<IInteractable>().IsBeingInteractedWith() == false) {
                IInteractable interactable = _highlighted.GetComponent<IInteractable>();
                if (interactable != null && interactable.IsHighlightable(this))
                {
                    interactable.GetPrimaryVerb(out string interactionText, out bool interactionEnabled, out string errorMessage);
                    bool isHold = interactable.interactionTime() > 0;
                    gameHud.ChangeLeftClickPrompt(interactionText, interactionEnabled, errorMessage, isHold);
                    gameHud.SetCrosshairStyle(interactable.GetCrosshairStyle());
                    return;
                }
            }
            gameHud.ChangeLeftClickPrompt("");
            gameHud.ChangeRightClickPrompt("");
        }

        public override void ActivateMode() {
            base.ActivateMode();
            UpdateInteractionUI();
            gameHud.ChangeRightClickPrompt("");
        }

        public override void DeactivateMode() {
            if (_holdTarget != null)
            {
                _holdTarget.ResetVisuals();
                _holdTarget = null;
            }
            time = 0f;
            _secondaryTarget = null;
            _secondaryTime = 0f;
            gameHud.ClearCrosshairProgress();
            base.DeactivateMode();
            ChangeHighlight();
        }

        private float time = 0f;
        private IInteractable _holdTarget = null;
        private float _secondaryTime = 0f;
        private IInteractable _secondaryTarget = null;

        public override bool ModeUpdate() {
            bool holdCancelledThisTick = false;

            UpdateHighlight();

            if (_holdTarget != null)
            {
                bool targetLost = _highlighted == null
                    || _highlighted.GetComponent<IInteractable>() != _holdTarget
                    || !_holdTarget.IsInteractable(this);
                if (targetLost || !InputFixed.GetMouseButton(0))
                {
                    _holdTarget.ResetVisuals();
                    gameHud.ClearCrosshairProgress();
                    _holdTarget = null;
                    time = 0f;
                    holdCancelledThisTick = true;
                }
            }

            if (_highlighted != null)
            {
                var interactable = _highlighted.GetComponent<IInteractable>();
                if (interactable != null)
                {
                    if (interactable.interactionTime() == 0)
                    {
                        if (interactable.IsInteractable(this) && InputFixed.GetMouseButtonDown(0))
                        {
                            interactable.EndVisuals();
                            InteractWithHighlighted();
                            ChangeHighlight();
                            return false;
                        }
                    }
                    else
                    {
                        if (_holdTarget == null && !holdCancelledThisTick
                            && InputFixed.GetMouseButtonDown(0) && interactable.IsInteractable(this))
                        {
                            _holdTarget = interactable;
                            time = 0f;
                            interactable.StartVisuals();
                        }

                        if (_holdTarget == interactable)
                        {
                            time += Time.deltaTime;
                            gameHud.SetCrosshairProgress(time / _holdTarget.interactionTime());
                            interactable.UpdateVisuals();
                            if (time > interactable.interactionTime())
                            {
                                interactable.EndVisuals();
                                gameHud.ClearCrosshairProgress();
                                InteractWithHighlighted();
                                ChangeHighlight();
                                _holdTarget = null;
                                time = 0f;
                                return false;
                            }
                        }
                    }
                }
            }

            UpdateSecondaryHold();
            UpdateInteractionUI();
            return false;
        }

        // Right-click hold on a highlighted interactable that offers a secondary action.
        private void UpdateSecondaryHold() {
            IInteractable interactable = _highlighted != null ? _highlighted.GetComponent<IInteractable>() : null;
            bool hasSecondary = interactable != null && interactable.secondaryInteractionTime() > 0f && interactable.IsInteractable(this);

            bool cancelledThisTick = false;
            if (_secondaryTarget != null && (!hasSecondary || interactable != _secondaryTarget || !InputFixed.GetMouseButton(1))) {
                _secondaryTarget = null;
                _secondaryTime = 0f;
                gameHud.ClearCrosshairProgress();
                cancelledThisTick = true;
            }

            if (!hasSecondary) return;

            gameHud.ChangeRightClickPrompt(interactable.GetSecondaryVerb(), true, "", true);

            if (_secondaryTarget == null && !cancelledThisTick && InputFixed.GetMouseButtonDown(1)) {
                _secondaryTarget = interactable;
                _secondaryTime = 0f;
            }

            if (_secondaryTarget != interactable) return;

            _secondaryTime += Time.deltaTime;
            gameHud.SetCrosshairProgress(_secondaryTime / interactable.secondaryInteractionTime());
            if (_secondaryTime < interactable.secondaryInteractionTime()) return;

            interactable.InteractSecondary(this);
            _secondaryTarget = null;
            _secondaryTime = 0f;
            gameHud.ClearCrosshairProgress();
            ChangeHighlight();
        }

        public override void UpdateHighlight() {
            PlayerRaycaster playerController = PiperMain.p.game.raycaster;

            if (playerController.raycastHitsCount > 0 && playerController.raycastHits[0].distance <= interactDistance)
            {
                RaycastHit hitInfo = playerController.raycastHits[0];

                IInteractable highlightable = hitInfo.collider.GetComponent<IInteractable>();
                if (highlightable != null && highlightable.IsHighlightable(this)) {
                    if ((_highlighted == null || _highlighted != highlightable.GetGameObject())) {
                        ChangeHighlight(highlightable.GetGameObject());
                    }
                }
                else if (_highlighted != null) {
                    ChangeHighlight();
                }
            } else if (_highlighted != null) {
                ChangeHighlight();
            }
        }

        public override bool ObjectOfInterestUnderCursor() {
            PlayerRaycaster playerController = PiperMain.p.game.raycaster;

            return (playerController.raycastHitsCount > 0 &&
                    playerController.raycastHits[0].distance <= interactDistance &&
                    playerController.raycastHits[0].collider.GetComponent<IInteractable>() != null
                );
        }
    }

    public interface IInteractable {
        public void GetPrimaryVerb(out string text, out bool actionEnabled, out string errorMessage);

        public void Interact(InteractionMode interactionMode);

        public bool IsHighlightable(InteractionMode interactionMode) {
            return interactionMode is FingerInteraction;
        }

        public bool IsInteractable(InteractionMode interactionMode);
        public GameObject GetGameObject();

        public float interactionTime() { return 0f; }

        // Right-click hold; 0 means the interactable has no secondary action.
        public float secondaryInteractionTime() { return 0f; }

        public string GetSecondaryVerb() { return ""; }

        public void InteractSecondary(InteractionMode interactionMode) { }

        public CrosshairStyle GetCrosshairStyle() { return CrosshairStyle.Interact; }

        public void StartVisuals() { }

        public void UpdateVisuals() { }

        public void EndVisuals() {}

        public void ResetVisuals() {}

        public bool IsBeingInteractedWith() {
            return false;
        }
    }
}
