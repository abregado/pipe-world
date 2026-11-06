using System;
using System.Collections.Generic;

namespace PipeWorld.Piper
{
    public class SUtil
    {
        public static void ReleaseAssert(bool val, string message = "")
        {
            if (!val)
                throw new Exception("assertion failed" + (message.Length == 0 ? "" : ": " + message));
        }

        public static void ReleaseAssertEqual(int expected, int actual)
        {
            if (expected != actual)
                throw new Exception("assertion failed, expected: " + expected + ", actual: " + actual);
        }


        public static void DebugAssert(bool val, string message = "")
        {
    #if DEBUG
            if (!val)
                throw new Exception("assertion failed" + (message.Length == 0 ? "" : ": " + message));
    #endif
        }

        public static void shuffle<T>(IList<T> list, Random random)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }

    public static class Bit
    {
        public static void set(ref int bits, int i, bool value)
        {
            SUtil.DebugAssert(i < 32);
            if (value)
                bits = bits | (1 << i);
            else
                bits = bits & ~(1 << i);
        }

        public static bool get(int bits, int i)
        {
            SUtil.DebugAssert(i < 32);
            return (bits & (1 << i)) != 0;
        }

        public static void set(ref byte bits, int i, bool value)
        {
            SUtil.DebugAssert(i < 8);
            if (value)
                bits = (byte)(((int)bits) | (1 << i));
            else
                bits = (byte)(((int)bits) & ~(1 << i));
        }

        public static bool get(byte bits, int i)
        {
            SUtil.DebugAssert(i < 8);
            return (((int)bits) & (1 << i)) != 0;
        }
    }
}
