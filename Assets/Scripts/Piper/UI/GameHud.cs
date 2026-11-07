using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public unsafe class GameHud : MonoBehaviour
    {
        private static readonly InventoryItemPrototypeId[] inventoryItemIds = BuildInventoryItemIds();

        private static InventoryItemPrototypeId[] BuildInventoryItemIds()
        {
            var result = new List<InventoryItemPrototypeId>();
            foreach (InventoryItemPrototypeId id in Enum.GetValues(typeof(InventoryItemPrototypeId)))
            {
                if (id != InventoryItemPrototypeId.MAX_VALUE && id != InventoryItemPrototypeId.None)
                    result.Add(id);
            }
            return result.ToArray();
        }

        private VisualElement uiRoot;
        private InventoryListElement inventoryList;
        private uint shownBuilding;
        private readonly Dictionary<InventoryItemPrototypeId, InventoryItemElement> inventoryItemElements = new();

        private VisualElement fullLeft;
        private VisualElement bottomCenter;
        private VisualElement bottomRight;
        private VisualElement crosshairRight;
        private VisualElement crosshairLeft;
        private CrosshairElement crosshair;
        private TooltipManagerElement tooltipManager;
        private Label networkStatus;
        private ModeToolbarElement modeToolbar;
        private EyeCloseElement eyeClose;

        private bool gameplayHudVisible = true;
        private UiPanel panel;

        public VisualElement Root => panel.content;

        private void Awake()
        {
            panel = UiPanel.Create(transform, "HudPanel", FindFirstObjectByType<ScreenUIHolder>().panelSettings, 0);
            HudRegions.Build(panel.content);
            UiSkins.Register(UiSurface.Hud, panel);
        }

        private void OnDestroy()
        {
            UiSettings.Changed -= ApplyUiSettings;
        }

        public void Setup()
        {
            VisualElement root = panel.content;
            uiRoot = root;

            VisualElement crossHairLayer = root.Q<VisualElement>("CrossHairLayer");
            crosshairLeft = new VisualElement { name = "crosshair-left" };
            crosshair = new CrosshairElement();
            crosshairRight = new VisualElement { name = "crosshair-right" };
            crossHairLayer.Add(crosshairLeft);
            crossHairLayer.Add(crosshair);
            crossHairLayer.Add(crosshairRight);

            fullLeft = root.Q<VisualElement>("full-left");
            inventoryList = new InventoryListElement();
            fullLeft.Add(inventoryList);

            bottomRight = root.Q<VisualElement>("bottom-right");
            networkStatus = new Label { name = "network-status" };
            networkStatus.AddToClassList("hudtext");
            networkStatus.style.position = Position.Absolute;
            networkStatus.style.right = 0;
            networkStatus.style.bottom = 0;
            bottomRight.Add(networkStatus);

            tooltipManager = new TooltipManagerElement();
            crosshairRight.Add(tooltipManager);

            bottomCenter = root.Q<VisualElement>("bottom-center");
            BuildModeLabel();
            modeToolbar = new ModeToolbarElement();
            bottomCenter.Add(modeToolbar);

            // Over everything else, so the lids cover the HUD too.
            eyeClose = new EyeCloseElement();
            root.Add(eyeClose);

            UiSettings.Changed += ApplyUiSettings;
            ApplyUiSettings();
            SetGameplayHudVisible(false);
        }

        public void ApplyUiSettings()
        {
            if (uiRoot == null)
                return;

            uiRoot.EnableInClassList("hud-font-normal", PiperMain.p.settings.hudFontSize == HudFontSize.Normal);
        }

        public void SetGameplayHudVisible(bool visible)
        {
            gameplayHudVisible = visible;
            DisplayStyle display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            inventoryList.style.display = display;
            crosshair.style.display = display;
            crosshairLeft.style.display = display;
            crosshairRight.style.display = display;
            bottomCenter.style.display = display;
            networkStatus.style.display = display;

            if (!visible)
                tooltipManager.ClearTooltip();
        }

        private void BuildModeLabel()
        {
            var label = new Label { name = "mode-label" };
            label.AddToClassList("hudtext");
            bottomCenter.Add(label);

            Sprite left = Resources.Load<Sprite>("Glyphs/KeyboardMouse/mouse_left");
            Sprite right = Resources.Load<Sprite>("Glyphs/KeyboardMouse/mouse_right");
            Sprite scroll = Resources.Load<Sprite>("Glyphs/KeyboardMouse/mouse_scroll_vertical");
            crosshairLeft.Add(new InteractionPromptElement() { name = "prompt-left-click", icon = left });
            crosshairLeft.Add(new InteractionPromptElement() { name = "prompt-right-click", icon = right });
            crosshairLeft.Add(new InteractionPromptElement() { name = "prompt-scroll-wheel", icon = scroll });
        }

        public void doUpdate()
        {
            World world = PiperMain.p.game.world;

            if (gameplayHudVisible)
            {
                // The inventory shown is the one of the building the player is in range of; out of range of every building there is none.
                Building* near = BuildingInventory.nearest(world, PiperMain.p.game.playerObject.transform.position, BuildingInventory.Range);
                uint nearId = near == null ? 0u : near->slabHeader._playerIndexAndGeneration;
                if (nearId != shownBuilding)
                {
                    shownBuilding = nearId;
                    ResetInventoryDisplay();
                }
                inventoryList.style.display = near == null ? DisplayStyle.None : DisplayStyle.Flex;

                foreach (InventoryItemPrototypeId itemId in inventoryItemIds)
                {
                    if (near == null || !near->inventory.getSeen(itemId))
                        continue;

                    if (!inventoryItemElements.TryGetValue(itemId, out InventoryItemElement element))
                    {
                        element = new InventoryItemElement { name = $"{itemId}-item" };
                        inventoryList.Add(element);
                        element.SlideIn();
                        inventoryItemElements[itemId] = element;
                        element.itemId = itemId;
                        element.title = InventoryItemPrototypes.instance[itemId].name;
                    }

                    element.quantity = near->inventory.getCount(itemId);
                }

                inventoryList.UpdateElement();
                networkStatus.text = PiperMain.p.game.describeNetworkStatus();
                tooltipManager.update();
            }
        }

        public void ResetInventoryDisplay()
        {
            inventoryList.ResetElement();
            inventoryItemElements.Clear();
        }

        public EyeCloseElement eyes => eyeClose;

        public void SetToolbar(IReadOnlyList<(string name, string icon)> items, int selected) => modeToolbar.Set(items, selected);

        public void ChangeModeText(string text)
        {
            bottomCenter.Q<Label>("mode-label").text = text;
        }

        public void ChangeLeftClickPrompt(string text = "", bool enabled = true, string errorMessage = "", bool isHoldInteraction = false)
        {
            crosshairLeft.Q<InteractionPromptElement>("prompt-left-click").setAction(text, enabled, errorMessage, isHoldInteraction);
        }

        public void ChangeRightClickPrompt(string text = "", bool enabled = true, string errorMessage = "", bool isHoldInteraction = false)
        {
            crosshairLeft.Q<InteractionPromptElement>("prompt-right-click").setAction(text, enabled, errorMessage, isHoldInteraction);
        }

        public void ChangeScrollWheelPrompt(string text = "", bool enabled = true, string errorMessage = "")
        {
            crosshairLeft.Q<InteractionPromptElement>("prompt-scroll-wheel").setAction(text, enabled, errorMessage);
        }

        public void ClearTooltip() => tooltipManager.ClearTooltip();

        public void MarkTooltipDirty() => tooltipManager.MarkTooltipDirty();

        public void SetTooltipValues(bool canTipThroughWalls, float tipViewDistance)
        {
            tooltipManager.canTipThroughWalls = canTipThroughWalls;
            tooltipManager.tipViewDistance = tipViewDistance;
        }

        public void SetCrosshairStyle(CrosshairStyle style) => crosshair.SetStyle(style);

        public void SetCrosshairProgress(float progress) => crosshair.SetProgress(progress);

        public void ClearCrosshairProgress() => crosshair.ClearProgress();
    }
}
