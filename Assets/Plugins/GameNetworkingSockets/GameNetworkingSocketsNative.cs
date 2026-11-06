using System;
using System.Runtime.InteropServices;

namespace GameNetworkingSockets
{
    public struct HSteamNetConnection : IEquatable<HSteamNetConnection>
    {
        public uint value;
        public static HSteamNetConnection Invalid => new HSteamNetConnection { value = 0 };
        public bool Equals(HSteamNetConnection other) => value == other.value;
        public override bool Equals(object obj) => obj is HSteamNetConnection o && Equals(o);
        public override int GetHashCode() => (int)value;
        public static bool operator ==(HSteamNetConnection a, HSteamNetConnection b) => a.value == b.value;
        public static bool operator !=(HSteamNetConnection a, HSteamNetConnection b) => a.value != b.value;
    }

    public struct HSteamListenSocket : IEquatable<HSteamListenSocket>
    {
        public uint value;
        public static HSteamListenSocket Invalid => new HSteamListenSocket { value = 0 };
        public bool Equals(HSteamListenSocket other) => value == other.value;
        public override bool Equals(object obj) => obj is HSteamListenSocket o && Equals(o);
        public override int GetHashCode() => (int)value;
        public static bool operator ==(HSteamListenSocket a, HSteamListenSocket b) => a.value == b.value;
        public static bool operator !=(HSteamListenSocket a, HSteamListenSocket b) => a.value != b.value;
    }

    public struct HSteamNetPollGroup : IEquatable<HSteamNetPollGroup>
    {
        public uint value;
        public static HSteamNetPollGroup Invalid => new HSteamNetPollGroup { value = 0 };
        public bool Equals(HSteamNetPollGroup other) => value == other.value;
        public override bool Equals(object obj) => obj is HSteamNetPollGroup o && Equals(o);
        public override int GetHashCode() => (int)value;
        public static bool operator ==(HSteamNetPollGroup a, HSteamNetPollGroup b) => a.value == b.value;
        public static bool operator !=(HSteamNetPollGroup a, HSteamNetPollGroup b) => a.value != b.value;
    }

    public enum EResult : int
    {
        k_EResultNone = 0,
        k_EResultOK = 1,
    }

    public enum ESteamNetworkingConnectionState : int
    {
        k_ESteamNetworkingConnectionState_None = 0,
        k_ESteamNetworkingConnectionState_Connecting = 1,
        k_ESteamNetworkingConnectionState_FindingRoute = 2,
        k_ESteamNetworkingConnectionState_Connected = 3,
        k_ESteamNetworkingConnectionState_ClosedByPeer = 4,
        k_ESteamNetworkingConnectionState_ProblemDetectedLocally = 5,
    }

    public enum ESteamNetworkingConfigScope : int
    {
        k_ESteamNetworkingConfig_Global = 1,
    }

    public enum ESteamNetworkingConfigDataType : int
    {
        k_ESteamNetworkingConfig_Int32 = 1,
    }

    public enum ESteamNetworkingConfigValue : int
    {
        k_ESteamNetworkingConfig_SendBufferSize = 9,
        k_ESteamNetworkingConfig_SendRateMin = 10,
        k_ESteamNetworkingConfig_SendRateMax = 11,
        k_ESteamNetworkingConfig_RecvBufferSize = 47,
        k_ESteamNetworkingConfig_RecvBufferMessages = 48,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct SteamNetworkingIPAddr
    {
        public fixed byte m_ipv6[16];
        public ushort m_port;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct SteamNetworkingIdentity
    {
        public int m_eType;
        public int m_cbSize;
        public fixed byte m_data[128];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct SteamNetConnectionInfo_t
    {
        public SteamNetworkingIdentity m_identityRemote;
        public long m_nUserData;
        public HSteamListenSocket m_hListenSocket;
        public SteamNetworkingIPAddr m_addrRemote;
        public ushort m__pad1;
        public uint m_idPOPRemote;
        public uint m_idPOPRelay;
        public ESteamNetworkingConnectionState m_eState;
        public int m_eEndReason;
        public fixed byte m_szEndDebug[128];
        public fixed byte m_szConnectionDescription[128];
        public int m_nFlags;
        public fixed uint reserved[63];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct SteamNetConnectionStatusChangedCallback_t
    {
        public HSteamNetConnection m_hConn;
        public SteamNetConnectionInfo_t m_info;
        public ESteamNetworkingConnectionState m_eOldState;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct SteamNetworkingMessage_t
    {
        public IntPtr m_pData;
        public int m_cbSize;
        public HSteamNetConnection m_conn;
        public SteamNetworkingIdentity m_identityPeer;
        public long m_nConnUserData;
        public long m_usecTimeReceived;
        public long m_nMessageNumber;
        public IntPtr m_pfnFreeData;
        public IntPtr m_pfnRelease;
        public int m_nChannel;
        public int m_nFlags;
        public long m_nUserData;
        public ushort m_idxLane;
        public ushort _pad1__;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void FnSteamNetConnectionStatusChanged(IntPtr pCallback);

    public static class GameNetworkingSocketsNative
    {
        private const string DLL = "GameNetworkingSockets";

        [DllImport(DLL)]
        public static extern bool GameNetworkingSockets_Init(IntPtr pIdentity, IntPtr errMsg);

        [DllImport(DLL)]
        public static extern void GameNetworkingSockets_Kill();

        [DllImport(DLL)]
        public static extern IntPtr SteamAPI_SteamNetworkingSockets_v009();

        [DllImport(DLL)]
        public static extern IntPtr SteamAPI_SteamNetworkingUtils_v003();

        [DllImport(DLL)]
        public static extern HSteamListenSocket SteamAPI_ISteamNetworkingSockets_CreateListenSocketIP(IntPtr self, ref SteamNetworkingIPAddr localAddress, int nOptions, IntPtr pOptions);

        [DllImport(DLL)]
        public static extern HSteamNetConnection SteamAPI_ISteamNetworkingSockets_ConnectByIPAddress(IntPtr self, ref SteamNetworkingIPAddr address, int nOptions, IntPtr pOptions);

        [DllImport(DLL)]
        public static extern EResult SteamAPI_ISteamNetworkingSockets_AcceptConnection(IntPtr self, HSteamNetConnection hConn);

        [DllImport(DLL, CharSet = CharSet.Ansi)]
        public static extern bool SteamAPI_ISteamNetworkingSockets_CloseConnection(IntPtr self, HSteamNetConnection hPeer, int nReason, string pszDebug, bool bEnableLinger);

        [DllImport(DLL)]
        public static extern bool SteamAPI_ISteamNetworkingSockets_CloseListenSocket(IntPtr self, HSteamListenSocket hSocket);

        [DllImport(DLL)]
        public static extern HSteamNetPollGroup SteamAPI_ISteamNetworkingSockets_CreatePollGroup(IntPtr self);

        [DllImport(DLL)]
        public static extern bool SteamAPI_ISteamNetworkingSockets_DestroyPollGroup(IntPtr self, HSteamNetPollGroup hPollGroup);

        [DllImport(DLL)]
        public static extern bool SteamAPI_ISteamNetworkingSockets_SetConnectionPollGroup(IntPtr self, HSteamNetConnection hConn, HSteamNetPollGroup hPollGroup);

        [DllImport(DLL)]
        public static extern int SteamAPI_ISteamNetworkingSockets_ReceiveMessagesOnPollGroup(IntPtr self, HSteamNetPollGroup hPollGroup, IntPtr[] ppOutMessages, int nMaxMessages);

        [DllImport(DLL)]
        public static extern int SteamAPI_ISteamNetworkingSockets_ReceiveMessagesOnConnection(IntPtr self, HSteamNetConnection hConn, IntPtr[] ppOutMessages, int nMaxMessages);

        [DllImport(DLL)]
        public static extern void SteamAPI_ISteamNetworkingSockets_SendMessages(IntPtr self, int nMessages, IntPtr[] pMessages, long[] pOutMessageNumberOrResult);

        [DllImport(DLL)]
        public static extern EResult SteamAPI_ISteamNetworkingSockets_ConfigureConnectionLanes(IntPtr self, HSteamNetConnection hConn, int nNumLanes, int[] pLanePriorities, ushort[] pLaneWeights);

        [DllImport(DLL)]
        public static extern void SteamAPI_ISteamNetworkingSockets_RunCallbacks(IntPtr self);

        [DllImport(DLL)]
        public static extern IntPtr SteamAPI_ISteamNetworkingUtils_AllocateMessage(IntPtr self, int cbAllocateBuffer);

        [DllImport(DLL)]
        public static extern bool SteamAPI_ISteamNetworkingUtils_SetGlobalCallback_SteamNetConnectionStatusChanged(IntPtr self, FnSteamNetConnectionStatusChanged fnCallback);

        [DllImport(DLL)]
        public static extern bool SteamAPI_ISteamNetworkingUtils_SetConfigValue(IntPtr self, ESteamNetworkingConfigValue eValue, ESteamNetworkingConfigScope eScopeType, IntPtr scopeObj, ESteamNetworkingConfigDataType eDataType, IntPtr pArg);

        [DllImport(DLL)]
        public static extern void SteamAPI_SteamNetworkingMessage_t_Release(IntPtr self);

        [DllImport(DLL)]
        public static extern void SteamAPI_SteamNetworkingIPAddr_Clear(ref SteamNetworkingIPAddr self);

        [DllImport(DLL)]
        public static extern void SteamAPI_SteamNetworkingIPAddr_SetIPv4(ref SteamNetworkingIPAddr self, uint nIP, ushort nPort);
    }
}
