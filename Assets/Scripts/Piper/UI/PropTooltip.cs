using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public unsafe class PropTooltip : TooltipElement
    {
        private readonly Label _nameLabel;
        private readonly Label _subtitleLabel;
        private readonly Label _costLabel;
        private readonly Label _descriptionLabel;
        public readonly VisualElement extraContent;

        public PropTooltip()
        {
            var headerRow = new VisualElement();
            headerRow.AddToClassList("tooltip-heading-row");
            _nameLabel = new Label() { name = "heading" };
            _nameLabel.AddToClassList("hudtext-large");
            headerRow.Add(_nameLabel);

            _subtitleLabel = new Label();
            _subtitleLabel.AddToClassList("tooltip-subtitle");
            headerRow.Add(_subtitleLabel);

            Add(headerRow);

            _costLabel = new Label();
            _costLabel.AddToClassList("hudtext");
            _costLabel.AddToClassList("tooltip-prop-cost");
            Add(_costLabel);

            _descriptionLabel = new Label() { name = "description" };
            _descriptionLabel.AddToClassList("hudtext");
            Add(_descriptionLabel);

            extraContent = new VisualElement();
            extraContent.AddToClassList("tooltip-prop-extra");
            extraContent.RegisterCallback<GeometryChangedEvent>(_ => EnableInClassList("has-extra", extraContent.childCount > 0));
            Add(extraContent);
        }

        public static PropTooltip CreatePreview(string name, string descriptiveType, string description)
        {
            var t = new PropTooltip();
            t._nameLabel.text = name;
            t._subtitleLabel.text = descriptiveType.ToUpper();
            t._costLabel.style.display = DisplayStyle.None;
            t._descriptionLabel.text = description;
            return t;
        }

        public static PropTooltip Create(PropHandle propHandle, bool showCost = false, bool showDescription = false)
        {
            var t = new PropTooltip();
            var proto = PropPrototypes.instance[propHandle.p->prototypeId];

            t._nameLabel.text = proto.name;
            t._subtitleLabel.text = proto.descriptiveType.ToUpper();

            string costItemName = InventoryItemPrototypes.instance[proto.craftCostItemId].name;
            t._costLabel.text = $"{proto.craftCost} {costItemName}";
            t._costLabel.style.display = (showCost && proto.craftCost > 0)
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            t._descriptionLabel.text = proto.description ?? "";
            t._descriptionLabel.style.display = (showDescription && !string.IsNullOrEmpty(proto.description))
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            return t;
        }
    }
}
