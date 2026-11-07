using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace PipeWorld.Piper
{
    public struct AllocatorContextData
    {
        public int playerIndex;
        public uint nextPropId;
        public uint nextEntityId;
    }

    public class AllocatorContext
    {
        public AllocatorContextData data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Prop
    {
        public SlabHeader slabHeader;
        public PropPrototypeId prototypeId;
        public Vec3f position;
        public Vec3f eulerRotation;
        public PropExtraData data;

        // Building props belong to a building (0 for outdoor props) and are attached to one of its faces. Position and rotation
        // are derived from the face and the 0-1 coordinates each time the building changes, so a prop follows its face when it is resized.
        public uint building;
        public uint faceRef;
        public float faceU;
        public float faceV;
        public BuildingFace face;

        public PropHandle getHandleToSelf()
        {
            SlabHandle slabHandle = PropAllocator.fabricateHandleFromHeader(slabHeader);
            return PropAllocator.convertHandle(slabHandle);
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct PropExtraData
    {
        [FieldOffset(0)] public CollectableItemExtraData collectable;
        [FieldOffset(0)] public DoorExtraData door;
        [FieldOffset(0)] public RefreshableItemExtraData refreshable;
        [FieldOffset(0)] public LampExtraData lamp;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CollectableItemExtraData
    {
        public InventoryItemPrototypeId givenItem;
        public int amount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DoorExtraData
    {
        public InventoryItemPrototypeId requiredItem;
        public bool doorOpen;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RefreshableItemExtraData
    {
        public InventoryItemPrototypeId givenItem;
        public int amountStored;
        public int maxStored;
        public ulong nextRefreshTick;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LampExtraData
    {
        public bool on;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NetUid : IEquatable<NetUid>
    {
        public fixed byte data[16];

        public NetUid(Guid guid)
        {
            byte[] temp = guid.ToByteArray();
            for (int i = 0; i < 16; i++)
                data[i] = temp[i];
        }

        public NetUid(string guid) : this(Guid.Parse(guid)) { }

        public bool Equals(NetUid other)
        {
            for (int i = 0; i < 16; i++)
            {
                if (data[i] != other.data[i])
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj) => obj is NetUid other && Equals(other);
        public static bool operator ==(NetUid a, NetUid b) { return a.Equals(b); }
        public static bool operator !=(NetUid a, NetUid b) { return !a.Equals(b); }

        public override int GetHashCode()
        {
            int hash = data[0].GetHashCode();
            for (int i = 1; i < 16; i++)
                hash = HashCode.Combine(hash, data[i].GetHashCode());
            return hash;
        }

        public override string ToString()
        {
            byte[] temp = new byte[16];
            for (int i = 0; i < 16; i++)
                temp[i] = data[i];
            return new Guid(temp).ToString();
        }

        public static readonly NetUid Empty = new NetUid(Guid.Empty);
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct WorldData
    {
        public bool dirty;
        public ulong currentTick;
        public PlayerDataList players;
        public Inventory inventory;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Inventory
    {
        private fixed int _count[(int)InventoryItemPrototypeId.MAX_VALUE];
        private fixed byte _seen[((int)InventoryItemPrototypeId.MAX_VALUE / 8) + 1];

        public int getCount(InventoryItemPrototypeId id) { return _count[(int)id]; }
        public void setCount(InventoryItemPrototypeId id, int value) { _count[(int)id] = value; if (value > 0) setSeen(id, true); }
        public void adjustCount(InventoryItemPrototypeId id, int value) { setCount(id, getCount(id) + value); }
        public bool getSeen(InventoryItemPrototypeId id) { return Bit.get(_seen[((int)id) / 8], ((int)id) % 8); }
        public void setSeen(InventoryItemPrototypeId id, bool value) { Bit.set(ref _seen[((int)id) / 8], ((int)id) % 8, value); }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct BasicPlayerDataList
    {
        private fixed byte _playerData[BasicPlayerData.SizeOf * Constants.MAX_PLAYER_COUNT];

        public BasicPlayerDataList(PlayerDataList fatList)
        {
            for (int i = 0; i < Constants.MAX_PLAYER_COUNT; i++)
                this.set(i, fatList.get(i).basicData);
        }

        public BasicPlayerData get(int index)
        {
            SUtil.ReleaseAssert(index < Constants.MAX_PLAYER_COUNT);
            fixed (byte* ptr = _playerData)
            {
                BasicPlayerData* data = (BasicPlayerData*)ptr;
                return data[index];
            }
        }

        public void set(int index, BasicPlayerData data)
        {
            SUtil.ReleaseAssert(index < Constants.MAX_PLAYER_COUNT);
            fixed (byte* ptr = _playerData)
            {
                BasicPlayerData* dataPtr = (BasicPlayerData*)ptr;
                dataPtr[index] = data;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PlayerDataList
    {
        private fixed byte _playerData[PlayerData.SizeOf * Constants.MAX_PLAYER_COUNT];

        public PlayerData get(int index)
        {
            SUtil.ReleaseAssert(index < Constants.MAX_PLAYER_COUNT);
            fixed (byte* ptr = _playerData)
            {
                PlayerData* data = (PlayerData*)ptr;
                return data[index];
            }
        }

        public int indexOf(NetUid id)
        {
            for (int i = 0; i < Constants.MAX_PLAYER_COUNT; i++)
            {
                if (get(i).id == id)
                    return i;
            }
            return -1;
        }

        public void set(int index, PlayerData data)
        {
            SUtil.ReleaseAssert(index < Constants.MAX_PLAYER_COUNT);
            fixed (byte* ptr = _playerData)
            {
                PlayerData* dataPtr = (PlayerData*)ptr;
                dataPtr[index] = data;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct BasicPlayerData
    {
        public NetUid id;
        public Vec3f position;
        public Vec3f rotation;
        private byte _flags;

        public bool isCrouching { get => Bit.get(_flags, 0); set => Bit.set(ref _flags, 0, value); }
        public bool isGrounded { get => Bit.get(_flags, 1); set => Bit.set(ref _flags, 1, value); }

        public const int SizeOf = 44;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PlayerData
    {
        public BasicPlayerData basicData;
        private fixed byte _name[Constants.MAX_PLAYER_NAME_LENGTH_UTF8_BYTES + 1];

        public NetUid id => basicData.id;
        public Vec3f position => basicData.position;
        public Vec3f rotation => basicData.rotation;
        public bool isCrouching => basicData.isCrouching;
        public bool isGrounded => basicData.isGrounded;

        public const int SizeOf = 108;

        public PlayerData(NetUid id, string name, Vector3 position, Vector3 eulerRotation, bool isCrouching, bool isGrounded)
        {
            basicData = new BasicPlayerData
            {
                id = id,
                position = position,
                rotation = eulerRotation,
                isCrouching = isCrouching,
                isGrounded = isGrounded,
            };

            byte[] nameTemp = Encoding.UTF8.GetBytes(name);
            int i = 0;
            for (; i < nameTemp.Length && i < Constants.MAX_PLAYER_NAME_LENGTH_UTF8_BYTES; i++)
                _name[i] = nameTemp[i];
            _name[i] = 0;
        }

        public string getName()
        {
            fixed (byte* ptr = _name)
            {
                int len = 0;
                for (; len < Constants.MAX_PLAYER_NAME_LENGTH_UTF8_BYTES && ptr[len] != 0; len++) { }

                return Encoding.UTF8.GetString(ptr, len);
            }
        }
    }

    public unsafe struct PropHandle : IEquatable<PropHandle>
    {
        public SlabHandle real;
        public static PropHandle Null => new() { real = SlabHandle.Null };
        public bool Equals(PropHandle other) { return real.Equals(other.real); }
        public override bool Equals(object obj) => obj is PropHandle other && Equals(other);
        public static bool operator ==(PropHandle a, PropHandle b) { return a.Equals(b); }
        public static bool operator !=(PropHandle a, PropHandle b) { return !a.Equals(b); }
        public override int GetHashCode() { return real.GetHashCode(); }
        public Prop* p => PiperMain.p.game.world.get(this);
        public Prop* tryGetPointer() => PiperMain.p.game.world.tryGet(this);
    }
}
