using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    [Flags]
    public enum TooltipRequestTags
    {
        None = 0,
        Normal = 1 << 0,
    }

    public interface TooltipProvider
    {
        List<VisualElement> createTooltips(TooltipRequestTags requestedTags);
        void updateTooltips(TooltipRequestTags requestedTags);
    }

    public unsafe class PropTooltipProvider : TooltipProvider
    {
        public PropHandle propHandle { get; private set; }

        public PropTooltipProvider(PropHandle propHandle)
        {
            this.propHandle = propHandle;
        }

        public virtual List<VisualElement> createTooltips(TooltipRequestTags requestedTags)
        {
            if (propHandle.tryGetPointer() == null)
                return new List<VisualElement>();

            return new List<VisualElement> { PropTooltip.Create(propHandle, showDescription: true) };
        }

        public virtual void updateTooltips(TooltipRequestTags requestedTags) { }
    }
}
