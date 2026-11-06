using UnityEngine;

namespace PipeWorld.Piper
{
    public interface IMenuActions
    {
        void NewGame();
        void LoadGame(string fileName);
        void JoinGame(string address);
        void Save(System.Action onSaved);
        void StartHosting();
        void StopHosting();
        void QuitToMainMenu();
        void QuitToDesktop();
    }

    public class GameMenuActions : IMenuActions
    {
        public void NewGame() => PiperMain.p.game.NewGameDeferred();

        public void LoadGame(string fileName) => PiperMain.p.game.LoadGameDeferred(fileName);

        public void JoinGame(string address) => PiperMain.p.game.ConnectToAddressDeferred(address);

        public void Save(System.Action onSaved) => PiperMain.p.game.SaveGameDeferred(onSaved);

        public void StartHosting() => PiperMain.p.game.StartHostingDeferred();

        public void StopHosting() => PiperMain.p.game.GoSingleplayerDeferred();

        public void QuitToMainMenu() => PiperMain.p.game.ReturnToMainMenu();

        public void QuitToDesktop()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
