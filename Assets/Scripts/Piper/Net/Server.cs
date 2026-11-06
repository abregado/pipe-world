using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PipeWorld.Piper
{
    public unsafe class Server : NetworkManager
    {
        private const int UNRELIABLE_LANE = 0;
        private const int RELIABLE_LANE = 1;

        private bool newDumpRequested = false;
        private byte[] lastDump = null;
        private PlayerDataList lastPlayerDataList;
        private long lastDumpTimeMs = 1000 * (-10);

        class ClientData
        {
            public ConnectionId connectionId;
            public long lastMessageReceivedMs = -1;
            public uint lastWorldPacketSentIndex = 0;
            public int lastDiffBytesSent = 0;
            public int lastDiffUncompressedBytes = 0;

            public ClientToServerPacket lastReceivedPacket = new ClientToServerPacket();
            public int lastActionProcessedByServerIndex = 0;
            public int lastActionReceivedByServerIndex = 0;
        }

        class PendingConnection
        {
            public ConnectionId connectionId;
            public long connectionTimeMs;
            public bool skipAuth;
        }

        private List<PendingConnection> pendingConnections = new List<PendingConnection>();
        private List<ClientData> clients = new List<ClientData>();
        private List<BoxStruct<InputAction>> mergedInputActions = new List<BoxStruct<InputAction>>();

        public Server()
        {
            Debug.Log("Server is running...");
            loopStart();
        }

        ClientData getClientByConnectionId(ConnectionId connectionId)
        {
            foreach (ClientData clientData in clients)
            {
                if (clientData.connectionId == connectionId)
                    return clientData;
            }
            return null;
        }

        public struct ClientLagInfo
        {
            public string name;
            public ulong ticksBehind;
            public int lastDiffBytesSent;
            public int lastDiffUncompressedBytes;
        }

        public int connectedClientCount
        {
            get { return clients.Count; }
        }

        public List<ClientLagInfo> getClientLagSnapshot(ulong serverTick)
        {
            List<ClientLagInfo> result = new List<ClientLagInfo>();
            foreach (ClientData client in clients)
            {
                ulong clientTick = client.lastReceivedPacket.clientCurrentTick;
                ulong behind = serverTick > clientTick ? serverTick - clientTick : 0ul;
                result.Add(new ClientLagInfo
                {
                    name = client.lastReceivedPacket.playerData.getName(),
                    ticksBehind = behind,
                    lastDiffBytesSent = client.lastDiffBytesSent,
                    lastDiffUncompressedBytes = client.lastDiffUncompressedBytes,
                });
            }
            return result;
        }

        public override void update(World world, InputActionManager inputActionManager)
        {
            lastPlayerDataList = world.data.players;

            {
                PlayerData myPlayerData = PiperMain.p.game.buildLocalPlayerData();
                SUtil.ReleaseAssert(updatePlayerData(world, myPlayerData));
                loadAllocatorStateIfNeeded(world);

                foreach (BoxStruct<InputAction> action in inputActionManager.myInputActions)
                    mergedInputActions.Add(action);
                inputActionManager.myInputActions.Clear();
            }

            for (int clientIndex = 0; clientIndex < clients.Count; )
            {
                ClientData client = clients[clientIndex];

                if (client.lastReceivedPacket.playerData.id == NetUid.Empty)
                {
                    clientIndex++;
                    continue;
                }

                bool playerDataUpdated = updatePlayerData(world, client.lastReceivedPacket.playerData);
                if (!playerDataUpdated)
                {
                    // search for a disconnected player and reuse their slot for this player
                    for (int i = 0; i < Constants.SPECTATOR_PLAYER_INDEX; i++)
                    {
                        PlayerData thisSlot = world.data.players.get(i);
                        if (PiperMain.p.game.playerId.Equals(thisSlot.id))
                            continue;

                        bool connected = false;
                        foreach (ClientData c in clients)
                        {
                            if (c.lastReceivedPacket.playerData.id.Equals(thisSlot.id))
                            {
                                connected = true;
                                break;
                            }
                        }

                        if (!connected)
                        {
                            Debug.Log($"New player {client.lastReceivedPacket.playerData.getName()}/{client.lastReceivedPacket.playerData.id} overwrites index {clientIndex} (was {world.data.players.get(i).getName()}/{world.data.players.get(i).id})");

                            world.data.players.set(i, client.lastReceivedPacket.playerData);

                            playerDataUpdated = true;
                            break;
                        }
                    }
                }

                if (!playerDataUpdated)
                {
                    NetworkingBackend.closeConnection(client.connectionId, "server is full");
                    clients.RemoveAt(clientIndex);
                    continue;
                }
                clientIndex++;

                for (int i = 0; i < ClientToServerPacket.MAX_INPUT_ACTIONS; i++)
                {
                    InputAction inputAction = client.lastReceivedPacket[i];

                    if (inputAction.type == InputActionType.Invalid)
                        break;

                    if (inputAction.sequenceNumber > client.lastActionReceivedByServerIndex)
                    {
                        client.lastActionReceivedByServerIndex = inputAction.sequenceNumber;
                        mergedInputActions.Add(new BoxStruct<InputAction>(inputAction));
                    }
                }
            }

            InputActionManager.apply(world, mergedInputActions);


            mergedInputActions.Clear();
            world.update();
            lastKnownServerTick = world.data.currentTick;

            long now = getTimeMs();
            long timeSinceLastDumpMs = now - lastDumpTimeMs;

            if ((newDumpRequested && timeSinceLastDumpMs >= 1000) || (clients.Count == 0 && timeSinceLastDumpMs > 1000 * 2))
            {
                lastDumpTimeMs = now;
                byte[] dump = world.save();

                foreach (ClientData client in clients)
                    client.lastActionProcessedByServerIndex = client.lastActionReceivedByServerIndex;

                lastDump = dump;

                newDumpRequested = false;
            }

            updateLoopBody();
        }

        unsafe List<PacketChunk> dataToPackets(byte[] dataBytes, uint packetIndex)
        {
            int headerSize = Marshal.SizeOf<PacketChunkHeader>();
            int dataChunkSize = PacketChunk.maxPacketSize - headerSize;

            int partsCount = dataBytes.Length / dataChunkSize;
            if (partsCount * dataChunkSize < dataBytes.Length)
                partsCount++;

            List<PacketChunk> packets = new List<PacketChunk>(partsCount);

            for (int partIndex = 0; partIndex < partsCount; partIndex++)
            {
                int start = partIndex * dataChunkSize;
                int end = Math.Min(start + dataChunkSize, dataBytes.Length);
                int chunkLength = end - start;

                PacketChunk packet = new PacketChunk(chunkLength + headerSize);

                fixed (void* headerV = packet.bytes)
                {
                    PacketChunkHeader* header = (PacketChunkHeader*)headerV;
                    header->partIndex = (ushort)partIndex;
                    header->partCountTotal = (ushort)partsCount;
                    header->packetIndex = packetIndex;
                }

                new Span<byte>(dataBytes, start, chunkLength).CopyTo(packet.bytes.Slice(headerSize, chunkLength));

                packets.Add(packet);
            }

            return packets;
        }

        ListenSocket gnsListenSocket = ListenSocket.Invalid;
        PollGroup gnsPollGroup = PollGroup.Invalid;
        IntPtr[] messagePtrs = new IntPtr[265];

        byte[] worldData = null;
        uint packetIndex = 0;

        private class Snapshot
        {
            public byte[] bytes;
            public Dictionary<ulong, ContentDefinedChunkRef> chunkIndex;
        }

        private Dictionary<uint, Snapshot> snapshots = new Dictionary<uint, Snapshot>();
        private List<ContentDefinedChunkRef> snapshotChunkScratch = new List<ContentDefinedChunkRef>();

        private void addSnapshot(uint id, byte[] bytes)
        {
            Dictionary<ulong, ContentDefinedChunkRef> index = new Dictionary<ulong, ContentDefinedChunkRef>();
            fixed (byte* p = bytes)
            {
                ContentDefinedChunker.split(p, bytes.Length, snapshotChunkScratch);
                for (int i = 0; i < snapshotChunkScratch.Count; i++)
                {
                    ContentDefinedChunkRef c = snapshotChunkScratch[i];
                    ulong h = Standart.Hash.xxHash.xxHash64.ComputeHash(p + c.offset, c.length);
                    index.TryAdd(h, c);
                }
            }
            snapshots[id] = new Snapshot { bytes = bytes, chunkIndex = index };
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
                foreach (uint id in toRemove)
                    snapshots.Remove(id);
        }

        long lastSendStartTimeMs = 0;
        const long updateIntervalMs = 32;

        ulong serverToClientPacketIndex = 1;
        ulong lastKnownServerTick = 0;

        private byte[] authSecret = new byte[Constants.MULTIPLAYER_AUTH_KEY_LENGTH];

        private void loopStart()
        {
            gnsListenSocket = NetworkingBackend.gns.createListenSocketIP(Constants.DEFAULT_PORT);
            gnsPollGroup = NetworkingBackend.gns.createPollGroup();
            Debug.Log("GNS listening on port " + Constants.DEFAULT_PORT);

            NetworkingBackend.connectionStatusChanged += connectionStatusChangedCallback;
        }

        void connectionStatusChangedCallback(ConnectionStatusChangedEvent data)
        {
            Debug.Log("Connection status change: " + data.newState);

            // Connection events for sockets this server did not open (e.g. its own outgoing connections) are not ours.
            bool isGns = !gnsListenSocket.isInvalid && data.listenSocket == gnsListenSocket;

            if (data.oldState == ConnectionState.None && data.newState == ConnectionState.Connecting)
            {
                Debug.Log("Client connected!");
                NetworkingBackend.acceptConnection(data.connection);
                NetworkingBackend.setConnectionPollGroup(data.connection, gnsPollGroup);
                NetworkingBackend.configureConnectionLanes(data.connection, 2, new int[] { 0, 1 }, new ushort[] { 1, 1 });

                Debug.Log("Client connection accepted, waiting for auth");
                pendingConnections.Add(new PendingConnection
                {
                    connectionId = data.connection,
                    connectionTimeMs = getTimeMs(),
                    skipAuth = true,
                });
            }
            else if (data.newState == ConnectionState.ClosedByPeer || data.newState == ConnectionState.ProblemDetectedLocally)
            {
                ClientData client = getClientByConnectionId(data.connection);
                if (client == null)
                {
                    Debug.Log("Connection not found!");
                    return;
                }

                Debug.Log("Client disconnected! " + data.newState);
                NetworkingBackend.closeConnection(data.connection, "client disconnected");
                clients.Remove(client);
            }
        }

        public override void Dispose()
        {
            loopEnd();
        }

        void loopEnd()
        {
            NetworkingBackend.connectionStatusChanged -= connectionStatusChangedCallback;

            foreach (ClientData client in clients)
                NetworkingBackend.closeConnection(client.connectionId, "server stopped");

            if (!gnsPollGroup.isInvalid)
            {
                NetworkingBackend.destroyPollGroup(gnsPollGroup);
                gnsPollGroup = PollGroup.Invalid;
            }

            if (!gnsListenSocket.isInvalid)
            {
                NetworkingBackend.closeListenSocket(gnsListenSocket);
                gnsListenSocket = ListenSocket.Invalid;
            }

            Debug.Log("SERVER worker stopped");
        }

        void updateLoopBody()
        {
            for (int i = 0; i < pendingConnections.Count;)
            {
                long timeSinceConnectionStart = getTimeMs() - pendingConnections[i].connectionTimeMs;
                if (timeSinceConnectionStart < 1000 * 5)
                {
                    i++;
                }
                else
                {
                    Debug.Log("Client connection timed out!");
                    NetworkingBackend.closeConnection(pendingConnections[i].connectionId, "timed out");
                    pendingConnections.RemoveAt(i);
                }
            }

            PollGroup pollGroup = gnsPollGroup;
            if (pollGroup != PollGroup.Invalid)
            {
                int receivedMessageCount = NetworkingBackend.receiveMessagesOnPollGroup(pollGroup, messagePtrs);
                for (int messageIndex = 0; messageIndex < receivedMessageCount; messageIndex++)
                {
                    byte[] receiveBuffer = null;
                    NetworkingBackend.readMessage(pollGroup.type, messagePtrs[messageIndex], out receiveBuffer, out int receivedBytesCount, out ConnectionId connectionId);
                    NetworkingBackend.releaseMessage(pollGroup.type, messagePtrs[messageIndex]);
                    messagePtrs[messageIndex] = IntPtr.Zero;

                    PacketChunk packet = new PacketChunk(receiveBuffer);

                    switch (packet.getPacketType())
                    {
                        case PacketType.ClientToServerAuth:
                        {
                            int pendingIndex = pendingConnections.FindIndex((x => x.connectionId == connectionId));
                            if (pendingIndex == -1)
                                continue;

                            PendingConnection pending = pendingConnections[pendingIndex];
                            pendingConnections.RemoveAt(pendingIndex);

                            bool authSuccess;
                            if (pending.skipAuth)
                            {
                                authSuccess = true;
                            }
                            else
                            {
                                ClientToServerPacketAuth authPacket;
                                fixed (byte* bytePtr = receiveBuffer)
                                    authPacket = *((ClientToServerPacketAuth*)bytePtr);

                                authSuccess = true;
                                for (int i = 0; i < authSecret.Length; i++)
                                {
                                    if (authSecret[i] != authPacket.secret[i])
                                    {
                                        authSuccess = false;
                                        break;
                                    }
                                }
                            }

                            if (!authSuccess)
                            {
                                Debug.Log("Client failed to authenticate!");
                                NetworkingBackend.closeConnection(connectionId, "auth failed");
                                continue;
                            }

                            ClientData client = new ClientData();
                            client.connectionId = connectionId;
                            clients.Add(client);

                            Debug.Log("Player authenticated (skipAuth=" + pending.skipAuth + ")");
                            break;
                        }
                        case PacketType.ClientToServer:
                        {
                            ClientToServerPacket lastReceivedPacket;
                            fixed (byte* bytePtr = receiveBuffer)
                                lastReceivedPacket = *((ClientToServerPacket*)bytePtr);

                            ClientData client = getClientByConnectionId(connectionId);
                            if (client == null)
                                continue;

                            if (client.lastReceivedPacket.playerData.id != NetUid.Empty && client.lastReceivedPacket.playerData.id != lastReceivedPacket.playerData.id)
                            {
                                Debug.Log("connection changed player id, that's not allowed!");
                                NetworkingBackend.closeConnection(connectionId, "connection changed player id");
                                clients.Remove(client);
                                continue;
                            }

                            client.lastReceivedPacket = lastReceivedPacket;
                            client.lastMessageReceivedMs = getTimeMs();
                            client.connectionId = connectionId;

                            break;
                        }
                    }
                }
            }

            long now = getTimeMs();

            long elapsed = now - lastSendStartTimeMs;
            if (elapsed < updateIntervalMs)
                return;

            lastSendStartTimeMs = getTimeMs();

            if (clients.Count == 0)
            {
                // With no clients, no anchors are referenced — drop everything but (optionally) the
                // most recent snapshot. New clients start with NoAnchor regardless.
                if (packetIndex > 0)
                    evictSnapshotsOlderThan(packetIndex);
                return;
            }

            bool allClientsUpToDate = true;
            for (int i = 0; i < clients.Count;)
            {
                ClientData clientData = clients[i];
                if (clientData.lastReceivedPacket.lastWorldPacketIndex != packetIndex)
                {
                    long timeSinceLastDumpMs = now - lastDumpTimeMs;
                    if (timeSinceLastDumpMs > 1000 * 25)
                    {
                        Debug.Log("Client is lagging too much, kicking...");
                        NetworkingBackend.closeConnection(clientData.connectionId, "kicked for lagging");
                        clients.RemoveAt(i);
                        continue;
                    }

                    allClientsUpToDate = false;
                }

                i++;
            }

            if (packetIndex == 0 || allClientsUpToDate)
                newDumpRequested = true;

            if (lastDump == null)
                return;

            if (lastDump != worldData)
            {
                worldData = lastDump;
                packetIndex++;
                addSnapshot(packetIndex, worldData);
            }


            foreach (ClientData clientData in clients)
            {
                if (clientData.lastWorldPacketSentIndex != packetIndex)
                {
                    Snapshot anchor = null;
                    if (clientData.lastReceivedPacket.lastWorldPacketIndex != 0)
                        snapshots.TryGetValue(clientData.lastReceivedPacket.lastWorldPacketIndex, out anchor);

                    uint anchorId = WorldDiff.NoAnchor;
                    byte[] anchorBytes = null;
                    Dictionary<ulong, ContentDefinedChunkRef> anchorIndex = null;

                    if (anchor != null)
                    {
                        anchorId = clientData.lastReceivedPacket.lastWorldPacketIndex;
                        anchorBytes = anchor.bytes;
                        anchorIndex = anchor.chunkIndex;
                    }

                    byte[] diffBlob = WorldDiff.encode(anchorId, anchorBytes, anchorIndex, packetIndex, worldData, snapshotChunkScratch);

                    List<PacketChunk> diffPackets = dataToPackets(diffBlob, packetIndex);
                    int diffBytesSent = 0;

                    for (int packetChunkIndex = 0; packetChunkIndex < diffPackets.Count; packetChunkIndex++)
                    {
                        PacketChunk worldPacket = diffPackets[packetChunkIndex];

                        // Stamp lastActionProcessedByServerIndex so the client can match it to the
                        // world state this packet reconstructs.
                        fixed (byte* bytePtr = worldPacket.bytes)
                        {
                            PacketChunkHeader* header = (PacketChunkHeader*)bytePtr;
                            header->lastActionProcessedByServerIndex = clientData.lastActionProcessedByServerIndex;
                        }

                        MessageWrapper message = NetworkingBackend.allocateMessage(clientData.connectionId.type, worldPacket.allBytesTuple.Item2);
                        message.writeData(worldPacket.allBytesTuple.Item1, worldPacket.allBytesTuple.Item2);

                        message.flags = PacketFlags.Reliable;
                        message.connection = clientData.connectionId;
                        message.lane = RELIABLE_LANE;

                        if (packetChunkIndex == diffPackets.Count - 1)
                            message.flags |= PacketFlags.NoNagle;

                        NetworkingBackend.sendMessage(message);
                        diffBytesSent += worldPacket.allBytesTuple.Item2;
                    }

                    clientData.lastWorldPacketSentIndex = packetIndex;
                    clientData.lastDiffBytesSent = diffBytesSent;
                    clientData.lastDiffUncompressedBytes = worldData.Length;
                }

                // and then the individual update
                {
                    PacketChunk packet = new PacketChunk(sizeof(ServerToClientPacket));

                    fixed (byte* bytePtr = packet.bytes)
                    {
                        ServerToClientPacket* serverToClientPacket = (ServerToClientPacket*)bytePtr;
                        serverToClientPacket->packetType = PacketType.ServerToClient;
                        serverToClientPacket->packetIndex = serverToClientPacketIndex;
                        serverToClientPacket->lastActionReceivedByServerIndex = clientData.lastActionReceivedByServerIndex;
                        serverToClientPacket->playerDataList = new BasicPlayerDataList(lastPlayerDataList);
                        serverToClientPacket->serverCurrentTick = lastKnownServerTick;

                        for (int i = 0; i < Constants.MAX_PLAYER_COUNT; i++)
                        {
                            NetUid playerId = lastPlayerDataList.get(i).id;
                            serverToClientPacket->setPlayerOnline(i, isPlayerOnline(playerId));
                        }
                    }

                    NetworkingBackend.sendBytes(clientData.connectionId, packet.allBytesTuple.Item1, packet.allBytesTuple.Item2, UNRELIABLE_LANE, PacketFlags.Unreliable | PacketFlags.NoNagle);
                }
            }

            uint minAcked = uint.MaxValue;
            foreach (ClientData cd in clients)
            {
                uint id = cd.lastReceivedPacket.lastWorldPacketIndex;
                minAcked = Math.Min(minAcked, id);
            }
            evictSnapshotsOlderThan(minAcked);

            serverToClientPacketIndex++;
        }

        public override bool isPlayerOnline(NetUid playerId)
        {
            if (playerId == PiperMain.p.game.playerId)
                return true;

            foreach (ClientData c in clients)
            {
                if (c.lastReceivedPacket.playerData.id.Equals(playerId))
                    return true;
            }

            return false;
        }
    }
}
