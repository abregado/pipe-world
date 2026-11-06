using System;
using UnityEngine;

namespace PipeWorld.Piper
{
    public enum GameState
    {
        MainMenu = 0,
        Local = 1,
        LocalLoad = 2,
        Join = 3,
    }

    [DefaultExecutionOrder(-1000)]
    public class PiperMain : MonoBehaviour
    {
        public static PiperMain p;

        public GameState gameState = GameState.MainMenu;
        public string serverAddress = "";

        [NonSerialized] public PiperGame game;
        [NonSerialized] public Settings settings;

        private void Awake()
        {
            SUtil.ReleaseAssert(p == null, "more than one PiperMain in the scene");
            p = this;

            Application.runInBackground = true;
            doAsserts();
            settings = Settings.load();
            SystemInit.init();
            GNSNetworkBackend.init();
            ApplyDisplaySettings();

            Debug.Log("PiperMain ready, player id " + settings.playerId);
        }

        private void OnDestroy()
        {
            game?.doDestroy();
            if (NetworkingBackend.gns != null)
            {
                NetworkingBackend.gns.cleanup();
                NetworkingBackend.gns = null;
            }

            if (p == this)
                p = null;
        }

        public void ApplyDisplaySettings()
        {
            QualitySettings.vSyncCount = settings.vsyncEnabled ? 1 : 0;
            AudioListener.volume = settings.masterVolume;
        }

        private static unsafe void doAsserts()
        {
            SUtil.ReleaseAssert(Math.Abs(((float)Constants.TICKS_PER_SECOND) - (1.0f / Time.fixedDeltaTime)) < 0.001f, "fixed timestep must be 1/60");
            SUtil.ReleaseAssertEqual(sizeof(PlayerData), PlayerData.SizeOf);
            SUtil.ReleaseAssertEqual(sizeof(BasicPlayerData), BasicPlayerData.SizeOf);
            SUtil.ReleaseAssertEqual(sizeof(ClientToServerPacket), ClientToServerPacket.SizeOf);
            SUtil.ReleaseAssertEqual(sizeof(InputAction), InputAction.SizeOf);
            SUtil.ReleaseAssert(ClientToServerPacket.SizeOf <= 1450);
        }
    }
}
