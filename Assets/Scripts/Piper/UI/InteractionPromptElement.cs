
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public class InteractionPromptElement: VisualElement {
        public new class UxmlFactory: UxmlFactory<InteractionPromptElement>{}

        private Label actionLabel;
        private Label holdLabel;
        private Label errorLabel;
        private Sprite _icon;

        public Sprite icon {
            get => _icon;
            set {
                _icon = value;
                this.Q<VisualElement>("icon").style.backgroundImage = new StyleBackground(value);
            }
        }

        private VisualElement modifierIcon;

        public Sprite modifierSprite {
            set {
                modifierIcon.style.backgroundImage = new StyleBackground(value);
                modifierIcon.style.display = new StyleEnum<DisplayStyle>(value != null ? DisplayStyle.Flex : DisplayStyle.None);
            }
        }

        public void setAction(string text, bool enabled, string errorMessage = "", bool isHoldInteraction = false)
        {
            actionLabel.text = text;
            errorLabel.text = errorMessage;

            holdLabel.style.display = new StyleEnum<DisplayStyle>(isHoldInteraction ? DisplayStyle.Flex : DisplayStyle.None);
            errorLabel.style.display = new StyleEnum<DisplayStyle>(errorMessage != "" ? DisplayStyle.Flex : DisplayStyle.None);
            style.display = new StyleEnum<DisplayStyle>(text != "" ? DisplayStyle.Flex : DisplayStyle.None);
        }

        public InteractionPromptElement() {
            // Added first = rightmost with flex-direction: row-reverse
            VisualElement iconContainer = new VisualElement();
            iconContainer.AddToClassList("prompt-icon-container");
            Add(iconContainer);

            holdLabel = new Label() { name = "hold", text = "(Hold)" };
            holdLabel.AddToClassList("prompthold");
            iconContainer.Add(holdLabel);

            VisualElement icon = new VisualElement() { name = "icon" };
            icon.AddToClassList("icon-32");
            icon.AddToClassList("prompt");
            iconContainer.Add(icon);

            modifierIcon = new VisualElement() { name = "modifier-icon" };
            modifierIcon.AddToClassList("icon-32");
            modifierIcon.AddToClassList("prompt");
            modifierIcon.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            Add(modifierIcon);

            // Added last = leftmost with flex-direction: row-reverse
            VisualElement textContainer = new VisualElement();
            textContainer.AddToClassList("prompt-text-container");
            Add(textContainer);

            actionLabel = new Label() { name = "action" };
            actionLabel.AddToClassList("prompttext");
            textContainer.Add(actionLabel);

            errorLabel = new Label() { name = "error" };
            errorLabel.AddToClassList("prompterrortext");
            errorLabel.AddToClassList("button-prompt-disabled");
            textContainer.Add(errorLabel);

            setAction("", true, "");
        }
    }
}
