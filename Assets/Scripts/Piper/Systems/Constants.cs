using System;
using System.IO;
using UnityEngine;

namespace PipeWorld.Piper
{
    public static class Constants
    {
        public const ulong TICKS_PER_SECOND = 60;
        public const int saveVersion = 4;

        public const int MAX_PLAYER_COUNT = 32;
        public static NetUid spectatorGuid = new NetUid(Guid.Parse("695569e9-7f9e-4df8-b6c1-2da4ae7d20c8"));
        public static int SPECTATOR_PLAYER_INDEX = MAX_PLAYER_COUNT - 1;
        public const int MAX_PLAYER_NAME_LENGTH_UTF8_BYTES = 63;
        public const int MULTIPLAYER_AUTH_KEY_LENGTH = 32;
        public const ushort DEFAULT_PORT = 27020;

        public const int refreshableMaxStored = 5;
        public const ulong refreshableTicksPerItem = 5 * TICKS_PER_SECOND;
        public const float refreshableHoldSeconds = 1.2f;

        public static string profileFolder => Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Application.isEditor ? "PipeWorldInEditor" : "PipeWorld");
        public static string saveGameFolder => Path.Join(profileFolder, "Saves");
    }
}
