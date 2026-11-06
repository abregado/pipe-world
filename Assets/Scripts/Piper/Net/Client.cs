using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PipeWorld.Piper
{
    public unsafe class Client : NetworkManager
    {
        private ReassemblyBuffer.ReassembledPacketData lastWorldPacket = null;
        private uint lastWorldPacketLoaded = uint.MaxValue;
        private ulong latestServerTick = 0;
        private ulong lastLoadedServerTick = 0;
        private Dictionary<uint, byte[]> snapshots = new Dictionary<uint, byte[]>();

        public bool hasLoadedWorld()
        {
            return snapshots.Count != 0;
        }

        public ulong getTicksBehind()
        {
            return latestServerTick > lastLoadedServerTick ? latestServerTick - lastLoadedServerTick : 0ul;
        }

        private void evictSnapshotsOlderThan(uint keepFromId)
        {
            List<uint> toRemove = null;
            foreach (var kv in snapshots)
            {
                if (kv.Key < keepFromId)
                {
                    if (toRemove == null) toRemove = new List<uint>();
                    toRemove.Add(kv.Key);
                }
            }

            if (toRemove != null)
            {
                foreach (uint id in toRemove)
                    snapshots.Remove(id);
            }
        }
        private ClientConnection clientConnection = ClientConnection.Invalid;
        private IntPtr[] messagePtrs = new IntPtr[265];

        private List<BoxStruct<InputAction>> lagCompensationTempActions = new List<BoxStruct<InputAction>>();
        private int lastActionAppliedForLagCompensationIndex = 0;

        private byte[] authSecret = new byte[Constants.MULTIPLAYER_AUTH_KEY_LENGTH];

        private ServerToClientPacket? lastServerPacket = null;

        public override void Dispose()
        {
            NetworkingBackend.connectionStatusChanged -= connectionStatusChangedCallback;

            Debug.Log("CLIENT worker end");

            if (!clientConnection.isInvalid)
            {
                NetworkingBackend.closeClientConnection(clientConnection, "disconnect");
                clientConnection = ClientConnection.Invalid;
            }
        }

        public Client(string host)
        {
            Debug.Log("Connecting to " + host);

            handleSend();

            if (host.StartsWith("ip://"))
                host = host.Substring("ip://".Length);

            ushort port = Constants.DEFAULT_PORT;
            int colon = host.LastIndexOf(':');
            if (colon > 0 && ushort.TryParse(host.Substring(colon + 1), out ushort parsedPort))
            {
                port = parsedPort;
                host = host.Substring(0, colon);
            }

            clientConnection = NetworkingBackend.gns.connectByIP(host, port);

            NetworkingBackend.connectionStatusChanged += connectionStatusChangedCallback;

        }

        private void connectionStatusChangedCallback(ConnectionStatusChangedEvent data)
        {
            if (data.connection != clientConnection.asConnectionId())
                return;

            if (data.newState == ConnectionState.Connected)
            {
                Debug.Log("Connected, sending auth packet");

                ClientToServerPacketAuth[] authPacket = new ClientToServerPacketAuth[1];
                authPacket[0].packetType = PacketType.ClientToServerAuth;
                for (int i = 0; i < authSecret.Length; i++)
                    authPacket[0].secret[i] = authSecret[i];

                PacketChunk packetChunk = new PacketChunk(sizeof(ClientToServerPacketAuth));
                fixed (ClientToServerPacketAuth* ptr = authPacket)
                    Marshal.Copy((IntPtr)ptr, packetChunk.allBytesTuple.Item1, 0, sizeof(ClientToServerPacket));

                MessageWrapper message = NetworkingBackend.allocateMessage(clientConnection.type, packetChunk.allBytesTuple.Item2);
                message.writeData(packetChunk.allBytesTuple.Item1, packetChunk.allBytesTuple.Item2);
                message.flags = PacketFlags.Reliable | PacketFlags.NoNagle;
                message.connection = clientConnection.asConnectionId();
                message.lane = 0;
                NetworkingBackend.sendMessage(message);
            }
            else if (data.newState == ConnectionState.ClosedByPeer || data.newState == ConnectionState.ProblemDetectedLocally)
            {
                Debug.Log("Disconnected!");
                NetworkingBackend.closeClientConnection(clientConnection, "disconnected");
                PiperMain.p.game.ReturnToMainMenu();
            }
        }

        public override void update(World world, InputActionManager inputActionManager)
        {
            if (allocatorContextData.playerIndex != -1) // ignore all input until we're connected to a server
            {
                for (int i = 0; i < inputActionManager.myInputActions.Count; i++)
                {
                    actionsNotYetReceivedByServer.Add(inputActionManager.myInputActions[i]);
                    actionsNotYetProcessedByServer.Add(inputActionManager.myInputActions[i]);
                }
            }

            inputActionManager.myInputActions.Clear();

            handleReceive(world);
            handleSend();

            netUpdateLoopBody();
        }

        private void handleReceive(World world)
        {
            ReassemblyBuffer.ReassembledPacketData data = lastWorldPacket;
            lastWorldPacket = null;

            BasicPlayerDataList localPlayerDataList = lastPlayerDataList;
            int myLastActionReceivedByServerIndex = lastActionReceivedByServerIndex;
            int myLastActionProcessedByServerIndex = lastActionProcessedByServerIndex;

            if (data != null && data.packetIndex != lastWorldPacketLoaded)
            {
                byte[] anchorBytes = null;

                uint anchorId = WorldDiff.peekAnchorId(data.data);
                if (anchorId != WorldDiff.NoAnchor)
                    SUtil.ReleaseAssert(snapshots.TryGetValue(anchorId, out anchorBytes));

                byte[] worldBytes = WorldDiff.decode(data.data, anchorBytes, out uint newId);

                world.load(worldBytes);
                loadAllocatorStateIfNeeded(world);

                if (!hasLoadedWorld())
                {
                    int myIndex = world.data.players.indexOf(PiperMain.p.game.playerId);
                    if (myIndex != -1)
                    {
                        PlayerData me = world.data.players.get(myIndex);
                        PiperMain.p.game.SetPlayerPose(me.position, me.rotation);
                    }
                }

                snapshots[newId] = worldBytes;
                // Keep the anchor alive — the server may still reference it on sends that were
                // in flight before our ack of newId reached it.
                if (anchorId != WorldDiff.NoAnchor)
                    evictSnapshotsOlderThan(anchorId);
                else
                    evictSnapshotsOlderThan(newId);

                lastWorldPacketLoaded = data.packetIndex;
                lastLoadedServerTick = world.data.currentTick;
                lastActionAppliedForLagCompensationIndex = 0;
            }

            for (int i = 0; i < actionsNotYetReceivedByServer.Count;)
            {
                if (actionsNotYetReceivedByServer[i].v.sequenceNumber <= myLastActionReceivedByServerIndex)
                    actionsNotYetReceivedByServer.RemoveAt(i);
                else
                    i++;
            }

            for (int i = 0; i < actionsNotYetProcessedByServer.Count;)
            {
                if (actionsNotYetProcessedByServer[i].v.sequenceNumber <= myLastActionProcessedByServerIndex)
                    actionsNotYetProcessedByServer.RemoveAt(i);
                else
                    i++;
            }

            for (int i = 0; i < actionsNotYetProcessedByServer.Count; i++)
            {
                BoxStruct<InputAction> action = actionsNotYetProcessedByServer[i];
                if (action.v.sequenceNumber > lastActionAppliedForLagCompensationIndex)
                {
                    lagCompensationTempActions.Add(action);
                    lastActionAppliedForLagCompensationIndex = action.v.sequenceNumber;
                }
            }

            for (int i = 0; i < Constants.MAX_PLAYER_COUNT; i++)
            {
                BasicPlayerData newPlayerData = localPlayerDataList.get(i);
                int liveIndex = world.data.players.indexOf(newPlayerData.id);
                if (liveIndex != -1)
                {
                    PlayerData livePlayerData = world.data.players.get(liveIndex);
                    livePlayerData.basicData = newPlayerData;
                    world.data.players.set(liveIndex, livePlayerData);
                }
            }

            InputActionManager.apply(world, lagCompensationTempActions);
            lagCompensationTempActions.Clear();
            world.update();
        }

        private List<BoxStruct<InputAction>> actionsNotYetReceivedByServer = new List<BoxStruct<InputAction>>();
        private List<BoxStruct<InputAction>> actionsNotYetProcessedByServer = new List<BoxStruct<InputAction>>();

        private ClientToServerPacket[] clientPacket = new ClientToServerPacket[1];
        private int lastActionReceivedByServerIndex = 0;
        private int lastActionProcessedByServerIndex = 0;

        private ulong lastPlayerToServerPacketIndex = 0;
        private BasicPlayerDataList lastPlayerDataList;

        private void handleSend()
        {
            clientPacket[0] = new ClientToServerPacket();
            clientPacket[0].packetType = PacketType.ClientToServer;
            clientPacket[0].lastWorldPacketIndex = lastWorldPacketLoaded != uint.MaxValue ? lastWorldPacketLoaded : 0;
            clientPacket[0].clientCurrentTick = lastLoadedServerTick;
            clientPacket[0].playerData = PiperMain.p.game.buildLocalPlayerData();

            for (int i = 0; i < Math.Min(actionsNotYetReceivedByServer.Count, ClientToServerPacket.MAX_INPUT_ACTIONS); i++)
                clientPacket[0][i] = actionsNotYetReceivedByServer[i].v;
        }


        ReassemblyBuffer reassemblyBuffer = new ReassemblyBuffer();
        long lastSendTimeMs = 0;
        const long updateIntervalMs = 8;


        private void netUpdateLoopBody()
        {
            long now = getTimeMs();

            long elapsed = now - lastSendTimeMs;
            if (elapsed >= updateIntervalMs && hasLoadedWorld()) // hasLoadedWorld() check here because we don't want to send our position before we load our old pos from the received save
            {
                PacketChunk packetChunk = new PacketChunk(sizeof(ClientToServerPacket));

                fixed (ClientToServerPacket* ptr = clientPacket)
                    Marshal.Copy((IntPtr)ptr, packetChunk.allBytesTuple.Item1, 0, sizeof(ClientToServerPacket));

                MessageWrapper message = NetworkingBackend.allocateMessage(clientConnection.type, packetChunk.allBytesTuple.Item2);
                message.writeData(packetChunk.allBytesTuple.Item1, packetChunk.allBytesTuple.Item2);
                message.flags = PacketFlags.Unreliable | PacketFlags.NoNagle;
                message.connection = clientConnection.asConnectionId();
                message.lane = 0;
                NetworkingBackend.sendMessage(message);

                lastSendTimeMs = now;
            }

            int receivedMessageCount = NetworkingBackend.receiveMessagesOnConnection(clientConnection, messagePtrs);
            for (int messageIndex = 0; messageIndex < receivedMessageCount; messageIndex++)
            {
                byte[] data = null;
                NetworkingBackend.readMessage(clientConnection.type, messagePtrs[messageIndex], out data, out int length, out ConnectionId _);
                NetworkingBackend.releaseMessage(clientConnection.type, messagePtrs[messageIndex]);
                messagePtrs[messageIndex] = IntPtr.Zero;

                PacketChunk packet = new PacketChunk(data);

                switch (packet.getPacketType())
                {
                    case PacketType.Chunk:
                    {
                        reassemblyBuffer.addPart(packet);

                        ReassemblyBuffer.ReassembledPacketData reassembled = reassemblyBuffer.tryPopReassembled();
                        if (reassembled != null)
                        {
                            lastWorldPacket = reassembled;

                            fixed (byte* bytePtr = packet.bytes)
                            {
                                PacketChunkHeader* header = (PacketChunkHeader*)bytePtr;
                                lastActionProcessedByServerIndex = header->lastActionProcessedByServerIndex;
                            }
                        }

                        break;
                    }
                    case PacketType.ServerToClient:
                    {
                        fixed (byte* bytePtr = data)
                        {
                            ServerToClientPacket* serverToClientPacket = (ServerToClientPacket*)bytePtr;
                            lastServerPacket = *serverToClientPacket;

                            if (lastPlayerToServerPacketIndex < serverToClientPacket->packetIndex)
                            {
                                lastPlayerToServerPacketIndex = serverToClientPacket->packetIndex;
                                lastActionReceivedByServerIndex = serverToClientPacket->lastActionReceivedByServerIndex;
                                lastPlayerDataList = serverToClientPacket->playerDataList;
                                latestServerTick = serverToClientPacket->serverCurrentTick;
                            }
                        }

                        break;
                    }
                }
            }
        }

        public override bool isPlayerOnline(NetUid playerId)
        {
            if (playerId == PiperMain.p.game.playerId)
                return true;

            if (lastServerPacket == null)
                return false;

            for (int i = 0; i < Constants.MAX_PLAYER_COUNT; i++)
            {
                if (lastServerPacket.Value.playerDataList.get(i).id == playerId)
                    return lastServerPacket.Value.getPlayerOnline(i);
            }

            return false;
        }
    }
}
