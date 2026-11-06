using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public class TooltipPassthrough : MonoBehaviour, TooltipProvider
    {
        public GameObject target;
        private TooltipProvider targetReal;

        private void Awake()
        {
            SUtil.ReleaseAssert(target != null);
            targetReal = target.GetComponent<TooltipProvider>();
            SUtil.ReleaseAssert(targetReal != null && !ReferenceEquals(targetReal, this), "TooltipPassthrough needs a target with a TooltipProvider");
        }

        public List<VisualElement> createTooltips(TooltipRequestTags requestedTags) => targetReal.createTooltips(requestedTags);
        public void updateTooltips(TooltipRequestTags requestedTags) => targetReal.updateTooltips(requestedTags);
    }
}
