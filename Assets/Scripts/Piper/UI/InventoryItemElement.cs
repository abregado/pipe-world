using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public class InventoryItemElement: VisualElement {
        public new class UxmlFactory: UxmlFactory<InventoryItemElement>{}

        private int oldValue = 0;
        private float lastChangedTime = 0;
        private const float changeTime = 0.95f;
        private readonly IconElement icon;

        public InventoryItemPrototypeId itemId {
            set => icon.SetItem(value);
        }

        public int quantity
        {
            get => int.Parse(this.Query<Label>().First().text);
            set => SetValue(value);
        }

        public string title
        {
            get => this.Query<Label>().AtIndex(1).text;
            set => this.Query<Label>().AtIndex(1).text = value;
        }

        public InventoryItemElement() {
            var gradientContainer = new VisualElement();
            gradientContainer.AddToClassList("inventory-gradient");
            for (int i = 0; i < 16; i++) {
                var step = new VisualElement();
                step.AddToClassList("gradient-step");
                step.AddToClassList($"gradient-step-{i + 1}");
                gradientContainer.Add(step);
            }
            Add(gradientContainer);

            icon = new IconElement() { name = "icon" };
            icon.AddToClassList("icon-16");
            Add(icon);
            Add(new Label(){name="count",text="0"});
            Add(new Label(){name="title"});
        }

        private void SetValue(int newValue) {
            Label countLabel = this.Query<Label>().First();

            if (newValue > oldValue)
            {
                AddToClassList("up");
                RemoveFromClassList("down");
                lastChangedTime = Time.time;
            }

            if (newValue < oldValue)
            {
                AddToClassList("down");
                RemoveFromClassList("up");
                lastChangedTime = Time.time;
            }

            oldValue = newValue;
            countLabel.text = newValue.ToString();
        }

        public void UpdateItem() {
            if (lastChangedTime > -1f && Time.time > lastChangedTime + changeTime)
            {
                RemoveFromClassList("up");
                RemoveFromClassList("down");
                lastChangedTime = -1f;
            }
        }

        public void SetNameVisibility(bool state) {
            this.Query<Label>().AtIndex(1).style.display =
                new StyleEnum<DisplayStyle>(state ? DisplayStyle.Flex : DisplayStyle.None);
        }

        public void SlideIn() {
            AddToClassList("offscreen");
            schedule.Execute(() => RemoveFromClassList("offscreen")).StartingIn(50);
        }
    }
}
