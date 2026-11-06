using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace PipeWorld.Piper
{
    public class IInteractablePassthrough: MonoBehaviour, IInteractable {
        public GameObject target;

        private void Awake() {
            Debug.Assert(target!=null, $"Passthrough on {gameObject} requires target", gameObject);
            Debug.Assert(target != gameObject, $"Passthrough on {gameObject} should not target itself", gameObject);
            Debug.Assert(target.GetComponent<IInteractable>() != null, $"Passthrough on {gameObject} should target an IInteractable", gameObject);
        }

        public void GetPrimaryVerb(out string text, out bool actionEnabled, out string errorMessage) {
            target.GetComponent<IInteractable>().GetPrimaryVerb(out text, out actionEnabled, out errorMessage);
        }

        public void Interact(InteractionMode interactionMode) {
            target.GetComponent<IInteractable>().Interact(interactionMode);
        }

        public bool IsHighlightable(InteractionMode interactionMode) {
            return target.GetComponent<IInteractable>().IsHighlightable(interactionMode);
        }

        public bool IsInteractable(InteractionMode interactionMode) {
            return target.GetComponent<IInteractable>().IsInteractable(interactionMode);
        }

        public GameObject GetGameObject() {
            return target.GetComponent<IInteractable>().GetGameObject();
        }

    }
}
