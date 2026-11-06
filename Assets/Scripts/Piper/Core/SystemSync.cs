using UnityEngine;

namespace PipeWorld.Piper
{
    public class SystemSync
    {
        public const float FADEIN_DISTANCE = 160f;
        public const float FADEOUT_DISTANCE = 180f;
        public const float COUNT_AS_NEW_PLACEMENT_DISTANCE = 30f;

        public readonly PropSyncer propSyncer;
        public bool initialSyncDone;

        public SystemSync(World world)
        {
            propSyncer = new PropSyncer(world);
        }

        public void sync(Vector3 viewerPosition)
        {
            propSyncer.sync(!initialSyncDone, viewerPosition);
            initialSyncDone = true;
        }
    }
}
