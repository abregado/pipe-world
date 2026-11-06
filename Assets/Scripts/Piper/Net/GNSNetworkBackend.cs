using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using AOT;
using GameNetworkingSockets;
using Native = GameNetworkingSockets.GameNetworkingSocketsNative;

namespace PipeWorld.Piper
{
    public unsafe class GNSNetworkBackend
    {
        public static void init()
        {
    #if UNITY_EDITOR
            // in builds the DLLs are copied into the right folder, but the editor is dumb apparently
            NativeFuncs.SetDllDirectory(System.IO.Path.Combine(UnityEngine.Application.dataPath, "Plugins", "GameNetworkingSockets", "Plugins"));
    #endif

            NetworkingBackend.gns = new GNSNetworkBackend();
            NetworkingBackend.gns.setup();
        }


        private IntPtr sockets;
        private IntPtr utils;

        private static FnSteamNetConnectionStatusChanged statusChangedDelegate;

        private static ConnectionState mapState(ESteamNetworkingConnectionState s)
        {
            switch (s)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_None: return ConnectionState.None;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting: return ConnectionState.Connecting;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_FindingRoute: return ConnectionState.FindingRoute;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected: return ConnectionState.Connected;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer: return ConnectionState.ClosedByPeer;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally: return ConnectionState.ProblemDetectedLocally;
                default: return ConnectionState.Other;
            }
        }

        [MonoPInvokeCallback(typeof(FnSteamNetConnectionStatusChanged))]
        private static void onConnectionStatusChanged(IntPtr pCallback)
        {
            SteamNetConnectionStatusChangedCallback_t data = *(SteamNetConnectionStatusChangedCallback_t*)pCallback;

            ConnectionStatusChangedEvent ev = new ConnectionStatusChangedEvent
            {
                BackendType = NetworkingBackendType.NonSteam,
                connection = new ConnectionId(data.m_hConn),
                listenSocket = data.m_info.m_hListenSocket == HSteamListenSocket.Invalid
                    ? ListenSocket.Invalid
                    : new ListenSocket(data.m_info.m_hListenSocket),
                oldState = mapState(data.m_eOldState),
                newState = mapState(data.m_info.m_eState),
            };

            NetworkingBackend.fireConnectionStatusChanged(ev);
        }

        public void setup()
        {
            IntPtr errMsgBuf = Marshal.AllocHGlobal(1024);
            try
            {
                bool ok = Native.GameNetworkingSockets_Init(IntPtr.Zero, errMsgBuf);
                if (!ok)
                {
                    string err = Marshal.PtrToStringAnsi(errMsgBuf);
                    throw new Exception("GameNetworkingSockets_Init failed: " + err);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(errMsgBuf);
            }

            sockets = Native.SteamAPI_SteamNetworkingSockets_v009();
            utils = Native.SteamAPI_SteamNetworkingUtils_v003();

            statusChangedDelegate = onConnectionStatusChanged;
            Native.SteamAPI_ISteamNetworkingUtils_SetGlobalCallback_SteamNetConnectionStatusChanged(utils, statusChangedDelegate);

            void setConfig(ESteamNetworkingConfigValue key, int val)
            {
                int[] blah = new int[1];
                blah[0] = val;
                fixed (int* ptr = blah)
                {
                    Native.SteamAPI_ISteamNetworkingUtils_SetConfigValue(utils, key,
                        ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global,
                        IntPtr.Zero,
                        ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                        (IntPtr)ptr);
                }
            }

            int bufferSize = 1024 * 1024 * 100;
            int sendRate = 1024 * 256;

            setConfig(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize, bufferSize);
            setConfig(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMin, sendRate);
            setConfig(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMax, sendRate);
            setConfig(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_RecvBufferSize, bufferSize);
            setConfig(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_RecvBufferMessages, 10000);
        }

        public void cleanup()
        {
            Native.GameNetworkingSockets_Kill();
            statusChangedDelegate = null;
            sockets = IntPtr.Zero;
            utils = IntPtr.Zero;
        }

        public void runCallbacks()
        {
            if (sockets != IntPtr.Zero)
                Native.SteamAPI_ISteamNetworkingSockets_RunCallbacks(sockets);
        }

        public ListenSocket createListenSocketIP(ushort port)
        {
            SteamNetworkingIPAddr localAddr = new SteamNetworkingIPAddr();
            Native.SteamAPI_SteamNetworkingIPAddr_Clear(ref localAddr);
            localAddr.m_port = port;
            return new ListenSocket(Native.SteamAPI_ISteamNetworkingSockets_CreateListenSocketIP(sockets, ref localAddr, 0, IntPtr.Zero));
        }

        public PollGroup createPollGroup()
        {
            return new PollGroup(Native.SteamAPI_ISteamNetworkingSockets_CreatePollGroup(sockets));
        }

        public ClientConnection connectByIP(string addressString, ushort port)
        {
            IPAddress address = Dns.GetHostAddresses(addressString).First(x => x.AddressFamily == AddressFamily.InterNetwork);
            byte[] b = address.GetAddressBytes();
            Array.Reverse(b);
            uint ipUint = BitConverter.ToUInt32(b, 0);

            SteamNetworkingIPAddr serverAddress = new SteamNetworkingIPAddr();
            Native.SteamAPI_SteamNetworkingIPAddr_SetIPv4(ref serverAddress, ipUint, port);
            return new ClientConnection(Native.SteamAPI_ISteamNetworkingSockets_ConnectByIPAddress(sockets, ref serverAddress, 0, IntPtr.Zero));
        }

        public void closeListenSocket(ListenSocket s)
        {
            SUtil.ReleaseAssert(s.type == NetworkingBackendType.NonSteam);
            Native.SteamAPI_ISteamNetworkingSockets_CloseListenSocket(sockets, s.gnsListen);
        }

        public void destroyPollGroup(PollGroup g)
        {
            SUtil.ReleaseAssert(g.type == NetworkingBackendType.NonSteam);
            Native.SteamAPI_ISteamNetworkingSockets_DestroyPollGroup(sockets, g.gnsPoll);
        }

        public void closeConnection(ConnectionId c, string reason)
        {
            SUtil.ReleaseAssert(c.type == NetworkingBackendType.NonSteam);
            Native.SteamAPI_ISteamNetworkingSockets_CloseConnection(sockets, c.gnsConnection, 0, reason, false);
        }

        public void closeClientConnection(ClientConnection c, string reason)
        {
            SUtil.ReleaseAssert(c.type == NetworkingBackendType.NonSteam);
            Native.SteamAPI_ISteamNetworkingSockets_CloseConnection(sockets, c.gnsConn, 0, reason, false);
        }

        public void acceptConnection(ConnectionId c)
        {
            SUtil.ReleaseAssert(c.type == NetworkingBackendType.NonSteam);
            EResult result = Native.SteamAPI_ISteamNetworkingSockets_AcceptConnection(sockets, c.gnsConnection);
            SUtil.ReleaseAssert(result == EResult.k_EResultOK, "acceptConnection failed: " + result);
        }

        public void setConnectionPollGroup(ConnectionId c, PollGroup g)
        {
            SUtil.ReleaseAssert(c.type == NetworkingBackendType.NonSteam && g.type == NetworkingBackendType.NonSteam);
            Native.SteamAPI_ISteamNetworkingSockets_SetConnectionPollGroup(sockets, c.gnsConnection, g.gnsPoll);
        }

        public void configureConnectionLanes(ConnectionId c, int nLanes, int[] lanePriorities, ushort[] laneWeights)
        {
            SUtil.ReleaseAssert(c.type == NetworkingBackendType.NonSteam);
            EResult result = Native.SteamAPI_ISteamNetworkingSockets_ConfigureConnectionLanes(sockets, c.gnsConnection, nLanes, lanePriorities, laneWeights);
            SUtil.ReleaseAssert(result == EResult.k_EResultOK, "configureConnectionLanes failed: " + result);
        }

        public int receiveMessagesOnPollGroup(PollGroup g, IntPtr[] outMessages)
        {
            SUtil.ReleaseAssert(g.type == NetworkingBackendType.NonSteam);
            return Native.SteamAPI_ISteamNetworkingSockets_ReceiveMessagesOnPollGroup(sockets, g.gnsPoll, outMessages, outMessages.Length);
        }

        public int receiveMessagesOnConnection(ClientConnection c, IntPtr[] outMessages)
        {
            SUtil.ReleaseAssert(c.type == NetworkingBackendType.NonSteam);
            return Native.SteamAPI_ISteamNetworkingSockets_ReceiveMessagesOnConnection(sockets, c.gnsConn, outMessages, outMessages.Length);
        }

        public void readMessage(IntPtr msgPtr, out byte[] buffer, out int length, out ConnectionId sender)
        {
            SteamNetworkingMessage_t* message = (SteamNetworkingMessage_t*)msgPtr;
            buffer = new byte[message->m_cbSize];
            fixed (byte* bufferPtr = buffer)
            {
                NativeFuncs.memcpy((IntPtr)bufferPtr, message->m_pData, message->m_cbSize);
            }
            length = message->m_cbSize;
            sender = new ConnectionId(message->m_conn);
        }

        public void releaseMessage(IntPtr msgPtr)
        {
            Native.SteamAPI_SteamNetworkingMessage_t_Release(msgPtr);
        }

        public MessageWrapper allocateMessage(int length)
        {
            return new MessageWrapper { type = NetworkingBackendType.NonSteam, msgPtr = Native.SteamAPI_ISteamNetworkingUtils_AllocateMessage(utils, length) };
        }

        public void sendMessages(MessageWrapper[] messages)
        {
            IntPtr[] ptrs = new IntPtr[messages.Length];
            for (int i = 0; i < messages.Length; i++)
            {
                SUtil.ReleaseAssert(messages[i].type == NetworkingBackendType.NonSteam);
                ptrs[i] = messages[i].msgPtr;
            }
            long[] results = new long[messages.Length];
            Native.SteamAPI_ISteamNetworkingSockets_SendMessages(sockets, messages.Length, ptrs, results);
        }

        public void sendMessage(MessageWrapper message)
        {
            SUtil.ReleaseAssert(message.type == NetworkingBackendType.NonSteam);
            long[] results = new long[1];
            Native.SteamAPI_ISteamNetworkingSockets_SendMessages(sockets, 1, new IntPtr[] { message.msgPtr }, results);
        }

        public void sendBytes(ConnectionId c, byte[] buffer, int length, int lane, PacketFlags flags)
        {
            SUtil.ReleaseAssert(c.type == NetworkingBackendType.NonSteam);

            MessageWrapper message = allocateMessage(length);
            message.flags = flags;
            message.connection = c;
            message.lane = (ushort)lane;

            fixed (byte* ptr = buffer)
                NativeFuncs.memcpy(message.getDataIntPtr(), (IntPtr)ptr, length);

            sendMessage(message);
        }
    }
}
