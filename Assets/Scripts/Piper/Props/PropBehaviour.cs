using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    [SelectionBase]
    public abstract unsafe class PropBehaviour : MonoBehaviour, TooltipProvider, IGetHighlighted
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private const float HighlightBrightness = 1.35f;

        [NonSerialized] public PropHandle propHandle;
        [NonSerialized] public readonly PropPrototypeId PropPrototypeId;

        private Renderer[] renderers;
        private MaterialPropertyBlock tintBlock;
        private Color baseTint = Color.white;
        private bool highlighted;
        private PropTooltipProvider tooltipProvider;

        protected PropBehaviour(PropPrototypeId propPrototypeId)
        {
            this.PropPrototypeId = propPrototypeId;
        }

        protected Prop* prop => propHandle.tryGetPointer();

        // Props are pooled, so this runs every time a view is (re)bound to a world prop.
        public virtual void resetVisualisation(bool isNewlyPlacedByPlayer)
        {
            highlighted = false;
            baseTint = Color.white;
            ApplyTint();
        }

        public virtual void updateVisualisation() { }

        public virtual void PrepareForPool()
        {
            highlighted = false;
        }

        protected void SetBaseTint(Color color)
        {
            baseTint = color;
            ApplyTint();
        }

        private void ApplyTint()
        {
            renderers ??= GetComponentsInChildren<Renderer>(true);
            tintBlock ??= new MaterialPropertyBlock();

            Color tint = baseTint * (highlighted ? HighlightBrightness : 1f);
            tint.a = 1f;
            foreach (Renderer r in renderers)
            {
                if (r is MeshRenderer && r.sharedMaterial != null && r.sharedMaterial.HasProperty(TintId))
                {
                    r.GetPropertyBlock(tintBlock);
                    tintBlock.SetColor(TintId, tint);
                    r.SetPropertyBlock(tintBlock);
                }
            }
        }

        public void HighlightOn()
        {
            highlighted = true;
            ApplyTint();
        }

        public void HighlightOff()
        {
            highlighted = false;
            ApplyTint();
        }

        public GameObject GetGameObject() => gameObject;

        protected virtual void Update()
        {
            if (prop != null)
                updateVisualisation();
        }

        public List<VisualElement> createTooltips(TooltipRequestTags requestedTags)
        {
            tooltipProvider = new PropTooltipProvider(propHandle);
            return tooltipProvider.createTooltips(requestedTags);
        }

        public void updateTooltips(TooltipRequestTags requestedTags)
        {
            tooltipProvider?.updateTooltips(requestedTags);
        }
    }
}
