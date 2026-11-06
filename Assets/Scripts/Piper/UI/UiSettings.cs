using System;

namespace PipeWorld.Piper
{
    public static class UiSettings {
        public static event Action Changed;

        public static void NotifyChanged() => Changed?.Invoke();
    }
}
