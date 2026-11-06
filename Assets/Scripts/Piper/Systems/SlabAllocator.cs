using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PipeWorld.Piper
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SlabHeader
    {
        private uint _indexAndAllocated;
        public uint _playerIndexAndGeneration;

        public bool allocated
        {
            get => (_indexAndAllocated & (1U << 31)) != 0;
            set
            {
                uint uIndex = _indexAndAllocated & 0x7FFFFFFF;
                uint uVal = (value ? (1U << 31) : 0);
                _indexAndAllocated = uIndex | uVal;
            }
        }

        public int index
        {
            get => (int)(_indexAndAllocated & 0x7FFFFFFF);
            set
            {
                SUtil.DebugAssert(value >= 0);
                uint a = _indexAndAllocated & (1U << 31);
                uint uVal = (uint)value;
                _indexAndAllocated = a | uVal;
            }
        }

        public int playerIndex
        {
            get => (int)((_playerIndexAndGeneration & 0xFF000000U) >> 24);
            set
            {
                SUtil.DebugAssert(value >= 0 && value < 256);
                _playerIndexAndGeneration = (_playerIndexAndGeneration & 0x00FFFFFFU) | (((uint)value) << 24);
            }
        }

        public uint perPlayerGeneration
        {
            get => _playerIndexAndGeneration & 0x00FFFFFFU;
            set
            {
                SUtil.DebugAssert(value <= 0x00FFFFFFU);
                _playerIndexAndGeneration = (_playerIndexAndGeneration & 0xFF000000U) | value;
            }
        }
    };

    public struct SlabHandle : IEquatable<SlabHandle>
    {
        public int v;
        public uint _playerIndexAndGeneration;

        public int playerIndex => (int)((_playerIndexAndGeneration & 0xFF000000U) >> 24);
        public uint perPlayerGeneration => _playerIndexAndGeneration & 0x00FFFFFFU;
        public static SlabHandle Null => new SlabHandle { v = 0, _playerIndexAndGeneration = 0 };
        public bool Equals(SlabHandle other) { return v == other.v && _playerIndexAndGeneration == other._playerIndexAndGeneration; }
        public override int GetHashCode() { return HashCode.Combine(v, _playerIndexAndGeneration); }
    }

    public interface SlabAllocator
    {
        public void loadFromBytes(byte[] bytes, int offset, int size);
        public void writeToBytes(byte[] bytes, int offset);

        public int sizeInBytes { get; }
    }

    public abstract unsafe class SlabAllocator<T>: SlabAllocator where T : unmanaged
    {
        public UnManagedArray<T> slabs;

        protected SlabAllocator(int initialSize = 256)
        {
            // assert that the first struct member is a SlabHeader, and its offset is 0, so we can cast internally
            FieldInfo firstField = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)[0];
            SUtil.DebugAssert(firstField.FieldType == typeof(SlabHeader));
            IntPtr offset = Marshal.OffsetOf(typeof(T), firstField.Name);
            SUtil.DebugAssert(offset == IntPtr.Zero);

            slabs = new UnManagedArray<T>(initialSize);
            slabs.zeroMemory();
        }

        protected T* get(SlabHandle handle)
        {
            int index = handle.v - 1;
            SUtil.ReleaseAssert(index >= 0 && index < slabs.size);

            SlabHeader* item = (SlabHeader*)slabs.get(index);

            SUtil.ReleaseAssert(item->allocated);
            SUtil.ReleaseAssert(item->_playerIndexAndGeneration == handle._playerIndexAndGeneration);

            return (T*)item;
        }

        protected T* tryGet(SlabHandle handle)
        {
            int index = handle.v - 1;
            if (index < 0 || index >= slabs.size)
                return null;

            SlabHeader* item = (SlabHeader*)slabs.get(index);

            if (!item->allocated)
                return null;

            if (item->_playerIndexAndGeneration != handle._playerIndexAndGeneration)
                return null;

            return (T*)item;
        }

        protected SlabHandle allocate(int playerIndex, uint perPlayerId)
        {
            int index = -1;

            for (int i = 0; i < slabs.size; i++)
            {
                SlabHeader* temp = (SlabHeader*)slabs.get(i);
                if (!temp->allocated)
                {
                    index = i;
                    break;
                }
            }

    #if DEBUG
            for (int i = 0; i < slabs.size; i++)
            {
                SlabHeader* temp = (SlabHeader*)slabs.get(i);
                if (temp->allocated)
                    SUtil.DebugAssert(temp->playerIndex != playerIndex || temp->perPlayerGeneration != perPlayerId);
            }
    #endif

            if (index == -1)
            {
                int oldSize = slabs.size;
                slabs.realloc(Math.Max(1, oldSize * 2), true);
                index = oldSize;
            }

            SlabHeader* item = (SlabHeader*)slabs.get(index);

            item->allocated = true;
            item->index = index + 1;
            item->playerIndex = playerIndex;
            item->perPlayerGeneration = perPlayerId;

            return fabricateHandleFromPointer((T*)item);
        }

        public static SlabHandle fabricateHandleFromHeader(SlabHeader header)
        {
            return new SlabHandle() { v = header.index, _playerIndexAndGeneration = header._playerIndexAndGeneration };
        }

        protected static SlabHandle fabricateHandleFromPointer(T* item)
        {
            SlabHeader header = *((SlabHeader*)item);
            return fabricateHandleFromHeader(header);
        }

        protected void free(SlabHandle handle)
        {
            SlabHeader* item = (SlabHeader*)get(handle);
            SUtil.ReleaseAssert(item->allocated);
            NativeFuncs.memset((IntPtr)item, 0, (UIntPtr)sizeof(T));
        }

        public void loadFromBytes(byte[] bytes, int offset, int size)
        {
            slabs.reallocInBytes(size, zeroNew: false);
            Marshal.Copy(bytes, offset, (IntPtr)slabs.bytes, size);
        }

        public void writeToBytes(byte[] bytes, int offset)
        {
            Marshal.Copy((IntPtr)slabs.bytes, bytes, offset, slabs.sizeInBytes);
        }

        public int sizeInBytes => slabs.sizeInBytes;
    }
}
