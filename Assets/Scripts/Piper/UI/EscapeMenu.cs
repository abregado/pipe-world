using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public class EscapeMenu : MonoBehaviour
    {
        private bool menuOpen;
        private VisualElement container;
        private VisualElement contentRow;
        private VisualElement panelArea;
        private LoadGamePanel loadGamePanel;
        private SettingsPanel settingsPanel;
        private Button loadBtn;
        private Button settingsBtn;
        private MenuViewPanel activePanel;
        private Button activePanelBtn;
        private string activePanelOriginalText;
        private bool pendingShowCancelled;
        private readonly List<Button> allButtons = new();

        private Label saveNotice;
        private int saveNoticeGeneration;

        private UiPanel panel;
        private readonly IMenuActions actions = new GameMenuActions();

        public bool IsOpen => menuOpen;

        private void Awake()
        {
            panel = UiPanel.Create(transform, "EscapeMenuPanel", GetComponent<ScreenUIHolder>().panelSettings, 30);
            UiSkins.Register(UiSurface.EscapeMenu, panel);
        }

        public void Setup()
        {
            container = new VisualElement();
            panel.content.Add(container);
            container.AddToClassList("menu");
            container.AddToClassList("blackout");
            container.AddToClassList("escape-menu");

            var logo = new Label("PIPE WORLD");
            logo.AddToClassList("menu-logo");
            container.Add(logo);

            var logoDivider = new VisualElement();
            logoDivider.AddToClassList("menu-divider-tight");
            container.Add(logoDivider);

            contentRow = new VisualElement();
            contentRow.AddToClassList("escape-menu-content");
            container.Add(contentRow);

            var buttonGroup = new VisualElement();
            buttonGroup.AddToClassList("menu-button-group");
            contentRow.Add(buttonGroup);

            panelArea = new VisualElement();
            panelArea.AddToClassList("escape-menu-panel-area");
            contentRow.Add(panelArea);

            loadGamePanel = new LoadGamePanel(actions);
            panelArea.Add(loadGamePanel);
            settingsPanel = new SettingsPanel();
            panelArea.Add(settingsPanel);

            AddButton(buttonGroup, "Resume", "resume", () => SetMenuState(false));
            AddButton(buttonGroup, "Save Game", "save", () => actions.Save(ShowSavedNotice));

            loadBtn = AddButton(buttonGroup, "Load Game", "load", () =>
            {
                if (activePanel == loadGamePanel) ClosePanel();
                else OpenPanel(loadGamePanel, loadBtn, () => loadGamePanel.RefreshList());
            });

            settingsBtn = AddButton(buttonGroup, "Settings", "settings", () =>
            {
                if (activePanel == settingsPanel) ClosePanel();
                else OpenPanel(settingsPanel, settingsBtn);
            });

            AddButton(buttonGroup, "Begin Hosting", "host", () => actions.StartHosting());
            AddButton(buttonGroup, "End Hosting", "stophosting", () => actions.StopHosting());

            var divider = new VisualElement();
            divider.AddToClassList("menu-divider");
            buttonGroup.Add(divider);

            AddButton(buttonGroup, "Quit to Menu", "quitmenu", () => actions.QuitToMainMenu());
            AddButton(buttonGroup, "Quit to Desktop", "quit", () => actions.QuitToDesktop());

            var footer = new VisualElement();
            footer.AddToClassList("menu-footer");
            container.Add(footer);

            var footerLeft = new VisualElement();
            footerLeft.AddToClassList("menu-footer-left");
            footer.Add(footerLeft);

            saveNotice = new Label("Game Saved");
            saveNotice.AddToClassList("escape-menu-save-notice");
            footer.Add(saveNotice);

            var footerRight = new VisualElement();
            footerRight.AddToClassList("menu-footer-right");
            footer.Add(footerRight);

            var statusWidget = new VisualElement();
            statusWidget.AddToClassList("menu-status");
            statusWidget.AddToClassList("escape-menu-status");
            statusWidget.Add(new Label() { name = "status-key", text = "Mode:" });
            statusWidget.Add(new Label() { name = "status-value" });
            footerRight.Add(statusWidget);

            container.style.display = DisplayStyle.None;
        }

        private Button AddButton(VisualElement parent, string text, string name, Action onClick)
        {
            var button = new Button() { name = name, text = text };
            button.RegisterCallback<MouseUpEvent>(_ => onClick());
            parent.Add(button);
            allButtons.Add(button);
            return button;
        }

        private void OpenPanel(MenuViewPanel viewPanel, Button btn, Action preShow = null)
        {
            if (activePanel != null)
                ClosePanel();

            pendingShowCancelled = false;
            activePanel = viewPanel;
            activePanelBtn = btn;
            activePanelOriginalText = btn.text;

            contentRow.AddToClassList("has-panel");
            panelArea.AddToClassList("open");
            btn.AddToClassList("menu-button--active");
            btn.text = "Back";

            foreach (Button b in allButtons)
            {
                if (b != btn)
                    b.SetEnabled(false);
            }

            preShow?.Invoke();
            viewPanel.pickingMode = PickingMode.Position;
            panelArea.schedule.Execute(() =>
            {
                if (!pendingShowCancelled)
                    viewPanel.AddToClassList("visible");
            }).StartingIn(16);
        }

        private void ClosePanel()
        {
            if (activePanel == null)
                return;

            pendingShowCancelled = true;
            contentRow.RemoveFromClassList("has-panel");
            panelArea.RemoveFromClassList("open");
            activePanel.Hide();
            activePanelBtn.RemoveFromClassList("menu-button--active");
            activePanelBtn.text = activePanelOriginalText;

            foreach (Button b in allButtons)
                b.SetEnabled(true);

            activePanel = null;
            activePanelBtn = null;
        }

        private void ShowSavedNotice()
        {
            int generation = ++saveNoticeGeneration;
            saveNotice.AddToClassList("visible");
            saveNotice.schedule.Execute(() =>
            {
                if (saveNoticeGeneration == generation)
                    saveNotice.RemoveFromClassList("visible");
            }).StartingIn(3000);
        }

        public void doUpdate()
        {
            if (InputFixed.GetKeyDown(KeyCode.Escape))
            {
                if (activePanel != null)
                    ClosePanel();
                else
                    SetMenuState(!menuOpen);
            }

            if (!menuOpen)
                return;

            PiperGame game = PiperMain.p.game;
            bool single = game.IsSinglePlayer();
            bool hosting = game.IsHostingMP();
            bool client = game.IsClientInMP();

            SetDisplay("host", single);
            SetDisplay("stophosting", hosting);

            bool showLoad = single || hosting;
            SetDisplay("load", showLoad);
            if (!showLoad && activePanel == loadGamePanel)
                ClosePanel();

            Label statusValue = container.Q<Label>("status-value");
            statusValue.EnableInClassList("status-single", single);
            statusValue.EnableInClassList("status-hosting", hosting);
            statusValue.EnableInClassList("status-client", client);
            if (single) statusValue.text = "Singleplayer";
            if (hosting) statusValue.text = "Hosting server";
            if (client) statusValue.text = "Client joined";
        }

        private void SetDisplay(string buttonName, bool visible)
        {
            container.Q<Button>(buttonName).style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetMenuState(bool state)
        {
            if (menuOpen == state)
                return;

            menuOpen = state;
            if (!state)
                ClosePanel();

            container.style.display = menuOpen ? DisplayStyle.Flex : DisplayStyle.None;
            PiperMain.p.game.playerObject.SetMenuState(state);
        }
    }
}
