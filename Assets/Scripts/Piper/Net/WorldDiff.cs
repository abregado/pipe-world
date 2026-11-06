using System;
using System.Collections.Generic;

namespace PipeWorld.Piper
{
    public static unsafe class WorldDiff
    {
        public const uint NoAnchor = 0xFFFFFFFFu;
        public const byte TagCopy = 0;
        public const byte TagLiteral = 1;
        public const int HeaderSize = 16;

        public static byte[] encode(
            uint anchorId,
            byte[] anchorBytes,
            Dictionary<ulong, ContentDefinedChunkRef> anchorChunkIndex,
            uint newId,
            byte[] newBytes,
            List<ContentDefinedChunkRef> scratchChunks)
        {
            uint crc;
            fixed (byte* p = newBytes)
                crc = Crc32.xcrc32(p, newBytes.Length);

            List<byte> output = new List<byte>(Math.Min(newBytes.Length, 4096));
            writeU32(output, anchorId);
            writeU32(output, newId);
            writeU32(output, (uint)newBytes.Length);
            writeU32(output, crc);
            SUtil.DebugAssert(output.Count == HeaderSize);

            if (anchorId == NoAnchor || anchorBytes == null || anchorChunkIndex == null)
            {
                writeLiteral(output, newBytes, 0, newBytes.Length);
                return output.ToArray();
            }

            fixed (byte* newBytesPtr = newBytes)
            fixed (byte* anchorBytesPtr = anchorBytes)
            {
                ContentDefinedChunker.split(newBytesPtr, newBytes.Length, scratchChunks);

                int pendingLiteralStart = -1;
                int pendingLiteralEnd = -1;

                for (int i = 0; i < scratchChunks.Count; i++)
                {
                    ContentDefinedChunkRef chunk = scratchChunks[i];
                    ulong hash = Standart.Hash.xxHash.xxHash64.ComputeHash(newBytesPtr + chunk.offset, chunk.length);

                    ContentDefinedChunkRef anchorChunk = default;
                    bool matches = anchorChunkIndex.TryGetValue(hash, out anchorChunk) && chunksEqual(anchorBytesPtr, anchorChunk, newBytesPtr, chunk);

                    if (matches)
                    {
                        if (pendingLiteralStart != -1)
                        {
                            writeLiteral(output, newBytes, pendingLiteralStart, pendingLiteralEnd - pendingLiteralStart);
                            pendingLiteralStart = -1;
                        }

                        output.Add(TagCopy);
                        writeU32(output, (uint)anchorChunk.offset);
                        writeU32(output, (uint)anchorChunk.length);
                    }
                    else
                    {
                        if (pendingLiteralStart == -1)
                            pendingLiteralStart = chunk.offset;
                        pendingLiteralEnd = chunk.offset + chunk.length;
                    }
                }

                if (pendingLiteralStart != -1)
                    writeLiteral(output, newBytes, pendingLiteralStart, pendingLiteralEnd - pendingLiteralStart);
            }

            return output.ToArray();
        }

        private static bool chunksEqual(byte* baseA, ContentDefinedChunkRef chunkA, byte* baseB, ContentDefinedChunkRef chunkB)
        {
            if (chunkA.length != chunkB.length)
                return false;

            return NativeFuncs.memcmp((IntPtr)(baseA + chunkA.offset), (IntPtr)(baseB + chunkB.offset), (UIntPtr)chunkA.length) == 0;
        }

        public static byte[] decode(byte[] diff, byte[] anchorBytes, out uint newId)
        {
            SUtil.ReleaseAssert(diff.Length >= HeaderSize);

            uint anchorId = readU32(diff, 0);
            newId = readU32(diff, 4);
            uint newLength = readU32(diff, 8);
            uint expectedCrc = readU32(diff, 12);

            SUtil.ReleaseAssert((anchorId == NoAnchor && anchorBytes == null) || (anchorId != NoAnchor && anchorBytes != null));

            byte[] newBytes = new byte[newLength];

            int readOffset = HeaderSize;
            int writeOffset = 0;

            while (readOffset < diff.Length)
            {
                byte tag = diff[readOffset++];
                if (tag == TagCopy)
                {
                    uint anchorOffset = readU32(diff, readOffset); readOffset += 4;
                    uint size = readU32(diff, readOffset); readOffset += 4;
                    SUtil.ReleaseAssert(anchorOffset + size <= (uint)anchorBytes.Length);
                    SUtil.ReleaseAssert(writeOffset + size <= newLength);
                    Buffer.BlockCopy(anchorBytes, (int)anchorOffset, newBytes, writeOffset, (int)size);
                    writeOffset += (int)size;
                }
                else if (tag == TagLiteral)
                {
                    uint size = readU32(diff, readOffset); readOffset += 4;
                    SUtil.ReleaseAssert(readOffset + size <= diff.Length);
                    SUtil.ReleaseAssert(writeOffset + size <= newLength);
                    Buffer.BlockCopy(diff, readOffset, newBytes, writeOffset, (int)size);
                    readOffset += (int)size;
                    writeOffset += (int)size;
                }
                else
                {
                    SUtil.ReleaseAssert(false);
                }
            }

            SUtil.ReleaseAssert(writeOffset == newLength);

            uint actualCrc;
            fixed (byte* p = newBytes)
                actualCrc = Crc32.xcrc32(p, newBytes.Length);
            SUtil.ReleaseAssert(actualCrc == expectedCrc);

            return newBytes;
        }

        public static uint peekAnchorId(byte[] diff)
        {
            SUtil.ReleaseAssert(diff.Length >= 4);
            return readU32(diff, 0);
        }

        private static void writeU32(List<byte> output, uint value)
        {
            output.Add((byte)(value & 0xFF));
            output.Add((byte)((value >> 8) & 0xFF));
            output.Add((byte)((value >> 16) & 0xFF));
            output.Add((byte)((value >> 24) & 0xFF));
        }

        private static uint readU32(byte[] buf, int offset)
        {
            return (uint)buf[offset]
                 | ((uint)buf[offset + 1] << 8)
                 | ((uint)buf[offset + 2] << 16)
                 | ((uint)buf[offset + 3] << 24);
        }

        private static void writeLiteral(List<byte> output, byte[] src, int srcOffset, int length)
        {
            output.Add(TagLiteral);
            writeU32(output, (uint)length);
            for (int i = 0; i < length; i++)
                output.Add(src[srcOffset + i]);
        }
    }
}
