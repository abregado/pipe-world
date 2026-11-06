using System;
using System.Runtime.InteropServices;

namespace PipeWorld.Piper
{
    public class PacketChunk
    {
        public const int maxPacketSize = 1450;
        private byte[] _bytes;
        private int _length = 0;

        public int length
        {
            get { return _length; }
            set
            {
                if (value > _bytes.Length)
                    throw new Exception();
                _length = value;
            }
        }

        public Span<byte> bytes => new Span<byte>(_bytes, 0, length);
        public Tuple<byte[], int> allBytesTuple => new Tuple<byte[], int>(_bytes, length);

        public PacketChunk(byte[] bytesIn = null)
        {
            if (bytesIn != null)
            {
                _bytes = bytesIn;
                length = _bytes.Length;
            }
            else
            {
                _bytes = new byte[maxPacketSize];
            }
        }

        public PacketChunk(int size) : this(null)
        {
            length = size;
            if (length > maxPacketSize)
                throw new Exception("packet size too big!");
        }

        public PacketType getPacketType()
        {
            if (length < 1)
                return PacketType.Invalid;

            return (PacketType)bytes[0];
        }
    }

    public class ReassemblyBuffer
    {
        private uint currentPacketIndex = 0;
        private PacketChunk[] parts = null;

        public unsafe void addPart(PacketChunk packet)
        {
            fixed (void* headerV = packet.bytes)
            {
                PacketChunkHeader* header = (PacketChunkHeader*)headerV;
                if (header->packetIndex > currentPacketIndex || parts == null)
                {
                    parts = new PacketChunk[header->partCountTotal];
                    currentPacketIndex = header->packetIndex;
                }

                parts[header->partIndex] = packet;
            }
        }

        public class ReassembledPacketData
        {
            public byte[] data;
            public uint packetIndex;
        }

        public ReassembledPacketData tryPopReassembled()
        {
            if (parts == null)
                return null;

            int totalLength = 0;
            foreach (var part in parts)
            {
                if (part == null)
                    return null;
                totalLength += part.length - Marshal.SizeOf<PacketChunkHeader>();
            }

            byte[] reassembled = new byte[totalLength];

            int offset = 0;
            foreach (var part in parts)
            {
                Span<byte> source = part.bytes.Slice(Marshal.SizeOf<PacketChunkHeader>());
                Span<byte> dest = new Span<byte>(reassembled, offset, source.Length);
                source.CopyTo(dest);
                offset += source.Length;
            }

            parts = null;
            return new ReassembledPacketData() { data = reassembled, packetIndex = currentPacketIndex };
        }
    }
}
