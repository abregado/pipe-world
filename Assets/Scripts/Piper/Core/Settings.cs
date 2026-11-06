using System;
using System.IO;
using UnityEngine;

namespace PipeWorld.Piper
{
    public enum HudFontSize { Normal, Larger }

    [Serializable]
    public class Settings
    {
        private static string settingsFilePath => Path.Join(Constants.profileFolder, "settings.json");

        public string playerGuid;
        public string playerName;
        public string lastServerAddress;
        public bool vsyncEnabled;
        public float masterVolume;
        public float mouseSensitivity;
        public float fieldOfView;
        public HudFontSize hudFontSize;

        // Set from the command line so several instances on one machine can have distinct ids; never saved.
        [NonSerialized] private string overrideGuid;
        public string playerIdOverride
        {
            get => overrideGuid;
            set
            {
                overrideGuid = value;
                playerIdCached = false;
            }
        }
        [NonSerialized] private NetUid cachedPlayerId;
        [NonSerialized] private bool playerIdCached;

        public NetUid playerId
        {
            get
            {
                if (!playerIdCached)
                {
                    cachedPlayerId = new NetUid(Guid.Parse(string.IsNullOrEmpty(playerIdOverride) ? playerGuid : playerIdOverride));
                    playerIdCached = true;
                }
                return cachedPlayerId;
            }
        }

        public Settings()
        {
            playerGuid = Guid.NewGuid().ToString();
            playerName = "";
            lastServerAddress = "localhost";
            vsyncEnabled = true;
            masterVolume = 1f;
            mouseSensitivity = 1f;
            fieldOfView = 60f;
            hudFontSize = HudFontSize.Normal;
        }

        public static Settings load()
        {
            Settings settings = new Settings();
            try
            {
                if (File.Exists(settingsFilePath))
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(settingsFilePath), settings);
                Guid.Parse(settings.playerGuid);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not read settings, using defaults: " + e.Message);
                settings = new Settings();
            }

            return settings;
        }

        public void save()
        {
            try
            {
                Directory.CreateDirectory(Constants.profileFolder);
                File.WriteAllText(settingsFilePath, JsonUtility.ToJson(this, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not write settings: " + e.Message);
            }
        }
    }
}
