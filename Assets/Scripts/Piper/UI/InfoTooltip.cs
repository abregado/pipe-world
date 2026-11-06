using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public class InfoTooltip : TooltipElement
    {
        private readonly Label _headingLabel;
        private readonly Label _descriptionLabel;

        public InfoTooltip()
        {
            var headerRow = new VisualElement();
            headerRow.AddToClassList("tooltip-heading-row");
            _headingLabel = new Label() { name = "heading" };
            _headingLabel.AddToClassList("hudtext-large");
            headerRow.Add(_headingLabel);

            Add(headerRow);

            _descriptionLabel = new Label() { name = "description" };
            _descriptionLabel.AddToClassList("hudtext");
            Add(_descriptionLabel);
        }

        public static InfoTooltip Create(string heading, string description)
        {
            var t = new InfoTooltip();
            t._headingLabel.text = heading;
            t._descriptionLabel.text = description;
            return t;
        }
    }
}
