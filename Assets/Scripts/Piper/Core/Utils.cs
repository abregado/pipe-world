using System;

namespace PipeWorld.Piper
{
    public static class Utils
    {
        public static string formatSecondsAsTime(double seconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes:D2}m" : $"{span.Minutes}m {span.Seconds:D2}s";
        }
    }

    // Lets a struct live in a List and be mutated in place.
    public class BoxStruct<T> where T : struct
    {
        public T v;

        public BoxStruct(T value)
        {
            v = value;
        }
    }
}
