using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Debug = UnityEngine.Debug;

namespace PipeWorld.Piper
{
    public enum PacketType : byte
    {
        Chunk,
        ServerToClient,
        ClientToServer,
        ClientToServerAuth,

        Invalid
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PacketChunkHeader
    {
        public PacketType packetType;
        public ushort partIndex;
        public ushort partCountTotal;
        public uint packetIndex;

        public int lastActionProcessedByServerIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ServerToClientPacket
    {
        public PacketType packetType;
        public ulong packetIndex;
        public int lastActionReceivedByServerIndex;
        public BasicPlayerDataList playerDataList;
        private fixed byte _playerOnline[((int)Constants.MAX_PLAYER_COUNT / 8) + 1];
        public ulong serverCurrentTick;

        public bool getPlayerOnline(int index) { return Bit.get(_playerOnline[index / 8], index % 8); }
        public void setPlayerOnline(int index, bool value) { Bit.set(ref _playerOnline[index / 8], index % 8, value); }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ClientToServerPacketAuth
    {
        public PacketType packetType;
        public fixed byte secret[Constants.MULTIPLAYER_AUTH_KEY_LENGTH];
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ClientToServerPacket
    {
        public PacketType packetType;
        public uint lastWorldPacketIndex;
        public ulong clientCurrentTick;
        public PlayerData playerData;
        private fixed byte _inputActions[InputAction.SizeOf * MAX_INPUT_ACTIONS];

        public const int MAX_INPUT_ACTIONS = 15;

        public InputAction this[int index]
        {
            get
            {
                fixed (byte* ptr = _inputActions)
                    return ((InputAction*)ptr)[index];
            }
            set
            {
                fixed (byte* ptr = _inputActions)
                    ((InputAction*)ptr)[index] = value;
            }
        }

        public const int SizeOf = 784;
    }

    public abstract unsafe class NetworkManager : IDisposable
    {
        private readonly Stopwatch stopwatch;
        public AllocatorContextData allocatorContextData;

        protected NetworkManager()
        {
            stopwatch = new Stopwatch();
            stopwatch.Start();

            allocatorContextData = new AllocatorContextData();
            allocatorContextData.playerIndex = -1;
        }

        protected long getTimeMs()
        {
            return stopwatch.ElapsedMilliseconds;
        }

        public abstract void update(World world, InputActionManager inputActionManager);

        protected static bool updatePlayerData(World world, PlayerData playerData)
        {
            int clientIndex = -1;
            int freeIndex = -1;

            for (int i = 0; i < Constants.SPECTATOR_PLAYER_INDEX; i++)
            {
                PlayerData thisSlot = world.data.players.get(i);

                if (thisSlot.id.Equals(playerData.id))
                    clientIndex = i;
                if (freeIndex == -1 && thisSlot.id.Equals(NetUid.Empty))
                    freeIndex = i;
            }

            if (clientIndex == -1 && playerData.id.Equals(Constants.spectatorGuid))
                clientIndex = Constants.SPECTATOR_PLAYER_INDEX;

            if (clientIndex == -1)
            {
                if (freeIndex == -1)
                    return false;

                clientIndex = freeIndex;
                Debug.Log($"New player {playerData.getName()}/{playerData.id} assigned index {clientIndex}");
            }

            world.data.players.set(clientIndex, playerData);
            return true;
        }

        protected void loadAllocatorStateIfNeeded(World world)
        {
            if (allocatorContextData.playerIndex != -1)
                return;

            for (int i = 0; i < Constants.MAX_PLAYER_COUNT; i++)
            {
                PlayerData playerData = world.data.players.get(i);
                if (playerData.id == PiperMain.p.game.playerId)
                {
                    allocatorContextData.playerIndex = i;
                    Debug.Log("Allocator context data loaded, player index:" + i);
                    break;
                }
            }

            if (allocatorContextData.playerIndex == -1)
                return;

            uint maxPropId = 0;
            for (int i = 0; i < world.props.slabs.size; i++)
            {
                Prop* prop = world.props.slabs.get(i);
                if (prop->slabHeader.allocated && prop->slabHeader.playerIndex == allocatorContextData.playerIndex)
                    maxPropId = Math.Max(maxPropId, prop->slabHeader.perPlayerGeneration);
            }
            allocatorContextData.nextPropId = maxPropId + 1;
        }

        public abstract bool isPlayerOnline(NetUid playerId);

        public abstract void Dispose();
    }
}
