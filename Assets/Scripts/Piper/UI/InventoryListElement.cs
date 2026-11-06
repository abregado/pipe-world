
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public class InventoryListElement: VisualElement {
        public new class UxmlFactory: UxmlFactory<InventoryListElement>{}

        private bool namesDisplayed = true;
        private Label header;

        public InventoryListElement() {
            header = new Label() { text = "INVENTORY" };
            header.AddToClassList("inventory-header");
            header.style.display = DisplayStyle.None;
            Add(header);
        }

        public void ResetElement() {
            var items = this.Query<InventoryItemElement>().Build();
            foreach (InventoryItemElement child in items)
            {
                this.Remove(child);
            }
        }

        public void UpdateElement() {
            var items = this.Query<InventoryItemElement>().Build();
            bool hasItems = false;
            foreach (InventoryItemElement child in items)
            {
                child.UpdateItem();
                hasItems = true;
            }
            header.style.display = hasItems ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ToggleNames() {
            namesDisplayed = !namesDisplayed;
            foreach (InventoryItemElement child in this.Query<InventoryItemElement>().Build())
            {
                child.SetNameVisibility(namesDisplayed);
            }
        }
    }
}
