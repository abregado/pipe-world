using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    // Items without an SVG icon fall back to a square tinted with the item's own colour.
    public class IconElement : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<IconElement> { }

        private static readonly Dictionary<InventoryItemPrototypeId, VectorImage> ItemIconCache = new();

        public IconElement()
        {
            AddToClassList("icon-element");
        }

        public void SetItem(InventoryItemPrototypeId id)
        {
            InventoryItemPrototype proto = InventoryItemPrototypes.instance[id];
            VectorImage image = LoadItemIcon(id);
            if (image != null)
            {
                style.backgroundImage = new StyleBackground(image);
                style.unityBackgroundImageTintColor = proto.color;
                style.backgroundColor = StyleKeyword.Null;
                RemoveFromClassList("icon-element--fallback");
            }
            else
            {
                style.backgroundImage = StyleKeyword.Null;
                style.backgroundColor = proto.color;
                AddToClassList("icon-element--fallback");
            }
        }

        private static VectorImage LoadItemIcon(InventoryItemPrototypeId id)
        {
            if (ItemIconCache.TryGetValue(id, out VectorImage cached))
                return cached;

            VectorImage loaded = Resources.Load<VectorImage>("Icons/Items/icon-" + id.ToString().ToLower());
            ItemIconCache[id] = loaded;
            return loaded;
        }
    }
}
