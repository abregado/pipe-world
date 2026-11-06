using System;
using System.IO;
using PipeWorld.Player;
using UnityEngine;

namespace PipeWorld.Piper
{
    [DefaultExecutionOrder(-900)]
    public unsafe class PiperGame : MonoBehaviour
    {
        public World world;
        public SystemSync syncer;
        public NetworkManager networkManager;
        public InputActionManager inputActionManager;

        [NonSerialized] public GameObject freeListObject;
        [NonSerialized] public GameObject liveListObject;
        [NonSerialized] public PlayerController playerObject;
        [NonSerialized] public PlayerRaycaster raycaster;
        [NonSerialized] public InteractionModeSelector interactionModeSelector;
        [NonSerialized] public GameHud hud;
        [NonSerialized] public MainMenu mainMenu;
        [NonSerialized] public EscapeMenu escapeMenu;
        [NonSerialized] public OtherPlayerSyncer otherPlayers;
        private PiperSelfTest selfTest;

        public bool mainMenuMode { get; private set; }

        private bool saveGameQueued;
        private Action saveCompletedCallback;
        private string loadGameQueued;
        private bool newGameQueued;
        private bool hostGameQueued;
        private bool goSingleplayerQueued;
        private bool returnToMenuQueued;
        private string connectGameAddressQueued;
        private bool needsSeed;

        public NetUid playerId => PiperMain.p.settings.playerId;

        public string playerName
        {
            get
            {
                if (!string.IsNullOrEmpty(PiperMain.p.settings.playerName))
                    return PiperMain.p.settings.playerName;
                return "Player " + (world != null ? world.data.players.indexOf(playerId) + 1 : 1);
            }
        }

        private void Awake()
        {
            Debug.Log("PiperGame::Awake");
            SUtil.ReleaseAssert(PiperMain.p.game == null);
            PiperMain.p.game = this;

            freeListObject = new GameObject("FreeList");
            freeListObject.SetActive(false);
            liveListObject = new GameObject("LiveList");

            world = new World();
            inputActionManager = new InputActionManager();
            networkManager = new SingleplayerNetworkManager();
            syncer = new SystemSync(world);
            needsSeed = true;
        }

        private void Start()
        {
            playerObject = FindFirstObjectByType<PlayerController>();
            SUtil.ReleaseAssert(playerObject != null, "PiperGame needs a PlayerController in the scene");
            raycaster = playerObject.GetComponent<PlayerRaycaster>();
            SUtil.ReleaseAssert(raycaster != null, "the player needs a PlayerRaycaster");

            interactionModeSelector = FindFirstObjectByType<InteractionModeSelector>();
            hud = FindFirstObjectByType<GameHud>();
            mainMenu = FindFirstObjectByType<MainMenu>();
            escapeMenu = FindFirstObjectByType<EscapeMenu>();
            SUtil.ReleaseAssert(interactionModeSelector != null && hud != null && mainMenu != null && escapeMenu != null, "UI objects missing from the scene");

            otherPlayers = new OtherPlayerSyncer(liveListObject.transform);
            hud.Setup();
            interactionModeSelector.setupUi();
            mainMenu.Setup();
            escapeMenu.Setup();
            ApplySettings();

            string[] args = Environment.GetCommandLineArgs();
            ParseCommandLine(args);
            Debug.Log($"[Piper] start, skipMenu={skipMenuOnStart}, args={string.Join(" ", args)}");

            if (!skipMenuOnStart)
            {
                mainMenuMode = true;
                mainMenu.SetMenuState(true);
            }
            else
            {
                EnterPlay();
                if (openEscapeOnStart)
                    escapeMenu.SetMenuState(true);
            }
        }

        private bool skipMenuOnStart;
        private bool openEscapeOnStart;
        private string startHostAddress;

        // A screenshot run skips the main menu unless -piperMenu is given. -piperPlay skips the main menu; -piperHost starts hosting; -piperJoin <address> connects to a host.
        private void ParseCommandLine(string[] args)
        {
            bool screenshot = Array.IndexOf(args, "-shot") >= 0;
            bool forceMenu = Array.IndexOf(args, "-piperMenu") >= 0;
            skipMenuOnStart = screenshot && !forceMenu;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-piperPlay")
                    skipMenuOnStart = true;
                else if (args[i] == "-piperHost")
                {
                    skipMenuOnStart = true;
                    hostGameQueued = true;
                }
                else if (args[i] == "-piperJoin" && i + 1 < args.Length)
                {
                    skipMenuOnStart = true;
                    connectGameAddressQueued = args[i + 1];
                }
                else if (args[i] == "-piperSelfTest")
                {
                    skipMenuOnStart = true;
                    selfTest = new PiperSelfTest(this);
                }
                else if (args[i] == "-piperEscape")
                {
                    skipMenuOnStart = true;
                    openEscapeOnStart = true;
                }
                else if (args[i] == "-piperPlayerId" && i + 1 < args.Length)
                {
                    PiperMain.p.settings.playerIdOverride = args[i + 1];
                }
                else if (args[i] == "-piperName" && i + 1 < args.Length)
                {
                    PiperMain.p.settings.playerName = args[i + 1];
                }
            }
        }

        private bool destroyed;

        public void doDestroy()
        {
            if (destroyed)
                return;

            destroyed = true;
            networkManager?.Dispose();
        }

        private void OnDestroy()
        {
            doDestroy();
            if (PiperMain.p != null && PiperMain.p.game == this)
                PiperMain.p.game = null;
        }

        public void ApplySettings()
        {
            Settings s = PiperMain.p.settings;
            if (playerObject == null)
                return;

            playerObject.mouseDegreesPerPixel = 0.1f * s.mouseSensitivity;
            Camera camera = playerObject.GetComponentInChildren<Camera>();
            if (camera != null)
                camera.fieldOfView = s.fieldOfView;
        }

        public PlayerData buildLocalPlayerData()
        {
            Transform camera = playerObject.cameraPivot != null ? playerObject.cameraPivot : playerObject.transform;
            return new PlayerData(playerId, playerName, playerObject.transform.position, camera.rotation.eulerAngles,
                playerObject.IsCrouching, playerObject.IsGrounded);
        }

        private void EnterPlay()
        {
            mainMenuMode = false;
            if (mainMenu.IsOpen)
                mainMenu.SetMenuState(false);
            hud.SetGameplayHudVisible(true);
        }

        public void ReturnToMainMenu() => returnToMenuQueued = true;

        public void NewGameDeferred() => newGameQueued = true;

        public void LoadGameDeferred(string fileName) => loadGameQueued = fileName;

        public void SaveGameDeferred(Action onCompleted = null)
        {
            saveGameQueued = true;
            saveCompletedCallback = onCompleted;
        }

        public void StartHostingDeferred() => hostGameQueued = true;

        public void GoSingleplayerDeferred() => goSingleplayerQueued = true;

        public void ConnectToAddressDeferred(string addr) => connectGameAddressQueued = addr;

        public bool IsSinglePlayer() => networkManager is SingleplayerNetworkManager;

        public bool IsHostingMP() => networkManager is Server;

        public bool IsClientInMP() => networkManager is Client;

        public bool isNetworkReady() => networkManager is not Client client || client.hasLoadedWorld();

        public string describeNetworkStatus()
        {
            if (networkManager is Server server)
                return $"HOSTING  {ActivePlayerCount()} players  port {Constants.DEFAULT_PORT}";
            if (networkManager is Client client)
                return client.hasLoadedWorld() ? $"CONNECTED  {ActivePlayerCount()} players" : "CONNECTING...";
            return "";
        }

        public int ActivePlayerCount()
        {
            int count = 0;
            for (int i = 0; i < Constants.MAX_PLAYER_COUNT; i++)
            {
                NetUid id = world.data.players.get(i).id;
                if (!id.Equals(NetUid.Empty) && !id.Equals(Constants.spectatorGuid) && networkManager.isPlayerOnline(id))
                    count++;
            }
            return count;
        }

        public void SetPlayerPose(Vector3 position, Vector3 eulerRotation)
        {
            playerObject.Teleport(position, eulerRotation.y, NormalisePitch(eulerRotation.x));
        }

        private static float NormalisePitch(float degrees) => degrees > 180f ? degrees - 360f : degrees;

        private void Update()
        {
            InputFixed.accumulateUpdate();
        }

        private void FixedUpdate()
        {
            NetworkingBackend.gns.runCallbacks();
            RunQueuedOperations();

            networkManager.update(world, inputActionManager);
            otherPlayers.sync(world, networkManager, playerId);

            if (needsSeed && !IsClientInMP() && networkManager.allocatorContextData.playerIndex != -1)
            {
                SeedWorld();
                needsSeed = false;
            }

            syncer.sync(playerObject.transform.position);
            selfTest?.tick();

            raycaster.updateRaycast();

            bool interactionAllowed = !mainMenuMode && !playerObject.MenuOpen;
            if (interactionAllowed)
                interactionModeSelector.doUpdate();
            else
                interactionModeSelector.Suspend();

            hud.doUpdate();

            if (mainMenuMode)
                mainMenu.doUpdate();
            else
                escapeMenu.doUpdate();

            InputFixed.update();
        }

        private void RunQueuedOperations()
        {
            if (returnToMenuQueued)
            {
                returnToMenuQueued = false;
                ResetToSingleplayer(seed: false);
                if (escapeMenu.IsOpen)
                    escapeMenu.SetMenuState(false);
                mainMenuMode = true;
                hud.SetGameplayHudVisible(false);
                mainMenu.SetMenuState(true);
            }

            if (newGameQueued)
            {
                newGameQueued = false;
                ResetToSingleplayer(seed: true);
                EnterPlay();
            }

            if (loadGameQueued != null)
            {
                string fileName = loadGameQueued;
                loadGameQueued = null;
                ResetToSingleplayer(seed: false);
                LoadGame(fileName);
                EnterPlay();
                if (escapeMenu.IsOpen)
                    escapeMenu.SetMenuState(false);
            }

            if (saveGameQueued)
            {
                saveGameQueued = false;
                SaveGame();
                saveCompletedCallback?.Invoke();
                saveCompletedCallback = null;
            }

            if (goSingleplayerQueued)
            {
                goSingleplayerQueued = false;
                networkManager.Dispose();
                networkManager = new SingleplayerNetworkManager();
            }

            if (hostGameQueued)
            {
                hostGameQueued = false;
                StartHosting();
            }

            if (!string.IsNullOrEmpty(connectGameAddressQueued))
            {
                string addr = connectGameAddressQueued;
                connectGameAddressQueued = null;
                ConnectToAddress(addr);
            }
        }

        private void StartHosting()
        {
            networkManager.Dispose();
            networkManager = new Server();
        }

        private void ConnectToAddress(string addr)
        {
            PiperMain.p.settings.lastServerAddress = addr;
            PiperMain.p.settings.save();

            // The world arrives from the host, so local props are dropped before the first dump loads.
            ResetToSingleplayer(seed: false, movePlayer: false);
            networkManager.Dispose();
            networkManager = new Client(addr);
            world.resetSyncerOnNextLoad = true;
            EnterPlay();
        }

        // Replaces the world and every prop view, then restarts singleplayer.
        private void ResetToSingleplayer(bool seed, bool movePlayer = true)
        {
            networkManager.Dispose();
            syncer.propSyncer.destroyAll();
            otherPlayers.destroyAll();

            world = new World();
            inputActionManager = new InputActionManager();
            networkManager = new SingleplayerNetworkManager();
            syncer = new SystemSync(world);
            hud.ResetInventoryDisplay();
            needsSeed = seed;

            if (movePlayer)
                Teleport(Vector3.zero, 0f);
        }

        private void Teleport(Vector3 position, float yaw)
        {
            playerObject.Teleport(new Vector3(position.x, position.y + 0.05f, position.z), yaw, 0f);
        }

        // Props go in through the same InputAction path as player placements, so a host's seed reaches clients as normal world state.
        private void SeedWorld()
        {
            (PropPrototypeId id, float x, float z, float yaw)[] layout =
            {
                (PropPrototypeId.DoorSingleItem, 0f, 9f, 0f),
                (PropPrototypeId.RefreshableBox, -3.5f, 5f, 25f),
                (PropPrototypeId.PlaceableLamp, 3.5f, 5f, 0f),
                (PropPrototypeId.CollectableGeneric, 2f, 3f, 10f),
                (PropPrototypeId.CollectableGeneric, -2f, 3.5f, 70f),
                (PropPrototypeId.CollectableGeneric, 5f, 1f, 140f),
                (PropPrototypeId.CollectableGeneric, -5f, 0f, 200f),
                (PropPrototypeId.CollectableGeneric, 0.5f, 6f, 300f),
            };

            foreach ((PropPrototypeId id, float x, float z, float yaw) entry in layout)
                inputActionManager.addPlaceProp(entry.id, new Vector3(entry.x, GroundHeight(entry.x, entry.z), entry.z), new Vector3(0f, entry.yaw, 0f));
        }

        private static float GroundHeight(float x, float z)
        {
            if (Physics.Raycast(new Vector3(x, 300f, z), Vector3.down, out RaycastHit hit, 600f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return 0.05f;
        }

        public string SaveGameNow()
        {
            SaveGame();
            return LatestSavePath();
        }

        public void ReloadFromDiskForTest(string path)
        {
            ResetToSingleplayer(seed: false);
            LoadGame(path);
        }

        private static string LatestSavePath()
        {
            string latest = null;
            foreach (string path in Directory.GetFiles(Constants.saveGameFolder, "save????.bin"))
            {
                if (latest == null || string.CompareOrdinal(path, latest) > 0)
                    latest = path;
            }
            return latest;
        }

        private void SaveGame()
        {
            Directory.CreateDirectory(Constants.saveGameFolder);

            int nextNumber = 1;
            foreach (string path in Directory.GetFiles(Constants.saveGameFolder, "save????.bin"))
            {
                string stem = Path.GetFileNameWithoutExtension(path);
                if (int.TryParse(stem.Substring(4), out int n) && n >= nextNumber)
                    nextNumber = n + 1;
            }

            string savePath = Path.Join(Constants.saveGameFolder, $"save{nextNumber:D4}.bin");
            Debug.Log("Saving to " + savePath);
            File.WriteAllBytes(savePath, world.save());
        }

        private void LoadGame(string fileName)
        {
            string path = Path.IsPathFullyQualified(fileName) ? fileName : Path.Join(Constants.saveGameFolder, fileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning("Cannot load save file " + path);
                return;
            }

            Debug.Log("Loading save file " + path);
            world.resetSyncerOnNextLoad = true;
            world.load(File.ReadAllBytes(path));
            world.data.dirty = true;

            int index = world.data.players.indexOf(playerId);
            PlayerData saved = world.data.players.get(index >= 0 ? index : 0);
            if (saved.id != NetUid.Empty)
                SetPlayerPose(saved.position, saved.rotation);
        }
    }
}
