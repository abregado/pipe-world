using System;
using System.Runtime.InteropServices;

namespace PipeWorld.Piper
{
    // Every building entity is identified by the slab header's player index and per-player counter, packed into one id.
    // The slot index is not part of the id: a client and the server may place the same entity in different slots.
    public static class EntityId
    {
        public const uint None = 0;

        public static uint make(int playerIndex, uint counter) => ((uint)playerIndex << 24) | (counter & 0x00FFFFFFu);
        public static int playerIndex(uint id) => (int)(id >> 24);
        public static uint counter(uint id) => id & 0x00FFFFFFu;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Building
    {
        public SlabHeader slabHeader;
        // The floor plane's centre. A building's floor is level, and its ceiling is `height` above it.
        public Vec3f position;
        public float yaw;
        public float height;
        // The terrain level the building was placed on: the scaffold under the floor reaches down to it.
        public float groundY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BuildingVertexData
    {
        public SlabHeader slabHeader;
        public uint building;
        public float x;
        public float z;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BuildingEdgeData
    {
        public SlabHeader slabHeader;
        public uint building;
        public uint vertexA;
        public uint vertexB;
        public byte type;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct BuildingAreaData
    {
        public SlabHeader slabHeader;
        public uint building;
        public int edgeCount;
        public fixed uint edges[4];
        public byte type;
    }

    // Which surface of a building a prop is attached to. Walls have two sides: the one the edge's normal points toward and the other.
    public enum BuildingFace : byte
    {
        Floor = 0,
        Ceiling = 1,
        WallPositive = 2,
        WallNegative = 3,
    }

    public static class BuildingConstants
    {
        public const float CeilingHeight = 3f;
        public const float MinHeight = 2.4f;
        public const float MaxHeight = 6f;
        public const float MaxRaise = 8f;
        public const float PropMargin = 0.1f;
        public const float WallThickness = 0.16f;
        public const float GlassThickness = 0.06f;
        public const float FloorThickness = 0.2f;
        public const float FoundationDepth = 0.6f;
        public const float StartHalfSize = 1.5f;
        public const float MinEdgeLength = 0.5f;
        public const float MaxExtent = 40f;
        public const float MinBuildingSpacing = 8f;
    }

    // Allocator for one kind of building entity, looked up by id rather than by handle.
    public unsafe class EntitySlab<T> : SlabAllocator<T> where T : unmanaged
    {
        public static uint idOf(T* item) => ((SlabHeader*)item)->_playerIndexAndGeneration;

        public T* find(uint id)
        {
            for (int i = 0; i < slabs.size; i++)
            {
                T* item = slabs.get(i);
                SlabHeader* header = (SlabHeader*)item;
                if (header->allocated && header->_playerIndexAndGeneration == id)
                    return item;
            }
            return null;
        }

        public T* create(uint id)
        {
            SlabHandle handle = allocate(EntityId.playerIndex(id), EntityId.counter(id));
            return get(handle);
        }

        public void remove(T* item)
        {
            NativeFuncs.memset((IntPtr)item, 0, (UIntPtr)sizeof(T));
        }
    }

    // The values of BEdge.type. Solid and Outer are ordinary walls (outer is a wall with a room on one side only); Open and Glass
    // only apply to a wall between two rooms.
    public static class WallStyle
    {
        public const byte Solid = 0;
        public const byte Outer = 1;
        public const byte Open = 2;
        public const byte Glass = 3;

        public static string name(byte style) => style == Open ? "Open" : style == Glass ? "Glass" : "Solid";

        // The style a click on a wall between two rooms changes it to.
        public static byte next(byte style) => style == Solid || style == Outer ? Glass : style == Glass ? Open : Solid;
    }
}
