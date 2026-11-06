using System;
using GameNetworkingSockets;

namespace PipeWorld.Piper
{
    public enum NetworkingBackendType
    {
        Invalid = 0,
        NonSteam,
    }

    public enum ConnectionState
    {
        None = 0,
        Connecting,
        FindingRoute,
        Connected,
        ClosedByPeer,
        ProblemDetectedLocally,
        Other,
    }

    public struct ConnectionId : IEquatable<ConnectionId>
    {
        public NetworkingBackendType type;
        public HSteamNetConnection gnsConnection;

        public ConnectionId(HSteamNetConnection connection)
        {
            gnsConnection = connection;
            type = NetworkingBackendType.NonSteam;
        }

        public bool Equals(ConnectionId other) => type == other.type && gnsConnection.Equals(other.gnsConnection);
        public override bool Equals(object obj) => obj is ConnectionId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)type, gnsConnection);
        public static bool operator ==(ConnectionId left, ConnectionId right) => left.Equals(right);
        public static bool operator !=(ConnectionId left, ConnectionId right) => !left.Equals(right);
    }

    public struct ListenSocket : IEquatable<ListenSocket>
    {
        public NetworkingBackendType type;
        public HSteamListenSocket gnsListen;

        public static ListenSocket Invalid => new ListenSocket { type = NetworkingBackendType.Invalid, gnsListen = HSteamListenSocket.Invalid };

        public ListenSocket(HSteamListenSocket s)
        {
            type = NetworkingBackendType.NonSteam;
            gnsListen = s;
        }

        public bool isInvalid => type == NetworkingBackendType.Invalid || gnsListen == HSteamListenSocket.Invalid;

        public bool Equals(ListenSocket other) => type == other.type && gnsListen.Equals(other.gnsListen);
        public override bool Equals(object obj) => obj is ListenSocket o && Equals(o);
        public override int GetHashCode() => HashCode.Combine((int)type, gnsListen);
        public static bool operator ==(ListenSocket a, ListenSocket b) => a.Equals(b);
        public static bool operator !=(ListenSocket a, ListenSocket b) => !a.Equals(b);
    }

    public struct PollGroup : IEquatable<PollGroup>
    {
        public NetworkingBackendType type;
        public HSteamNetPollGroup gnsPoll;

        public static PollGroup Invalid => new PollGroup { type = NetworkingBackendType.Invalid, gnsPoll = HSteamNetPollGroup.Invalid };

        public PollGroup(HSteamNetPollGroup g)
        {
            type = NetworkingBackendType.NonSteam;
            gnsPoll = g;
        }

        public bool isInvalid => type == NetworkingBackendType.Invalid || gnsPoll == HSteamNetPollGroup.Invalid;

        public bool Equals(PollGroup other) => type == other.type && gnsPoll.Equals(other.gnsPoll);
        public override bool Equals(object obj) => obj is PollGroup o && Equals(o);
        public override int GetHashCode() => HashCode.Combine((int)type, gnsPoll);
        public static bool operator ==(PollGroup a, PollGroup b) => a.Equals(b);
        public static bool operator !=(PollGroup a, PollGroup b) => !a.Equals(b);
    }

    public struct ClientConnection : IEquatable<ClientConnection>
    {
        public NetworkingBackendType type;
        public HSteamNetConnection gnsConn;

        public static ClientConnection Invalid => new ClientConnection { type = NetworkingBackendType.Invalid, gnsConn = HSteamNetConnection.Invalid };

        public ClientConnection(HSteamNetConnection c)
        {
            type = NetworkingBackendType.NonSteam;
            gnsConn = c;
        }

        public bool isInvalid => type == NetworkingBackendType.Invalid || gnsConn == HSteamNetConnection.Invalid;

        public ConnectionId asConnectionId() => new ConnectionId(gnsConn);

        public bool Equals(ClientConnection other) => type == other.type && gnsConn.Equals(other.gnsConn);
        public override bool Equals(object obj) => obj is ClientConnection o && Equals(o);
        public override int GetHashCode() => HashCode.Combine((int)type, gnsConn);
        public static bool operator ==(ClientConnection a, ClientConnection b) => a.Equals(b);
        public static bool operator !=(ClientConnection a, ClientConnection b) => !a.Equals(b);
    }

    [Flags]
    public enum PacketFlags
    {
        Unreliable = 0,
        NoNagle = 1,
        NoDelay = 4,
        Reliable = 8,
        UseCurrentThread = 16,
        AutoRestartBrokenSession = 32,
    }

    public unsafe struct MessageWrapper
    {
        public NetworkingBackendType type;
        public IntPtr msgPtr;

        private SteamNetworkingMessage_t* message => (SteamNetworkingMessage_t*)msgPtr;

        public PacketFlags flags
        {
            get => (PacketFlags)message->m_nFlags;
            set => message->m_nFlags = (int)value;
        }

        public ConnectionId connection
        {
            get => new ConnectionId(message->m_conn);
            set => message->m_conn = value.gnsConnection;
        }

        public ushort lane
        {
            get => message->m_idxLane;
            set => message->m_idxLane = value;
        }

        public void writeData(byte[] buffer, int length = -1)
        {
            if (length == -1)
                length = buffer.Length;

            fixed (byte* ptr = buffer)
                NativeFuncs.memcpy(getDataIntPtr(), (IntPtr)ptr, length);
        }

        public IntPtr getDataIntPtr() => message->m_pData;

        public int getDataLength() => message->m_cbSize;
    }

    public struct ConnectionStatusChangedEvent
    {
        public NetworkingBackendType BackendType;
        public ConnectionId connection;
        public ListenSocket listenSocket;
        public ConnectionState oldState;
        public ConnectionState newState;
    }

    public static class NetworkingBackend
    {
        public static GNSNetworkBackend gns;

        public static event Action<ConnectionStatusChangedEvent> connectionStatusChanged;
        internal static void fireConnectionStatusChanged(ConnectionStatusChangedEvent e) => connectionStatusChanged?.Invoke(e);

        public static void closeConnection(ConnectionId c, string reason) => gns.closeConnection(c, reason);

        public static void closeClientConnection(ClientConnection c, string reason) => gns.closeClientConnection(c, reason);

        public static void closeListenSocket(ListenSocket s) => gns.closeListenSocket(s);

        public static void destroyPollGroup(PollGroup g) => gns.destroyPollGroup(g);

        public static void acceptConnection(ConnectionId c) => gns.acceptConnection(c);

        public static void setConnectionPollGroup(ConnectionId c, PollGroup g) => gns.setConnectionPollGroup(c, g);

        public static void configureConnectionLanes(ConnectionId c, int nLanes, int[] priorities, ushort[] weights) =>
            gns.configureConnectionLanes(c, nLanes, priorities, weights);

        public static int receiveMessagesOnPollGroup(PollGroup g, IntPtr[] outMessages) => gns.receiveMessagesOnPollGroup(g, outMessages);

        public static int receiveMessagesOnConnection(ClientConnection c, IntPtr[] outMessages) => gns.receiveMessagesOnConnection(c, outMessages);

        public static void readMessage(NetworkingBackendType b, IntPtr msgPtr, out byte[] buffer, out int length, out ConnectionId sender) =>
            gns.readMessage(msgPtr, out buffer, out length, out sender);

        public static void releaseMessage(NetworkingBackendType b, IntPtr msgPtr) => gns.releaseMessage(msgPtr);

        public static MessageWrapper allocateMessage(NetworkingBackendType b, int length) => gns.allocateMessage(length);

        public static void sendMessages(MessageWrapper[] messages) => gns.sendMessages(messages);

        public static void sendMessage(MessageWrapper message) => gns.sendMessage(message);

        public static void sendBytes(ConnectionId c, byte[] buffer, int length, int lane, PacketFlags flags) =>
            gns.sendBytes(c, buffer, length, lane, flags);
    }
}
