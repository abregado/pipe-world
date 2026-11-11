using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    // A full screen list of the furniture the player can place. Choosing one closes the list and hands the choice back; Escape or Cancel
    // closes it without a choice. Opening it counts as a menu, so the cursor is free and the interaction modes are suspended.
    public class FurniturePicker
    {
        private readonly UiPanel panel;
        private readonly VisualElement container;
        private readonly VisualElement grid;
        private readonly Label title;
        private readonly Action<BuildingPropDef> onChosen;

        public bool IsOpen { get; private set; }

        public FurniturePicker(Transform parent, PanelSettings settings, Action<BuildingPropDef> onChosen)
        {
            this.onChosen = onChosen;
            panel = UiPanel.Create(parent, "FurniturePickerPanel", settings, 25);
            UiSkins.Register(UiSurface.FurniturePicker, panel);

            container = new VisualElement { name = "furniture-picker" };
            container.AddToClassList("menu");
            container.style.backgroundColor = new Color(0f, 0f, 0f, 0.82f);
            container.style.alignItems = Align.Center;
            container.style.display = DisplayStyle.None;
            panel.content.Add(container);

            title = new Label("Place furniture");
            title.AddToClassList("menu-logo");
            title.style.marginTop = 24;
            title.style.marginBottom = 24;
            container.Add(title);

            grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.Center;
            grid.style.maxWidth = 1300;
            container.Add(grid);

            var cancel = new Button { text = "Cancel" };
            cancel.style.marginTop = 32;
            cancel.style.width = 240;
            cancel.RegisterCallback<MouseUpEvent>(_ => Close());
            container.Add(cancel);
        }

        public void Open(BuildingPropDef[] choices)
        {
            if (IsOpen)
                return;

            grid.Clear();
            foreach (BuildingPropDef def in choices)
            {
                var button = new Button { text = def.name };
                button.style.width = 260;
                button.style.height = 84;
                button.style.marginLeft = 10;
                button.style.marginRight = 10;
                button.style.marginTop = 10;
                button.style.marginBottom = 10;
                BuildingPropDef chosen = def;
                button.RegisterCallback<MouseUpEvent>(_ =>
                {
                    Close();
                    onChosen(chosen);
                });
                grid.Add(button);
            }

            IsOpen = true;
            container.style.display = DisplayStyle.Flex;
            PiperMain.p.game.playerObject.SetMenuState(true);
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            container.style.display = DisplayStyle.None;
            PiperMain.p.game.playerObject.SetMenuState(false);
        }

        public void doUpdate()
        {
            if (IsOpen && InputFixed.GetKeyDown(KeyCode.Escape))
                Close();
        }
    }
}
