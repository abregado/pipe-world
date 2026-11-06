using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public class MainMenu : MonoBehaviour
    {
        private bool menuOpen;
        private VisualElement container;
        private readonly List<Button> allButtons = new();
        private Button activeButton;
        private readonly Dictionary<Button, (string originalText, MenuViewPanel panel)> panelButtons = new();

        private SettingsPanel settingsPanel;
        private JoinGamePanel joinGamePanel;
        private LoadGamePanel loadGamePanel;
        private Button continueButton;
        private Button loadGameButton;

        private UiPanel panel;
        private readonly IMenuActions actions = new GameMenuActions();

        public bool IsOpen => menuOpen;

        private void Awake()
        {
            panel = UiPanel.Create(transform, "MainMenuPanel", GetComponent<ScreenUIHolder>().panelSettings, 20);
            UiSkins.Register(UiSurface.MainMenu, panel);
        }

        public void Setup()
        {
            VisualElement uiRoot = panel.content;

            container = new VisualElement();
            container.AddToClassList("menu");
            container.AddToClassList("main-menu");
            uiRoot.Add(container);

            var logo = new Label("PIPE WORLD");
            logo.AddToClassList("menu-logo");
            container.Add(logo);

            var contentRow = new VisualElement();
            contentRow.AddToClassList("menu-main-content");
            container.Add(contentRow);

            var buttonColumn = new VisualElement();
            buttonColumn.AddToClassList("menu-button-column");
            contentRow.Add(buttonColumn);

            var panelArea = new VisualElement();
            panelArea.AddToClassList("menu-panel-area");
            contentRow.Add(panelArea);

            settingsPanel = new SettingsPanel();
            joinGamePanel = new JoinGamePanel(actions);
            loadGamePanel = new LoadGamePanel(actions);
            panelArea.Add(settingsPanel);
            panelArea.Add(joinGamePanel);
            panelArea.Add(loadGamePanel);

            var newGame = MakeButton("New Game", "newGame");
            newGame.RegisterCallback<ClickEvent>(_ => actions.NewGame());
            buttonColumn.Add(newGame);

            continueButton = MakeButton("Continue", "continueGame");
            continueButton.RegisterCallback<ClickEvent>(_ => ContinueMostRecentSave());
            buttonColumn.Add(continueButton);

            loadGameButton = MakeMenuButton("Load Game", loadGamePanel);
            buttonColumn.Add(loadGameButton);

            buttonColumn.Add(MakeMenuButton("Join Game", joinGamePanel));
            buttonColumn.Add(MakeDivider());
            buttonColumn.Add(MakeMenuButton("Settings", settingsPanel));
            buttonColumn.Add(MakeDivider());

            var quit = MakeButton("Quit", "quit");
            quit.RegisterCallback<ClickEvent>(_ => actions.QuitToDesktop());
            buttonColumn.Add(quit);

            container.RegisterCallback<NavigationMoveEvent>(OnNavigationMove);

            var footer = new VisualElement();
            footer.AddToClassList("menu-footer");
            container.Add(footer);

            var footerRight = new VisualElement();
            footerRight.AddToClassList("menu-footer-right");
            footer.Add(footerRight);

            var versionLabel = new Label($"v{Application.version}");
            versionLabel.AddToClassList("menu-version-label");
            footerRight.Add(versionLabel);

            container.style.display = DisplayStyle.None;
            panel.Attached += () =>
            {
                if (menuOpen)
                    FocusFirst();
            };
        }

        private Button MakeButton(string text, string name)
        {
            var btn = new Button { text = text };
            if (name != null)
                btn.name = name;
            allButtons.Add(btn);
            return btn;
        }

        private Button MakeMenuButton(string text, MenuViewPanel viewPanel)
        {
            var btn = new Button { text = text };
            panelButtons[btn] = (text, viewPanel);
            btn.RegisterCallback<ClickEvent>(_ =>
            {
                if (activeButton == btn)
                    ClosePanel();
                else
                    OpenPanel(btn);
            });
            allButtons.Add(btn);
            return btn;
        }

        private static VisualElement MakeDivider()
        {
            var d = new VisualElement();
            d.AddToClassList("menu-divider");
            return d;
        }

        private void FocusFirst()
        {
            foreach (Button b in allButtons)
            {
                if (b.enabledSelf && b.style.display != DisplayStyle.None)
                {
                    b.Focus();
                    return;
                }
            }
        }

        private void OnNavigationMove(NavigationMoveEvent evt)
        {
            int delta = evt.direction switch
            {
                NavigationMoveEvent.Direction.Down => 1,
                NavigationMoveEvent.Direction.Up => -1,
                _ => 0
            };
            if (delta == 0)
                return;

            VisualElement focused = container.panel?.focusController?.focusedElement as VisualElement;
            int idx = allButtons.FindIndex(b => b == focused);

            for (int i = 1; i <= allButtons.Count; i++)
            {
                int next = ((idx + i * delta) % allButtons.Count + allButtons.Count) % allButtons.Count;
                if (allButtons[next].enabledSelf && allButtons[next].style.display != DisplayStyle.None)
                {
                    allButtons[next].Focus();
                    evt.StopPropagation();
                    return;
                }
            }
        }

        private void OpenPanel(Button btn)
        {
            if (activeButton != null)
                ClosePanel();

            activeButton = btn;
            btn.text = "Back";
            btn.AddToClassList("menu-button--active");
            MenuViewPanel viewPanel = panelButtons[btn].panel;
            viewPanel.Show();
            viewPanel.FocusFirst();

            foreach (Button b in allButtons)
            {
                if (b != btn)
                    b.SetEnabled(false);
            }
        }

        private void ClosePanel()
        {
            if (activeButton == null)
                return;

            (string originalText, MenuViewPanel viewPanel) = panelButtons[activeButton];
            Button returning = activeButton;
            activeButton.text = originalText;
            activeButton.RemoveFromClassList("menu-button--active");
            viewPanel.Hide();

            foreach (Button b in allButtons)
                b.SetEnabled(true);

            activeButton = null;
            returning.Focus();
        }

        public void doUpdate()
        {
            if (!menuOpen)
                return;

            if (activeButton != null && InputFixed.GetKeyDown(KeyCode.Escape))
                ClosePanel();
        }

        public void SetMenuState(bool state)
        {
            if (menuOpen == state)
                return;

            menuOpen = state;
            if (!state)
                ClosePanel();

            container.style.display = menuOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (menuOpen)
            {
                RefreshSaveButtonVisibility();
                container.schedule.Execute(FocusFirst).StartingIn(50);
            }

            PiperMain.p.game.playerObject.SetMenuState(state);
        }

        private void RefreshSaveButtonVisibility()
        {
            string mostRecentSave = GetMostRecentSave();
            bool compatible = mostRecentSave != null && IsSaveCompatible(Path.Join(Constants.saveGameFolder, mostRecentSave));

            continueButton.style.display = compatible ? DisplayStyle.Flex : DisplayStyle.None;
            loadGameButton.style.display = mostRecentSave != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string GetMostRecentSave()
        {
            string saveDir = Constants.saveGameFolder;
            if (!Directory.Exists(saveDir))
                return null;

            return new DirectoryInfo(saveDir).GetFiles("*.bin").OrderByDescending(f => f.LastWriteTime).FirstOrDefault()?.Name;
        }

        // Continue never loads an incompatible save directly; LoadGamePanel is where version mismatches are shown.
        private static bool IsSaveCompatible(string path)
        {
            try
            {
                World.TryReadSaveVersion(File.ReadAllBytes(path), out int version, out _);
                return version == Constants.saveVersion;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private void ContinueMostRecentSave()
        {
            string fileName = GetMostRecentSave();
            if (fileName != null)
                actions.LoadGame(fileName);
        }
    }
}
