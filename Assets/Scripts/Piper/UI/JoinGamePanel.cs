using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public class JoinGamePanel : MenuViewPanel
    {
        private readonly TextField ipField;
        private readonly Label errorLabel;
        private readonly IMenuActions actions;

        public JoinGamePanel(IMenuActions actions)
        {
            this.actions = actions;
            AddToClassList("menu-view-panel--join-game");

            var header = new VisualElement();
            header.AddToClassList("join-header");
            Add(header);

            var title = new Label("Join Game");
            title.AddToClassList("load-game-title");
            header.Add(title);

            var hint = new Label($"Enter the host's address. The host uses port {Constants.DEFAULT_PORT} unless the address gives another.");
            hint.AddToClassList("join-section-title");
            Add(hint);

            var bottom = new VisualElement();
            bottom.AddToClassList("join-connect-section");
            Add(bottom);

            var divider = new VisualElement();
            divider.AddToClassList("menu-divider-tight");
            bottom.Add(divider);

            var connectTitle = new Label("Direct Connect");
            connectTitle.AddToClassList("join-section-title");
            bottom.Add(connectTitle);

            var joinRow = new VisualElement();
            joinRow.AddToClassList("menu-join-row");
            bottom.Add(joinRow);

            ipField = new TextField { value = PiperMain.p.settings.lastServerAddress ?? "" };
            joinRow.Add(ipField);

            var joinBtn = new Button(OnDirectJoin) { text = "Join" };
            joinRow.Add(joinBtn);

            errorLabel = new Label();
            errorLabel.AddToClassList("join-error-label");
            bottom.Add(errorLabel);
        }

        private void OnDirectJoin()
        {
            string addr = (ipField.value ?? "").Trim();
            if (string.IsNullOrEmpty(addr))
            {
                errorLabel.text = "Please enter an address.";
                return;
            }

            errorLabel.text = "";
            actions.JoinGame(addr);
        }
    }
}
