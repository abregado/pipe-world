using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    // A row of icons showing which interaction modes (or, while editing a building, which tools) the mouse wheel steps through,
    // with the current one lit. Icons are from game-icons.net (see Assets/Resources/Icons/CREDITS.txt).
    public class ModeToolbarElement : VisualElement
    {
        private static readonly Color Selected = new Color(1f, 0.84f, 0.35f, 1f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.55f);

        private readonly List<VisualElement> slots = new List<VisualElement>();
        private readonly List<VisualElement> images = new List<VisualElement>();
        private string signature = "";

        public ModeToolbarElement()
        {
            name = "mode-toolbar";
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.marginBottom = 8;
            pickingMode = PickingMode.Ignore;
        }

        public void Set(IReadOnlyList<(string name, string icon)> items, int selected)
        {
            string newSignature = string.Join(",", System.Linq.Enumerable.Select(items, i => i.icon));
            if (newSignature != signature)
            {
                signature = newSignature;
                Clear();
                slots.Clear();
                images.Clear();

                int number = 0;
                foreach (var item in items)
                {
                    number++;
                    var slot = new VisualElement { name = "slot-" + item.icon };
                    slot.style.width = 48;
                    slot.style.height = 48;
                    slot.style.marginLeft = 3;
                    slot.style.marginRight = 3;
                    slot.style.alignItems = Align.Center;
                    slot.style.justifyContent = Justify.Center;
                    slot.style.borderTopLeftRadius = 6;
                    slot.style.borderTopRightRadius = 6;
                    slot.style.borderBottomLeftRadius = 6;
                    slot.style.borderBottomRightRadius = 6;
                    SetBorder(slot, 2, Color.clear);
                    slot.pickingMode = PickingMode.Ignore;

                    var image = new VisualElement();
                    image.style.width = 34;
                    image.style.height = 34;
                    image.pickingMode = PickingMode.Ignore;
                    Texture2D texture = Resources.Load<Texture2D>("Icons/" + item.icon);
                    if (texture != null)
                        image.style.backgroundImage = new StyleBackground(Background.FromTexture2D(texture));
                    slot.Add(image);

                    // The number key that selects this slot.
                    var key = new Label(number.ToString());
                    key.style.position = Position.Absolute;
                    key.style.top = -2;
                    key.style.right = 1;
                    key.style.fontSize = 11;
                    key.style.unityFontStyleAndWeight = FontStyle.Bold;
                    key.style.color = new Color(1f, 1f, 1f, 0.9f);
                    key.pickingMode = PickingMode.Ignore;
                    slot.Add(key);

                    Add(slot);
                    slots.Add(slot);
                    images.Add(image);
                }
            }

            for (int i = 0; i < slots.Count; i++)
            {
                bool on = i == selected;
                slots[i].style.backgroundColor = on ? new Color(0f, 0f, 0f, 0.65f) : new Color(0f, 0f, 0f, 0.35f);
                SetBorder(slots[i], 2, on ? Selected : Color.clear);
                images[i].style.unityBackgroundImageTintColor = on ? Selected : Dim;
                slots[i].style.scale = new StyleScale(new Scale(on ? new Vector3(1.12f, 1.12f, 1f) : Vector3.one));
            }
        }

        private static void SetBorder(VisualElement e, float width, Color color)
        {
            e.style.borderTopWidth = width;
            e.style.borderBottomWidth = width;
            e.style.borderLeftWidth = width;
            e.style.borderRightWidth = width;
            e.style.borderTopColor = color;
            e.style.borderBottomColor = color;
            e.style.borderLeftColor = color;
            e.style.borderRightColor = color;
        }
    }
}
