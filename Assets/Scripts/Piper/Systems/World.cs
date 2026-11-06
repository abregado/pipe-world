using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PipeWorld.Piper
{
    public unsafe class World
    {
        public readonly PropAllocator props = new PropAllocator();
        public WorldData data;

        // Set when a save is loaded so PropSyncer re-creates every prop view instead of treating them as new placements.
        public bool resetSyncerOnNextLoad = false;

        public void update()
        {
            PropLogic.update(this);
            data.currentTick++;
        }

        private IEnumerable<SlabAllocator> parts()
        {
            yield return props;
        }

        private const uint SaveMagic = 0x57495053; // "SPIW"
        private const int SaveHeaderSize = 8;

        public byte[] save()
        {
            int totalNeeded = SaveHeaderSize + sizeof(WorldData);
            foreach (SlabAllocator part in parts())
                totalNeeded += 4 + part.sizeInBytes;

            byte[] bytes = new byte[totalNeeded];
            int offset = 0;

            Array.Copy(BitConverter.GetBytes(SaveMagic), 0, bytes, offset, 4);
            offset += 4;
            Array.Copy(BitConverter.GetBytes(Constants.saveVersion), 0, bytes, offset, 4);
            offset += 4;

            WorldData[] temp = new WorldData[1];
            temp[0] = this.data;
            fixed (WorldData* ptr = temp)
                Marshal.Copy((IntPtr)ptr, bytes, offset, sizeof(WorldData));
            offset += sizeof(WorldData);

            foreach (SlabAllocator part in parts())
            {
                Array.Copy(BitConverter.GetBytes(part.sizeInBytes), 0, bytes, offset, 4);
                offset += 4;

                part.writeToBytes(bytes, offset);
                offset += part.sizeInBytes;
            }

            return bytes;
        }

        public static bool TryReadSaveVersion(byte[] bytes, out int version, out int dataOffset)
        {
            if (bytes.Length >= SaveHeaderSize && BitConverter.ToUInt32(bytes, 0) == SaveMagic)
            {
                version = BitConverter.ToInt32(bytes, 4);
                dataOffset = SaveHeaderSize;
                return true;
            }

            version = 0;
            dataOffset = 0;
            return false;
        }

        public void load(byte[] bytes)
        {
            if (resetSyncerOnNextLoad)
            {
                PiperMain.p.game.syncer.initialSyncDone = false;
                resetSyncerOnNextLoad = false;
            }

            TryReadSaveVersion(bytes, out _, out int offset);

            fixed (byte* ptr = bytes)
            {
                WorldData* temp = (WorldData*)(ptr + offset);
                this.data = *temp;
                offset += sizeof(WorldData);
            }

            foreach (SlabAllocator part in parts())
            {
                int partSize = BitConverter.ToInt32(bytes, offset);
                offset += 4;

                part.loadFromBytes(bytes, offset, partSize);
                offset += partSize;
            }

            SUtil.ReleaseAssert(offset == bytes.Length);
        }

        public Prop* get(PropHandle handle) { return props.get(handle); }
        public Prop* tryGet(PropHandle handle) { return props.tryGet(handle); }
        public void free(PropHandle handle) { props.free(handle); }
    }

    public unsafe class PropAllocator : SlabAllocator<Prop>
    {
        public static SlabHandle convertHandle(PropHandle handle) { return handle.real; }
        public static PropHandle convertHandle(SlabHandle handle) { return new PropHandle { real = handle }; }

        public Prop* get(PropHandle handle) { return base.get(convertHandle(handle)); }
        public Prop* tryGet(PropHandle handle) { return base.tryGet(convertHandle(handle)); }
        public new PropHandle allocate(int playerIndex, uint perPlayerId) { return convertHandle(base.allocate(playerIndex, perPlayerId)); }
        public PropHandle allocate(AllocatorContext allocator) { return allocate(allocator.data.playerIndex, allocator.data.nextPropId++); }
        public void free(PropHandle handle) { base.free(convertHandle(handle)); }
    }
}
